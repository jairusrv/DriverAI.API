using System.Text.Json;
using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Services;

public class RecopeService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecopeService> _logger;
    private readonly AppDbContext _context;

    private const string RecopeBaseUrl = "https://api.recope.go.cr/ventas/precio/consumidor";

    public RecopeService(
        HttpClient httpClient,
        ILogger<RecopeService> logger,
        AppDbContext context
    )
    {
        _httpClient = httpClient;
        _logger = logger;
        _context = context;
    }

    public async Task<List<RecopeData>?> ObtenerDatosAsync()
    {
        try
        {
            var datosApi = await ObtenerDatosDesdeApiRecopeAsync();

            if (datosApi != null && datosApi.Count > 0)
            {
                return datosApi;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error al consumir API Recope. Se intentará usar BD local."
            );
        }

        var datosBd = await ObtenerUltimosDatosDesdeBdAsync();

        if (datosBd.Count > 0)
        {
            return datosBd;
        }

        return ObtenerFallback();
    }

    private async Task<List<RecopeData>?> ObtenerDatosDesdeApiRecopeAsync()
    {
        var endpoints = new[]
        {
            $"{RecopeBaseUrl}/servicio-api",
            $"{RecopeBaseUrl}/api",
            $"{RecopeBaseUrl}/precios",
            $"{RecopeBaseUrl}/precios/combustibles"
        };

        foreach (var endpoint in endpoints)
        {
            try
            {
                var response = await _httpClient.GetAsync(endpoint);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Endpoint RECOPE respondió {StatusCode}: {Endpoint}",
                        response.StatusCode,
                        endpoint
                    );

                    continue;
                }

                var content =
                    await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(content) ||
                    content.TrimStart().StartsWith("<"))
                {
                    _logger.LogWarning(
                        "Endpoint RECOPE devolvió HTML o vacío: {Endpoint}",
                        endpoint
                    );

                    continue;
                }

                var datos =
                    IntentarParsearDatosRecope(content);

                if (datos.Count > 0)
                {
                    return datos;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "No se pudo consultar endpoint RECOPE: {Endpoint}",
                    endpoint
                );
            }
        }

        return null;
    }

    private List<RecopeData> IntentarParsearDatosRecope(
        string json
    )
    {
        var resultados =
            new List<RecopeData>();

        using var document =
            JsonDocument.Parse(json);

        var root = document.RootElement;

        if (root.ValueKind == JsonValueKind.Array)
        {
            ExtraerDesdeArray(root, resultados, json);
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Array)
            {
                ExtraerDesdeArray(data, resultados, json);
            }
            else if (root.TryGetProperty("datos", out var datos) &&
                     datos.ValueKind == JsonValueKind.Array)
            {
                ExtraerDesdeArray(datos, resultados, json);
            }
        }

        return NormalizarProductos(resultados);
    }

    private void ExtraerDesdeArray(
        JsonElement array,
        List<RecopeData> resultados,
        string rawJson
    )
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var producto =
                ObtenerString(
                    item,
                    "producto",
                    "Producto",
                    "nombre",
                    "Nombre",
                    "descripcion",
                    "Descripcion",
                    "tipo",
                    "Tipo"
                );

            var precio =
                ObtenerDecimal(
                    item,
                    "precio",
                    "Precio",
                    "monto",
                    "Monto",
                    "valor",
                    "Valor",
                    "precioTotal",
                    "PrecioTotal"
                );

            if (string.IsNullOrWhiteSpace(producto) ||
                precio == null)
            {
                continue;
            }

            resultados.Add(
                new RecopeData
                {
                    Tipo = "Combustible",
                    Producto = producto,
                    Precio = precio,
                    Fecha = DateTime.UtcNow,
                    Origen = "RECOPE_API",
                    RawData = rawJson,
                    FechaConsulta = DateTime.UtcNow
                }
            );
        }
    }

    private List<RecopeData> NormalizarProductos(
        List<RecopeData> datos
    )
    {
        var normalizados =
            new List<RecopeData>();

        foreach (var dato in datos)
        {
            var producto =
                dato.Producto?.ToLowerInvariant() ?? "";

            if (producto.Contains("super") ||
                producto.Contains("súper"))
            {
                dato.Producto = "super";
                normalizados.Add(dato);
                continue;
            }

            if (producto.Contains("regular") ||
                producto.Contains("plus 91"))
            {
                dato.Producto = "regular";
                normalizados.Add(dato);
                continue;
            }

            if (producto.Contains("diesel") ||
                producto.Contains("diésel"))
            {
                dato.Producto = "diesel";
                normalizados.Add(dato);
                continue;
            }
        }

        return normalizados
            .GroupBy(x => x.Producto)
            .Select(x => x.First())
            .ToList();
    }

    private async Task<List<RecopeData>> ObtenerUltimosDatosDesdeBdAsync()
    {
        var ultimaFecha =
            await _context.RecopeData
                .MaxAsync(x => (DateTime?)x.FechaConsulta);

        if (ultimaFecha == null)
        {
            return new List<RecopeData>();
        }

        return await _context.RecopeData
            .Where(x => x.FechaConsulta == ultimaFecha.Value)
            .OrderBy(x => x.Producto)
            .ToListAsync();
    }

    private static List<RecopeData> ObtenerFallback()
    {
        var now = DateTime.UtcNow;

        return new List<RecopeData>
        {
            new RecopeData
            {
                Tipo = "Combustible",
                Producto = "super",
                Precio = 733,
                Fecha = now,
                Origen = "FALLBACK",
                RawData = "fallback_super",
                FechaConsulta = now
            },
            new RecopeData
            {
                Tipo = "Combustible",
                Producto = "regular",
                Precio = 748,
                Fecha = now,
                Origen = "FALLBACK",
                RawData = "fallback_regular",
                FechaConsulta = now
            },
            new RecopeData
            {
                Tipo = "Combustible",
                Producto = "diesel",
                Precio = 716,
                Fecha = now,
                Origen = "FALLBACK",
                RawData = "fallback_diesel",
                FechaConsulta = now
            }
        };
    }

    private static string? ObtenerString(
        JsonElement item,
        params string[] names
    )
    {
        foreach (var name in names)
        {
            if (item.TryGetProperty(name, out var value) &&
                value.ValueKind != JsonValueKind.Null)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static decimal? ObtenerDecimal(
        JsonElement item,
        params string[] names
    )
    {
        foreach (var name in names)
        {
            if (!item.TryGetProperty(name, out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.Number &&
                value.TryGetDecimal(out var decimalValue))
            {
                return decimalValue;
            }

            if (decimal.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}