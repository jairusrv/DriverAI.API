using System.Net;
using System.Net.Mail;

namespace DriverAI.API.Services;

public class EmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    
    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<bool> SendVerificationCodeAsync(string email, string code)
    {
        try
        {
            // Configuración desde appsettings.json o variables de entorno
            var smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
            var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
            var smtpUser = _configuration["Email:SmtpUser"] ?? "";
            var smtpPassword = _configuration["Email:SmtpPassword"] ?? "";
            var fromEmail = _configuration["Email:FromEmail"] ?? smtpUser;
            
            using var client = new SmtpClient(smtpServer, smtpPort);
            client.EnableSsl = true;
            client.Credentials = new NetworkCredential(smtpUser, smtpPassword);
            
            var subject = "DriverAI - Código de verificación";
            var body = $@"
                <html>
                <body>
                    <h2>DriverAI - Verificación de cuenta</h2>
                    <p>Gracias por registrarte en DriverAI.</p>
                    <p>Tu código de verificación es:</p>
                    <h1 style='color: #0066cc;'>{code}</h1>
                    <p>Este código expirará en 15 minutos.</p>
                    <p>Si no solicitaste este código, ignora este mensaje.</p>
                    <br/>
                    <p>Saludos,<br/>Equipo DriverAI</p>
                </body>
                </html>
            ";
            
            var mailMessage = new MailMessage(fromEmail, email, subject, body);
            mailMessage.IsBodyHtml = true;
            
            await client.SendMailAsync(mailMessage);
            _logger.LogInformation($"Correo de verificación enviado a {email}");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al enviar email a {email}");
            return false;
        }
    }
}