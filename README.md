# Indx Cloud API

A **ready-to-run** starter template for building a search service with **Indx Search**.

This template provides a complete Blazor Server application with HTTP API, user authentication, and everything you need to deploy a multi-user search service. Try it first, configure it later.

## What's Included

**Core Features:**
- Blazor Server UI with interactive web interface
- HTTP API with JWT authentication
- User management and authentication (local accounts + OAuth)
- API key generation for programmatic access
- SQLite databases (ready for production alternatives)
- Swagger documentation

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
 This template works immediately without any secrets or external services.

1. **Clone and run**
   ```bash
   git clone https://github.com/indxSearch/IndxCloudApi
   cd IndxCloudApi
   dotnet run
   ```

2. **Open your browser**
   - Navigate to `https://localhost:5001`
   - Register an account at `/Account/Register`
   - Start using the search API

**That's it.** The template works out-of-the-box with:
- ✓ Local user accounts (username/password)
- ✓ SQLite databases (auto-created in `./IndxData/`)
- ✓ Console email mode (emails logged to console, no SMTP needed)
- ✓ Default JWT configuration (secure for testing, customize for production)

## Related Projects

**IndxCloudApi** is part of the Indx Search ecosystem. These companion tools make it even easier to work with:

### **IndxCloudLoader** (C#)
A command-line tool for loading data into your IndxCloudApi instance. Perfect for batch importing documents, testing with sample data, or automating dataset creation.

- **Repository:** [IndxCloudLoader](https://github.com/indxSearch/IndxCloudLoader)
- **Use cases:**
  - Bulk load JSON documents
  - Automated dataset setup for testing

### **indx-intrface** (React)
Ready-to-use React UI components and a complete demo application for building search interfaces that connect to IndxCloudApi.

- **Repository:** [indx-intrface](https://github.com/indxSearch/indx-intrface)
- **Features:**
  - Pre-built search components
  - Working demo application
  - Easy integration with your React projects

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
├── Controllers/          # HTTP API controllers (search, login)
├── Data/                 # Database context and models
├── Services/             # Email services (Console, Azure Communication)
├── Shared/               # Layout and shared UI components
├── docs/                 # Detailed setup guides
└── IndxData/             # SQLite databases (identity.db, indx.db)
```

## Configuration

**The template works without any configuration.** All settings below are optional and can be configured when you're ready for production or want additional features.

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

## License Configuration

**Indx Search** is completely free but includes a **100,000 document limit** by default. To remove this limit, you need to register as a developer.

### Document Limits

- **No License**: 100,000 documents maximum
- **Extended License (Free)**: Removes limitation - requires registered account at indx.co
- **Company License (Paid)**: Same capacity as extended license, but includes SLA and support

### Getting a License

**Option 1: Free Extended License (Recommended)**
- Register an account at [https://indx.co](https://indx.co)
- Request your free extended license - no restrictions
- Use for any purpose: development, testing, or production
- Filename: `indx-developer.license`

**Option 2: Paid Company License**
- For organizations requiring SLA and technical support
- Includes service level guarantees and priority support
- Same document capacity as extended license
- Contact [https://indx.co](https://indx.co) for pricing
- Filename: `indx-yourcompany.license` (e.g., `indx-google.license`, `indx-apple.license`)

### License File Placement

The system automatically detects license files in the `./IndxData/` directory:

```bash
IndxCloudApi/
└── IndxData/
    ├── identity.db
    ├── indx.db
    └── indx-developer.license    # Place your license file here
```

**Steps:**
1. Receive your license file (`.license` extension)
2. Place it in `./IndxData/` directory
3. Restart the application

The system will automatically detect and use any `.license` file in this directory. If multiple license files exist, it prioritizes `indx-company.license` over `indx-developer.license`.

### Custom License Path (Optional)

If you need to store your license file elsewhere, configure the path in `appsettings.json`:

```json
{
  "Indx": {
    "LicenseFile": "/path/to/your/license.file"
  }
}
```

Or use environment variables for production:

```bash
# Local development (User Secrets)
dotnet user-secrets set "Indx:LicenseFile" "/path/to/license.file"

# Production (Environment Variable)
export Indx__LicenseFile="/path/to/license.file"

# Azure App Service (Application Settings)
Indx__LicenseFile = "/path/to/license.file"
```

### Verifying Your License

When the application starts, it logs the license status:

```
✓ Search system initialized at: ./IndxData/indx.db
✓ Using license file: ./IndxData/indx-developer.license
```

Or if no license is found:

```
ℹ No license file found - running with 100,000 document limit
  Place your license file (.license) in ./IndxData/ to remove the limit
```

### Azure Deployment

**For Azure App Service**, license files work the same way:

1. **Option A: Include in deployment (Recommended)**
   - Place license file in `./IndxData/` before publishing
   - Deploy normally - the file will be included
   - License file deploys with your application

2. **Option B: Upload after deployment**
   - Use FTP or Azure Portal App Service Editor (Kudu)
   - Upload to `D:\home\site\wwwroot\IndxData\`
   - Useful if you need to update the license without redeploying

**Important**: The `./IndxData/` directory persists across restarts on Azure App Service. License files placed there remain available even after redeployment.

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

**SQLite:**
- `./IndxData/identity.db` - User accounts and authentication through ASP.NET Core Identity
- `./IndxData/indx.db` - Application configuration and JSON data

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

**Application Settings** (Environment variables → App settings or Configuration → Application settings):
```
Jwt__Key = "your-custom-production-key-32-characters-minimum"
Authentication__Microsoft__ClientId = "your-client-id"
Authentication__Microsoft__ClientSecret = "your-client-secret"
Email__Provider = "AzureCommunicationServices"
Email__AzureCommunicationServices__ConnectionString = "your-acs-connection"
Registration__Mode = "EmailDomain"
Registration__AllowedDomains__0 = "yourcompany.com"
Registration__AllowedDomains__1 = "partner.com"
```

**Note:** In the Azure Portal, these settings may be found under:
- **"Environment variables"** → **"App settings"** (newer portal UI), OR
- **"Configuration"** → **"Application settings"** (classic portal UI)

Click **"+ Add"** to add each setting. Azure will automatically restart your app when you save changes.

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
