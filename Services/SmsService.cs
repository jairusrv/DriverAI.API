namespace DriverAI.API.Services;

public class SmsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsService> _logger;
    
    public SmsService(IConfiguration configuration, ILogger<SmsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }
    
    public async Task<bool> SendVerificationCodeAsync(string phoneNumber, string code)
    {
        try
        {
            // TODO: Implementar con servicio real (Twilio, Vonage, etc.)
            // Por ahora, simulamos el envío y lo logueamos
            
            _logger.LogInformation($"📱 SMS enviado a {phoneNumber}: Código {code}");
            
            // Simular envío
            await Task.Delay(100);
            
            // Cuando implementes un servicio real, usa algo como:
            // var twilioClient = new TwilioClient(_configuration["Twilio:AccountSid"], _configuration["Twilio:AuthToken"]);
            // var message = await MessageResource.CreateAsync(
            //     body: $"DriverAI: Tu código de verificación es: {code}",
            //     from: new PhoneNumber(_configuration["Twilio:PhoneNumber"]),
            //     to: new PhoneNumber(phoneNumber)
            // );
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al enviar SMS a {phoneNumber}");
            return false;
        }
    }
}