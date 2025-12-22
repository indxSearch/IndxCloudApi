# Indx Cloud API

A **ready-to-run** starter template for building a search service with **Indx Search**.

**Works immediately without configuration!** No Azure accounts, OAuth setup, or email services required to get started.

This template provides a complete Blazor Server application with REST API, user authentication, and everything you need to deploy a multi-user search service. Try it first, configure it later!

## What's Included

**Core Features:**
- Blazor Server UI with interactive web interface
- REST API with JWT authentication
- User management and authentication (local accounts + OAuth)
- API key generation for programmatic access
- SQLite databases (ready for production alternatives)
- Swagger/OpenAPI documentation

**Authentication:**
- Local accounts (username/password) - works immediately
- Microsoft OAuth (Azure AD) - optional
- Google OAuth - optional
- Password reset via email - works via console logging (optional SMTP)

**Developer Experience:**
- Works out of the box with minimal configuration
- User Secrets for local development
- Environment variables for production
- Comprehensive setup guides

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A code editor (VS Code, Visual Studio, Rider)

### Get Running in 2 Steps

**No configuration needed!** This template works immediately without any secrets or external services.

1. **Clone and run**
   ```bash
   git clone <repository-url>
   cd IndxCloudApi
   dotnet run
   ```

2. **Open your browser**
   - Navigate to `https://localhost:5001`
   - Register an account at `/Account/Register`
   - Start using the search API

**That's it!** The template works out-of-the-box with:
- ✓ Local user accounts (username/password)
- ✓ SQLite databases (auto-created in `./IndxData/`)
- ✓ Console email mode (emails logged to console, no SMTP needed)
- ✓ Default JWT configuration (secure for testing, customize for production)

**No Azure, OAuth, or email service required to get started!**

### Testing Email Features (Console Mode)

When testing password reset or other email features, check your console output. All emails are logged there:

```
========================================
EMAIL SENT
To: user@example.com
Subject: Reset your password
Body:
Please reset your password by clicking here: <a href='https://localhost:5001/Account/ResetPassword?code=...'>link</a>
========================================
```

You can copy the reset link directly from the console and paste it into your browser.

## Project Structure

```
IndxCloudApi/
├── Components/
│   └── Account/          # Authentication & account management pages
├── Controllers/          # REST API controllers (search, login)
├── Data/                 # Database context and models
├── Services/             # Email services (Console, Azure Communication)
├── Shared/               # Layout and shared UI components
├── docs/                 # Detailed setup guides
└── IndxData/             # SQLite databases (identity.db, indx.db)
```

## Configuration

**The template works without any configuration!** All settings below are optional and can be configured when you're ready for production or want additional features.

### What Works Without Configuration

- ✓ **User Registration & Login** - Local accounts with username/password
- ✓ **JWT API Authentication** - Uses default key (will show warning on startup)
- ✓ **Email Notifications** - Logged to console (perfect for testing)
- ✓ **Password Reset** - Works via console emails
- ✓ **API Key Generation** - Full JWT token management

### Optional Configuration (When You're Ready)

#### 1. Production JWT Key (Recommended for Production)

```bash
# Set a custom JWT key for production security
dotnet user-secrets set "Jwt:Key" "your-secret-key-minimum-32-characters-here"
```

#### 2. OAuth Providers (Optional - Social Login)

Enable Microsoft or Google authentication:

```bash
# Microsoft OAuth
dotnet user-secrets set "Authentication:Microsoft:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "your-client-secret"

# Google OAuth
dotnet user-secrets set "Authentication:Google:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Google:ClientSecret" "your-client-secret"
```

See detailed guide: [OAuth Setup](docs/OAUTH_SETUP.md)

#### 3. Production Email Service (Optional - Real Emails)

Configure Azure Communication Services for sending real emails:

```bash
dotnet user-secrets set "Email:Provider" "AzureCommunicationServices"
dotnet user-secrets set "Email:AzureCommunicationServices:ConnectionString" "your-connection-string"
```

See detailed guide: [Email Setup](docs/EMAIL_SETUP.md)

### Production (Environment Variables)

Use environment variables with double underscores for nested configuration:

```bash
export Jwt__Key="your-secret-key-minimum-32-characters"
export Authentication__Microsoft__ClientId="your-client-id"
export Authentication__Microsoft__ClientSecret="your-client-secret"
export Email__Provider="AzureCommunicationServices"
```

## Using the API

### Web UI Access

1. Register: `/Account/Register`
2. Login: `/Account/Login`
3. Manage account: `/Account/Manage`

### API Access

1. **Generate an API key:**
   - Login to the web UI
   - Navigate to **API Key** in the menu
   - Generate a JWT token (30/90/180/365 days)

2. **Use the token:**
   ```bash
   curl -H "Authorization: Bearer <your-token>" https://localhost:5001/api/search
   ```

3. **API Documentation:**
   - Swagger UI: `https://localhost:5001/swagger`
   - Interactive testing and full endpoint documentation

## Database

**SQLite (Identity only):**
- `./IndxData/identity.db` - User accounts and authentication
- `./IndxData/indx.db` - Application configuration (Indx Search is memory-based)

**Important:** Indx Search keeps all search indexes in memory for performance. The `indx.db` file is used for configuration and metadata only, not for storing search data.

**Migrations:**
```bash
# Create a migration for identity database
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

**Production:** For identity storage, SQLite works well for moderate loads. For high-traffic scenarios, configure SQL Server, PostgreSQL, or MySQL by updating the connection string in `Program.cs`.

## Security

**Default Configuration (Development/Testing):**
- ✓ Safe for local development and testing
- ✓ Uses a default JWT key (you'll see a warning on startup)
- ✓ No secrets exposed in git
- ✓ HTTPS enabled by default
- ✓ Open registration (anyone can create an account)

**Password Requirements:**
- Minimum 8 characters
- Uppercase, lowercase, digit, and special character required

**JWT Tokens:**
- Configurable expiration (30-365 days)
- Default key is secure for testing, but should be customized for production

### Restricting Registration

By default, **anyone can register** for ease of getting started. You can easily restrict who can register by configuring the registration mode:

#### Open Registration (Default)
```json
{
  "Registration": {
    "Mode": "Open"
  }
}
```
Anyone can create an account - perfect for getting started or public APIs.

#### Restrict by Email Domain
```json
{
  "Registration": {
    "Mode": "EmailDomain",
    "AllowedDomains": ["yourcompany.com", "partner.com"]
  }
}
```
Only users with email addresses from specified domains can register - great for organization-specific APIs.

#### Close Registration
```json
{
  "Registration": {
    "Mode": "Closed"
  }
}
```
No one can self-register. Only admins can create accounts - ideal for private APIs with known users.

#### Using Environment Variables (Production)
```bash
# Azure App Service Application Settings
Registration__Mode = "EmailDomain"
Registration__AllowedDomains__0 = "yourcompany.com"
Registration__AllowedDomains__1 = "partner.com"

# Or for closed registration
Registration__Mode = "Closed"
```

The registration page will automatically show appropriate messages to users based on your configuration.

**Production Best Practices:**
- Change the JWT key (see Configuration section above)
- Configure registration restrictions based on your use case
- Never commit secrets to git
- Use User Secrets for development
- Use Azure Key Vault or environment variables for production
- Enable HTTPS (included by default)
- Rotate secrets regularly

## Deployment

### Azure App Service (Zero Configuration!)

**This template works on Azure without any configuration!** Just deploy and run.

#### Deploy Steps:

1. **Create App Service** in Azure Portal
   - Choose **.NET 9** runtime
   - Any tier (Free F1 works for testing)

2. **Configure Environment** (Important!)
   - Go to **Configuration → General settings**
   - Set **Stack**: .NET
   - Set **Major version**: .NET 9
   - Set **ASPNETCORE_ENVIRONMENT** = `Production` (uses appsettings.production.json)

3. **Deploy**
   ```bash
   # Using Azure CLI
   az webapp up --name your-app-name --resource-group your-resource-group

   # Or publish and deploy
   dotnet publish -c Release
   # Then deploy using Azure Portal, VS Code, or GitHub Actions
   ```

That's it! The app will use:
- ✓ SQLite databases in Azure's persistent storage (`D:\home\data\`)
- ✓ Default JWT key (with warning message)
- ✓ Console email logging (visible in Log Stream)
- ✓ No Application Settings required!

#### Optional: Production Configuration

Once deployed, you can optionally configure:

**Application Settings** (Configuration → Application settings):
```
Jwt__Key = "your-custom-production-key-32-characters-minimum"
Authentication__Microsoft__ClientId = "your-client-id"
Authentication__Microsoft__ClientSecret = "your-client-secret"
Email__Provider = "AzureCommunicationServices"
Email__AzureCommunicationServices__ConnectionString = "your-acs-connection"
```

**Remember**: If adding OAuth, update redirect URIs in Azure AD to include:
- `https://your-app.azurewebsites.net/signin-microsoft`
- `https://your-app.azurewebsites.net/signin-google`

## Development Commands

```bash
# Build the project
dotnet build

# Run with auto-reload
dotnet watch run

# Run tests
dotnet test

# View user secrets
dotnet user-secrets list
```

## Customization

This is a **starter template** - customize it for your needs:

1. **Add your search implementation** in `Controllers/SearchController.cs`
2. **Customize the UI** in `Components/` and `Shared/`
3. **Add more API endpoints** in `Controllers/`
4. **Change database** by updating connection strings in `Program.cs`
5. **Add email provider** by implementing `IEmailSender` in `Services/`

## Dependencies

- **IndxSearchLib** (4.1.0) - Core search functionality
- ASP.NET Core 9.0 - Web framework
- Entity Framework Core - Database ORM
- Swashbuckle - API documentation
- Azure Communication Services - Email (optional)

## Support

- Check the [setup guides](docs/) for detailed configuration
- Review [Swagger documentation](https://localhost:5001/swagger) for API reference
