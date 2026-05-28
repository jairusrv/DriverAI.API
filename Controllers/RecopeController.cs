using DriverAI.API.Config;
using DriverAI.API.Models.Entities;
using DriverAI.API.Models.Requests;
using DriverAI.API.Models.Responses;
using DriverAI.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class RecopeController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly RecopeService _recopeService;
    private readonly ILogger<RecopeController> _logger;
    
    public RecopeController(AppDbContext context, RecopeService recopeService, ILogger<RecopeController> logger)
    {
        _context = context;
        _recopeService = recopeService;
        _logger = logger;
    }
    
    [HttpGet("actualizar")]
    public async Task<IActionResult> ActualizarDatos()
    {
        var datos = await _recopeService.ObtenerDatosAsync();
        
        if (datos == null || datos.Count == 0)
        {
            return StatusCode(502, new { message = "Error al obtener datos de Recope" });
        }
        
        // Guardar en la base de datos
        foreach (var dato in datos)
        {
            dato.FechaConsulta = DateTime.UtcNow;
            _context.RecopeData.Add(dato);
        }
        
        await _context.SaveChangesAsync();
        
        return Ok(new { 
            message = $"Se guardaron {datos.Count} registros de Recope",
            data = datos 
        });
    }
    
    [HttpGet("datos")]
    public async Task<IActionResult> ObtenerDatos([FromQuery] DateTime? desde, [FromQuery] DateTime? hasta)
    {
        var query = _context.RecopeData.AsQueryable();
        
        if (desde.HasValue)
            query = query.Where(d => d.FechaConsulta >= desde.Value);
        
        if (hasta.HasValue)
            query = query.Where(d => d.FechaConsulta <= hasta.Value);
        
        var datos = await query
            .OrderByDescending(d => d.FechaConsulta)
            .Take(100)  // Limitar resultados
            .ToListAsync();
        
        return Ok(datos);
    }
}
