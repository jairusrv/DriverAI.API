using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace DriverAI.API.Models;

public class RecopeData
{
    [Key]
    public int Id { get; set; }
    
    public string? Tipo { get; set; }          // Combustible, precio, etc.
    public string? Producto { get; set; }
    public decimal? Precio { get; set; }
    public DateTime? Fecha { get; set; }
    public string? Origen { get; set; }
    
    // Datos adicionales que vengan del API
    public string? RawData { get; set; }       // Guardar JSON original
    
    public DateTime FechaConsulta { get; set; } = DateTime.UtcNow;
    
    // Relación con usuario (si quieres guardar quién consultó)
    public int? UserId { get; set; }
    public User? User { get; set; }
}