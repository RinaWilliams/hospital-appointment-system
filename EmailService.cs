namespace HospitalApi.Services
{
 
    public class EmailService
    {
        private readonly ILogger<EmailService> _log;

        public EmailService(ILogger<EmailService> log)
        {
            _log = log;
        }

        public Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            _log.LogInformation(
                "\n==================== EMAIL (console only) ====================\n" +
                "To:      {To}\n" +
                "Subject: {Subject}\n" +
                "Body:\n{Body}\n" +
                "================================================================",
                toEmail, subject, htmlBody);

           
            return Task.CompletedTask;
        }
    }
}