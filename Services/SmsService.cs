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
            // Validar formato de Costa Rica: +506 + 8 dígitos
            if (!phoneNumber.StartsWith("+506") || phoneNumber.Length != 12)
            {
                _logger.LogError($"Número inválido para Costa Rica: {phoneNumber}");
                return false;
            }
            
            var localNumber = phoneNumber.Substring(4); // Extrae solo los 8 dígitos
            
            _logger.LogInformation($"📱 Enviando SMS a Costa Rica (+506 {localNumber}): Código {code}");
            
            // ==========================================
            // TODO: Implementar con servicio real de SMS
            // Opciones recomendadas para Costa Rica:
            // 1. Twilio (soporta números de Costa Rica)
            // 2. Vonage (ex-Nexmo)
            // 3. MensajeroCR (servicio local)
            // ==========================================
            
            // Simular envío (remover en producción)
            await Task.Delay(100);
            
            // Ejemplo con Twilio (descomentar cuando tengas credenciales):
            /*
            TwilioClient.Init(
                _configuration["Twilio:AccountSid"],
                _configuration["Twilio:AuthToken"]
            );
            
            var message = await MessageResource.CreateAsync(
                body: $"DriverAI: Tu código de verificación es: {code}. Válido por 15 minutos.",
                from: new PhoneNumber(_configuration["Twilio:PhoneNumber"]),
                to: new PhoneNumber(phoneNumber)
            );
            
            if (message.ErrorCode != null)
            {
                _logger.LogError($"Error Twilio: {message.ErrorMessage}");
                return false;
            }
            */
            
            _logger.LogInformation($"✅ SMS enviado a +506{localNumber}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error al enviar SMS a {phoneNumber}");
            return false;
        }
    }
}