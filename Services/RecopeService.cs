using System.Text.Json;
using DriverAI.API.Models;

namespace DriverAI.API.Services;

public class RecopeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecopeService> _logger;
    
    public RecopeService(HttpClient httpClient, ILogger<RecopeService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }
    
    public async Task<List<RecopeData>?> ObtenerDatosAsync()
    {
        try
        {
            // Nota: Ajusta la URL exacta según la documentación de Recope
            var response = await _httpClient.GetAsync("https://datosabiertos.recope.go.cr/servicio-api");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al consultar API Recope: {StatusCode}", response.StatusCode);
                return null;
            }
            
            var json = await response.Content.ReadAsStringAsync();
            
            // TODO: Ajustar el mapeo según la estructura real del JSON
            // Esto es un ejemplo genérico
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var datos = JsonSerializer.Deserialize<List<RecopeData>>(json, options);
            
            return datos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al consumir API Recope");
            return null;
        }
    }
}