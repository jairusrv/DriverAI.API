using DriverAI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Config;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) 
        : base(options) { }
    
    public DbSet<User> Users { get; set; }
    public DbSet<RecopeData> RecopeData { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Índice único para email
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        
        // Índice para búsquedas por fecha
        modelBuilder.Entity<RecopeData>()
            .HasIndex(r => r.Fecha);
    }
}