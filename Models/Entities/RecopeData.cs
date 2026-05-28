using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DriverAI.API.Models.Entities;

public class RecopeData
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Producto { get; set; } = string.Empty;

    public decimal Precio { get; set; }

    public DateTime Fecha { get; set; }

    public DateTime FechaConsulta { get; set; }

    public string Origen { get; set; } = string.Empty;

    public string RawData { get; set; } = string.Empty;

    public int? UserId { get; set; }
}