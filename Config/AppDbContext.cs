using DriverAI.API.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DriverAI.API.Config;

public class AppDbContext : DbContext
{
    public AppDbContext(
        DbContextOptions<AppDbContext> options
    ) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<VerificationCode> VerificationCodes => Set<VerificationCode>();

    public DbSet<RecopeData> RecopeData => Set<RecopeData>();

    public DbSet<UserSettings> UserSettings => Set<UserSettings>();

    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<RideHistory> RideHistory => Set<RideHistory>();

    public DbSet<ErrorLog> ErrorLogs => Set<ErrorLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.PhoneNumber);

        modelBuilder.Entity<UserSettings>()
            .HasIndex(x => x.UserId)
            .IsUnique();

        modelBuilder.Entity<UserSettings>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserSubscription>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RideHistory>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Imei)
            .IsUnique();

        modelBuilder.Entity<VerificationCode>()
            .HasKey(x => x.Id);

        modelBuilder.Entity<VerificationCode>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}