using IndxCloudApi.Data;
using IndxCloudApi.Models;
using IndxCloudApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using Utilities;

namespace IndxCloudApi;

/// <summary>
/// Main application entry point for Indx Cloud API
/// </summary>
public class Program
{
    /// <summary>
    /// Application entry point - configures and starts the web host
    /// </summary>
    /// <param name="args">Command line arguments</param>
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ============================================
        // RAZOR COMPONENTS (Blazor Server UI)
        // ============================================
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddCascadingAuthenticationState();
        builder.Services.AddScoped<Components.Account.IdentityUserAccessor>();
        builder.Services.AddScoped<Components.Account.IdentityRedirectManager>();
        builder.Services.AddScoped<AuthenticationStateProvider, Components.Account.IdentityRevalidatingAuthenticationStateProvider>();

        // ============================================
        // DATABASE CONFIGURATION
        // ============================================
        var identityConnectionString = ConnectionStringHelper.GetIdentityConnectionString(builder.Configuration);
        var dbPath = ConnectionStringHelper.ExtractDbPath(identityConnectionString);
        Console.WriteLine($"Using Identity database: {dbPath}");

        // Ensure database directory exists (works on both local and Azure)
        try
        {
            ConnectionStringHelper.EnsureDatabaseDirectoryExists(identityConnectionString);

            // Additional check: manually ensure directory exists for Azure compatibility
            var dbDirectory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dbDirectory) && !Directory.Exists(dbDirectory))
            {
                Directory.CreateDirectory(dbDirectory);
                Console.WriteLine($"✓ Created database directory: {dbDirectory}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Warning: Could not ensure database directory exists: {ex.Message}");
            Console.WriteLine("Attempting to continue - database will be created if directory is writable");
        }

        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlite(identityConnectionString));

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        // ============================================
        // IDENTITY CONFIGURATION
        // ============================================

        // ============================================
        // REGISTRATION RESTRICTION CONFIGURATION
        // ============================================
        builder.Services.Configure<RegistrationOptions>(
            builder.Configuration.GetSection("Registration"));
        builder.Services.AddScoped<RegistrationValidator>();

        var registrationMode = builder.Configuration["Registration:Mode"] ?? "Open";
        Console.WriteLine($"ℹ Registration mode: {registrationMode}");

        if (registrationMode.Equals("EmailDomain", StringComparison.OrdinalIgnoreCase))
        {
            var allowedDomains = builder.Configuration.GetSection("Registration:AllowedDomains").Get<List<string>>();
            if (allowedDomains?.Any() == true)
            {
                Console.WriteLine($"✓ Allowed email domains: {string.Join(", ", allowedDomains)}");
            }
            else
            {
                Console.WriteLine("⚠ EmailDomain mode configured but no allowed domains specified");
            }
        }
        else if (registrationMode.Equals("Closed", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("⚠ Registration is closed - only admins can create accounts");
        }

        // ============================================
        // EMAIL SERVICE CONFIGURATION (Optional - configurable)
        // ============================================
        var emailProvider = builder.Configuration["Email:Provider"]?.ToLower() ?? "console";

        switch (emailProvider)
        {
            case "azurecommunicationservices":
            case "acs":
                var acsConnectionString = builder.Configuration["Email:AzureCommunicationServices:ConnectionString"];
                if (!string.IsNullOrEmpty(acsConnectionString))
                {
                    builder.Services.AddTransient<IEmailSender, AzureCommunicationEmailSender>();
                    Console.WriteLine("✓ Email configured: Azure Communication Services");
                }
                else
                {
                    builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();
                    Console.WriteLine("⚠ Azure Communication Services not configured, using Console mode");
                }
                break;

            case "console":
            default:
                builder.Services.AddTransient<IEmailSender, ConsoleEmailSender>();
                Console.WriteLine("ℹ Email configured: Console mode (emails logged to console)");
                break;
        }

        // ============================================
        // DUAL AUTHENTICATION: Cookies + JWT + External
        // ============================================

        // Identity registration (includes cookie authentication automatically)
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Configure Authentication with multiple schemes
        var authBuilder = builder.Services.AddAuthentication();

        // JWT Authentication for API
        var jwtkey = builder.Configuration["Jwt:Key"];
        if (string.IsNullOrEmpty(jwtkey))
        {
            throw new InvalidOperationException("JWT Key is not configured. Please set a secure key in appsettings.json or user secrets.");
        }

        // Warn if using the default/insecure key
        var defaultKey = "your-secret-key-minimum-32-characters-change-in-production";
        if (jwtkey == defaultKey)
        {
            Console.WriteLine("⚠ WARNING: Using default JWT key from appsettings.json");
            Console.WriteLine("⚠ This is OK for development/testing, but MUST be changed in production!");
            Console.WriteLine("⚠ Set a secure key using: dotnet user-secrets set \"Jwt:Key\" \"your-secure-key-here\"");
        }
        else
        {
            Console.WriteLine("✓ JWT authentication configured with custom key");
        }

        authBuilder.AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtkey)),
                ValidateIssuer = true,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        // Google Authentication (Optional - configure in appsettings.json or User Secrets)
        var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
        var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        if (!string.IsNullOrEmpty(googleClientId) &&
            !string.IsNullOrEmpty(googleClientSecret) &&
            !googleClientId.StartsWith("your-"))
        {
            authBuilder.AddGoogle(options =>
            {
                options.ClientId = googleClientId;
                options.ClientSecret = googleClientSecret;
                options.CallbackPath = "/signin-google";

                // Optional: Request additional scopes
                options.Scope.Add("profile");
                options.Scope.Add("email");

                // Save tokens for later use
                options.SaveTokens = true;
            });

            Console.WriteLine("✓ Google authentication configured");
        }
        else
        {
            Console.WriteLine("ℹ Google authentication not configured (optional)");
        }

        // Microsoft Authentication (Optional - configure in appsettings.json or User Secrets)
        var microsoftClientId = builder.Configuration["Authentication:Microsoft:ClientId"];
        var microsoftClientSecret = builder.Configuration["Authentication:Microsoft:ClientSecret"];
        if (!string.IsNullOrEmpty(microsoftClientId) &&
            !string.IsNullOrEmpty(microsoftClientSecret) &&
            !microsoftClientId.StartsWith("your-"))
        {
            authBuilder.AddMicrosoftAccount(options =>
            {
                options.ClientId = microsoftClientId;
                options.ClientSecret = microsoftClientSecret;
                options.CallbackPath = "/signin-microsoft";

                // Optional: Request additional scopes
                options.Scope.Add("User.Read");

                // Save tokens for later use
                options.SaveTokens = true;
            });

            Console.WriteLine("✓ Microsoft authentication configured");
        }
        else
        {
            Console.WriteLine("ℹ Microsoft authentication not configured (optional)");
        }

        // ============================================
        // SWAGGER CONFIGURATION
        // ============================================
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1.0",
                Title = "Indx Cloud API",
                Description = "JWT Authenticated HTTP API for Indx Search"
            });

            var filePath = Path.Combine(AppContext.BaseDirectory, "IndxCloudApi.xml");
            if (File.Exists(filePath))
            {
                c.IncludeXmlComments(filePath);
            }

            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
            {
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
                }
            });
        });

        // ============================================
        // CONTROLLERS & API CONFIGURATION
        // ============================================
        builder.Services.AddControllers(options =>
        {
            options.InputFormatters.Add(new TextPlainInputFormatter());
        })
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.IncludeFields = true;
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });

        builder.Services.Configure<IISServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("NewPolicy", builder =>
                builder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        builder.WebHost.ConfigureKestrel(serverOptions =>
        {
            serverOptions.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
            serverOptions.Limits.MaxRequestBodySize = 2_000_000_000;
            serverOptions.AllowSynchronousIO = true;
        });

        var app = builder.Build();

        // ============================================
        // DATABASE INITIALIZATION
        // ============================================
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILogger<Program>>();

            try
            {
                logger.LogInformation("Initializing Identity database...");
                var context = services.GetRequiredService<ApplicationDbContext>();

                // Ensure database is created
                logger.LogInformation("Creating database if it doesn't exist...");
                context.Database.EnsureCreated();
                logger.LogInformation("✓ Database created successfully");

                // Enable WAL mode for better concurrency (may not work on all filesystems)
                try
                {
                    context.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
                    context.Database.ExecuteSqlRaw("PRAGMA busy_timeout=5000;");
                    logger.LogInformation("✓ SQLite WAL mode enabled");
                }
                catch (Exception walEx)
                {
                    logger.LogWarning(walEx, "Could not enable WAL mode (may not be supported on this filesystem)");
                }

                logger.LogInformation("✓ Identity database initialized at: {Path}", dbPath);

                // Seed initial data
                logger.LogInformation("Seeding initial data...");
                SeedData(services).Wait();
                logger.LogInformation("✓ Initial data seeded successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "FATAL: Error initializing Identity database at {Path}", dbPath);
                logger.LogError("Database directory: {Dir}", Path.GetDirectoryName(dbPath));
                logger.LogError("Directory exists: {Exists}", Directory.Exists(Path.GetDirectoryName(dbPath)));
                throw; // Re-throw to cause app startup failure - this is critical
            }
        }

        // ============================================
        // HTTP PIPELINE CONFIGURATION
        // ============================================
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseCors("NewPolicy");
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();


        // ============================================
        // ENDPOINT MAPPING
        // ============================================

        // Map Blazor Components (UI)
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        // Map Identity endpoints (login, register, etc.)
        app.MapAdditionalIdentityEndpoints();

        // Map API Controllers
        app.MapControllers();

        // Swagger UI
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Indx Cloud API v1.0");
            c.RoutePrefix = "swagger";

            // Auto-authenticate with JWT token if user is logged in
            c.InjectJavascript("/swagger-auth.js");
        });

        // ============================================
        // INTERNAL API INITIALIZATION
        // ============================================
        var searchConnectionString = ConnectionStringHelper.GetSearchDataConnectionString(builder.Configuration);
        var searchDbPath = ConnectionStringHelper.ExtractDbPath(searchConnectionString);
        Console.WriteLine($"Using Search database: {searchDbPath}");

        // Ensure search database directory exists (works on both local and Azure)
        try
        {
            ConnectionStringHelper.EnsureDatabaseDirectoryExists(searchConnectionString);

            // Additional check: manually ensure directory exists for Azure compatibility
            var searchDbDirectory = Path.GetDirectoryName(searchDbPath);
            if (!string.IsNullOrEmpty(searchDbDirectory) && !Directory.Exists(searchDbDirectory))
            {
                Directory.CreateDirectory(searchDbDirectory);
                Console.WriteLine($"✓ Created search database directory: {searchDbDirectory}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Warning: Could not ensure search database directory exists: {ex.Message}");
        }

        try
        {
            IndxCloudInternalApi.StartUpSystem(searchConnectionString);
            Console.WriteLine($"✓ Search system initialized at: {searchDbPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠ Warning: Search system initialization failed: {ex.Message}");
            Console.WriteLine("The application will start but search functionality may be limited");
        }

        app.Run();
    }

    // ============================================
    // SEED DATA METHOD
    // ============================================
    private static async Task SeedData(IServiceProvider services)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = services.GetRequiredService<ILogger<Program>>();

        // Create roles
        string[] roleNames = { "Admin", "User", "ApiUser" };
        foreach (var roleName in roleNames)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new IdentityRole(roleName));
                logger.LogInformation("Created role: {RoleName}", roleName);
            }
        }

        // Create admin user
        var adminEmail = "admin@indx.co";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(adminUser, "Admin123!@#");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("Admin user created: {Email}", adminEmail);
            }
        }
    }
}