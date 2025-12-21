# Indx Cloud API

A production-ready starter template for building a search service with **Indx Search**.

This template provides a complete Blazor Server application with REST API, user authentication, and everything you need to deploy a multi-user search service.

## What's Included

**Core Features:**
- Blazor Server UI with interactive web interface
- REST API with JWT authentication
- User management and authentication (local accounts + OAuth)
- API key generation for programmatic access
- SQLite databases (ready for production alternatives)
- Swagger/OpenAPI documentation

**Authentication:**
- Local accounts (username/password)
- Microsoft OAuth (Azure AD)
- Google OAuth
- Email confirmation and password reset

**Developer Experience:**
- Works out of the box with minimal configuration
- User Secrets for local development
- Environment variables for production
- Comprehensive setup guides

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A code editor (VS Code, Visual Studio, Rider)

### Get Running in 3 Steps

1. **Clone and restore**
   ```bash
   git clone <repository-url>
   cd IndxCloudApi
   dotnet restore
   ```

2. **Run the application**
   ```bash
   dotnet run
   ```

3. **Open your browser**
   - Navigate to `https://localhost:5001`
   - Register an account at `/Account/Register`
   - Start using the search API

That's it! The template uses:
- SQLite (auto-created in `./IndxData/`)
- Console email mode (emails logged to console)
- Default JWT configuration (change in production)

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

### Development (User Secrets)

For local development, use User Secrets to keep sensitive data out of your repository:

```bash
# Required: Set a secure JWT key (minimum 32 characters)
dotnet user-secrets set "Jwt:Key" "your-secret-key-minimum-32-characters-here"

# Optional: Configure OAuth providers
dotnet user-secrets set "Authentication:Microsoft:ClientId" "your-client-id"
dotnet user-secrets set "Authentication:Microsoft:ClientSecret" "your-client-secret"

# Optional: Configure production email service
dotnet user-secrets set "Email:Provider" "AzureCommunicationServices"
dotnet user-secrets set "Email:AzureCommunicationServices:ConnectionString" "your-connection-string"
```

See detailed guides:
- [OAuth Setup](docs/OAUTH_SETUP.md) - Microsoft and Google authentication
- [Email Setup](docs/EMAIL_SETUP.md) - Azure Communication Services or custom providers

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

**Password Requirements:**
- Minimum 8 characters
- Uppercase, lowercase, digit, and special character required

**JWT Tokens:**
- Configurable expiration (30-365 days)
- Secure key required (minimum 32 characters)

**Best Practices:**
- Never commit secrets to git (use `.gitignore`)
- Use User Secrets for development
- Use Azure Key Vault or environment variables for production
- Enable HTTPS in production (included by default)
- Rotate secrets regularly

## Deployment

### Azure App Service

1. Create an App Service in Azure Portal
2. Configure Application Settings with your secrets (use double underscore notation)
3. Deploy:
   ```bash
   dotnet publish -c Release
   # Deploy using Azure CLI, Visual Studio, or GitHub Actions
   ```

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
