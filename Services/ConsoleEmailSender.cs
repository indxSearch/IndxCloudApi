using Microsoft.AspNetCore.Identity.UI.Services;

namespace IndxCloudApi.Services
{
    /// <summary>
    /// Email sender that logs to console - for development/testing
    /// </summary>
    public class ConsoleEmailSender : IEmailSender
    {
        private readonly ILogger<ConsoleEmailSender> _logger;

        public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogInformation("=== EMAIL (Console Mode) ===");
            _logger.LogInformation("To: {Email}", email);
            _logger.LogInformation("Subject: {Subject}", subject);
            _logger.LogInformation("Body: {Body}", htmlMessage);
            _logger.LogInformation("============================");

            // In console mode, we just log - no actual email sent
            return Task.CompletedTask;
        }
    }
}
