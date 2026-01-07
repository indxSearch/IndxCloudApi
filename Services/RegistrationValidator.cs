using Microsoft.Extensions.Options;

namespace IndxCloudApi.Services;

/// <summary>
/// Service for validating user registration based on configured policies
/// </summary>
public class RegistrationValidator
{
    private readonly RegistrationOptions _options;
    private readonly ILogger<RegistrationValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the RegistrationValidator class
    /// </summary>
    /// <param name="options">Registration configuration options</param>
    /// <param name="logger">Logger instance</param>
    public RegistrationValidator(
        IOptions<RegistrationOptions> options,
        ILogger<RegistrationValidator> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Validates if registration is allowed based on current mode
    /// </summary>
    /// <returns>ValidationResult with success status and message</returns>
    public RegistrationValidationResult ValidateRegistrationAllowed()
    {
        if (_options.Mode == RegistrationMode.Closed)
        {
            _logger.LogWarning("Registration attempt blocked - registration is closed");
            return RegistrationValidationResult.Failure(
                "Registration is currently closed. Please contact the administrator for access.");
        }

        return RegistrationValidationResult.Success();
    }

    /// <summary>
    /// Validates if an email address is allowed to register
    /// </summary>
    /// <param name="email">Email address to validate</param>
    /// <returns>ValidationResult with success status and message</returns>
    public RegistrationValidationResult ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RegistrationValidationResult.Failure("Email is required.");
        }

        // In Open mode, all emails are allowed
        if (_options.Mode == RegistrationMode.Open)
        {
            return RegistrationValidationResult.Success();
        }

        // In Closed mode, registration is not allowed
        if (_options.Mode == RegistrationMode.Closed)
        {
            return RegistrationValidationResult.Failure(
                "Registration is currently closed. Please contact the administrator for access.");
        }

        // In EmailDomain mode, check if domain is allowed
        if (_options.Mode == RegistrationMode.EmailDomain)
        {
            if (_options.AllowedDomains == null || _options.AllowedDomains.Count == 0)
            {
                _logger.LogWarning("EmailDomain mode configured but no allowed domains specified. Treating as Open mode.");
                return RegistrationValidationResult.Success();
            }

            var emailDomain = GetEmailDomain(email);
            if (string.IsNullOrEmpty(emailDomain))
            {
                return RegistrationValidationResult.Failure("Invalid email format.");
            }

            var isAllowed = _options.AllowedDomains.Any(domain =>
                emailDomain.Equals(domain, StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                var allowedDomainsText = string.Join(", ", _options.AllowedDomains);
                _logger.LogWarning("Registration blocked for email {Email} - domain not in allowed list", email);
                return RegistrationValidationResult.Failure(
                    $"Registration is restricted to the following domains: {allowedDomainsText}");
            }
        }

        return RegistrationValidationResult.Success();
    }

    /// <summary>
    /// Gets the current registration mode
    /// </summary>
    public RegistrationMode GetCurrentMode() => _options.Mode;

    /// <summary>
    /// Gets a user-friendly message about current registration restrictions
    /// </summary>
    public string GetRegistrationInfoMessage()
    {
        return _options.Mode switch
        {
            RegistrationMode.Closed => "Registration is currently closed. Please contact the administrator for access.",
            RegistrationMode.EmailDomain when _options.AllowedDomains?.Count > 0 =>
                $"Registration is restricted to: {string.Join(", ", _options.AllowedDomains)}",
            RegistrationMode.Open => "Open registration - anyone can create an account.",
            _ => "Open registration - anyone can create an account."
        };
    }

    private static string? GetEmailDomain(string email)
    {
        var atIndex = email.LastIndexOf('@');
        if (atIndex < 0 || atIndex == email.Length - 1)
        {
            return null;
        }

        return email.Substring(atIndex + 1);
    }
}

/// <summary>
/// Result of registration validation
/// </summary>
public class RegistrationValidationResult
{
    /// <summary>
    /// Gets whether the validation passed
    /// </summary>
    public bool IsValid { get; init; }
    /// <summary>
    /// Gets the error message if validation failed, or null if successful
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <returns>A successful validation result</returns>
    public static RegistrationValidationResult Success() =>
        new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result with an error message
    /// </summary>
    /// <param name="errorMessage">The error message describing why validation failed</param>
    /// <returns>A failed validation result</returns>
    public static RegistrationValidationResult Failure(string errorMessage) =>
        new() { IsValid = false, ErrorMessage = errorMessage };
}
