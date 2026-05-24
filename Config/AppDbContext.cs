using DriverAI.API.Models;

using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Config;

public class AppDbContext
    : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options
    ) : base(options)
    {
    }

    public DbSet<User> Users =>
        Set<User>();
}