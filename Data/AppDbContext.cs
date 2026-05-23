using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;
using VictusLounge.Models;

namespace VictusLounge.Data;

public class AppDbContext : DbContext
{
    public static string ConnectionString { get; } = LoadConnectionString();

    public DbSet<User> Users => Set<User>();
    public DbSet<Computer> Computers => Set<Computer>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<GameSession> GameSessions => Set<GameSession>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
        }
    }

    private static string LoadConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("VICTUS_DB");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        const string fallback = "Server=localhost;Database=victus_lounge;Trusted_Connection=True;TrustServerCertificate=True";
        var appSettingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(appSettingsPath))
        {
            appSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
        }

        if (!File.Exists(appSettingsPath))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                && connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection)
                && !string.IsNullOrWhiteSpace(defaultConnection.GetString()))
            {
                return defaultConnection.GetString()!;
            }
        }
        catch
        {
            return fallback;
        }

        return fallback;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureUsers(modelBuilder);
        ConfigureComputers(modelBuilder);
        ConfigureTariffs(modelBuilder);
        ConfigureBookings(modelBuilder);
        ConfigureGameSessions(modelBuilder);
        ConfigurePayments(modelBuilder);
        ConfigureShifts(modelBuilder);
        ConfigurePromoCodes(modelBuilder);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(user => user.Id).ValueGeneratedNever();
            entity.Property(user => user.FullName).HasMaxLength(120).IsRequired();
            entity.Property(user => user.Login).HasMaxLength(50).IsRequired();
            entity.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(user => user.Role).HasMaxLength(30).IsRequired();
            entity.Property(user => user.LoyaltyTier).HasMaxLength(30).IsRequired();
            entity.Property(user => user.Balance).HasPrecision(10, 2);
            entity.HasIndex(user => user.Login).IsUnique();
        });
    }

    private static void ConfigureComputers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Computer>(entity =>
        {
            entity.Property(computer => computer.Id).ValueGeneratedNever();
            entity.Property(computer => computer.Name).HasMaxLength(40).IsRequired();
            entity.Property(computer => computer.Zone).HasMaxLength(50).IsRequired();
            entity.Property(computer => computer.Specs).HasMaxLength(300).IsRequired();
            entity.Property(computer => computer.Status).HasMaxLength(30).IsRequired();
            entity.Property(computer => computer.HourPrice).HasPrecision(10, 2);
            entity.HasIndex(computer => computer.Name).IsUnique();
        });
    }

    private static void ConfigureTariffs(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tariff>(entity =>
        {
            entity.Property(tariff => tariff.Id).ValueGeneratedNever();
            entity.Property(tariff => tariff.Name).HasMaxLength(80).IsRequired();
            entity.Property(tariff => tariff.PricePerHour).HasPrecision(10, 2);
            entity.Property(tariff => tariff.Description).HasMaxLength(300).IsRequired();
        });
    }

    private static void ConfigureBookings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.Property(booking => booking.Id).ValueGeneratedNever();
            entity.Property(booking => booking.Status).HasMaxLength(30).IsRequired();
            entity.Property(booking => booking.Package).HasMaxLength(30).IsRequired();
            entity.Property(booking => booking.TotalPrice).HasPrecision(10, 2);
            entity.HasOne(booking => booking.User)
                .WithMany()
                .HasForeignKey(booking => booking.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(booking => booking.Computer)
                .WithMany()
                .HasForeignKey(booking => booking.ComputerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureGameSessions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GameSession>(entity =>
        {
            entity.Property(session => session.Id).ValueGeneratedNever();
            entity.Property(session => session.TotalPrice).HasPrecision(10, 2);
            entity.Property(session => session.Status).HasMaxLength(30).IsRequired();
            entity.HasOne(session => session.User)
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(session => session.Computer)
                .WithMany()
                .HasForeignKey(session => session.ComputerId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigurePayments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(payment => payment.Id).ValueGeneratedNever();
            entity.Property(payment => payment.Amount).HasPrecision(10, 2);
            entity.Property(payment => payment.PaymentType).HasMaxLength(50).IsRequired();
            entity.Property(payment => payment.Comment).HasMaxLength(300).IsRequired();
            entity.HasOne(payment => payment.User)
                .WithMany()
                .HasForeignKey(payment => payment.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureShifts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Shift>(entity =>
        {
            entity.Property(shift => shift.Id).ValueGeneratedNever();
            entity.Property(shift => shift.EmployeeName).HasMaxLength(120).IsRequired();
            entity.Property(shift => shift.CashTotal).HasPrecision(10, 2);
        });
    }

    private static void ConfigurePromoCodes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PromoCode>(entity =>
        {
            entity.Property(promoCode => promoCode.Id).ValueGeneratedNever();
            entity.Property(promoCode => promoCode.Code).HasMaxLength(50).IsRequired();
            entity.Property(promoCode => promoCode.BookingDiscountRate).HasPrecision(5, 2);
            entity.Property(promoCode => promoCode.TopupBonusRate).HasPrecision(5, 2);
            entity.Property(promoCode => promoCode.MinTopupAmount).HasPrecision(10, 2);
            entity.HasIndex(promoCode => promoCode.Code).IsUnique();
        });
    }
}
