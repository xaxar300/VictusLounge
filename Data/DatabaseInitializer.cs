using Microsoft.EntityFrameworkCore;
using VictusLounge.Helpers;
using VictusLounge.Models;

namespace VictusLounge.Data;

public static class DatabaseInitializer
{
    private static DateTime Today => DateTime.Today;

    public static void Initialize()
    {
        using var dbContext = new AppDbContext();
        dbContext.Database.EnsureCreated();

        Seed(dbContext);
    }

    private static void Seed(AppDbContext dbContext)
    {
        UpsertUsers(dbContext);
        MigratePlainTextPasswords(dbContext);
        UpsertComputers(dbContext);
        UpsertTariffs(dbContext);
        UpsertBookings(dbContext);
        UpsertGameSessions(dbContext);
        UpsertPayments(dbContext);
        UpsertShifts(dbContext);

        dbContext.SaveChanges();
    }

    private static void UpsertUsers(AppDbContext dbContext)
    {
        var users = new[]
        {
            new User { Id = 1, FullName = "Victor Romanov", Login = "owner", PasswordHash = PasswordHasher.HashPassword("owner123"), Role = "Owner", Balance = 0m },
            new User { Id = 2, FullName = "Anna Smirnova", Login = "admin1", PasswordHash = PasswordHasher.HashPassword("admin123"), Role = "Admin", Balance = 0m },
            new User { Id = 3, FullName = "Dmitry Volkov", Login = "admin2", PasswordHash = PasswordHasher.HashPassword("admin456"), Role = "Admin", Balance = 0m },
            new User { Id = 4, FullName = "Ivan Petrov", Login = "client1", PasswordHash = PasswordHasher.HashPassword("client123"), Role = "Client", Balance = 42m },
            new User { Id = 5, FullName = "Maria Sokolova", Login = "client2", PasswordHash = PasswordHasher.HashPassword("client234"), Role = "Client", Balance = 18m },
            new User { Id = 6, FullName = "Pavel Morozov", Login = "client3", PasswordHash = PasswordHasher.HashPassword("client345"), Role = "Client", Balance = 65m },
            new User { Id = 7, FullName = "Elena Kuznetsova", Login = "client4", PasswordHash = PasswordHasher.HashPassword("client456"), Role = "Client", Balance = 27m },
            new User { Id = 8, FullName = "Nikita Zakharov", Login = "client5", PasswordHash = PasswordHasher.HashPassword("client567"), Role = "Client", Balance = 10m },
            new User { Id = 9, FullName = "Olga Belova", Login = "client6", PasswordHash = PasswordHasher.HashPassword("client678"), Role = "Client", Balance = 33m },
            new User { Id = 10, FullName = "Sergey Antonov", Login = "client7", PasswordHash = PasswordHasher.HashPassword("client789"), Role = "Client", Balance = 75m }
        };

        InsertMissingRange(dbContext, users, user => user.Id);
    }

    private static void MigratePlainTextPasswords(AppDbContext dbContext)
    {
        foreach (var user in dbContext.Users.Where(user => !PasswordHasher.IsHashed(user.PasswordHash)))
        {
            user.PasswordHash = PasswordHasher.HashPassword(user.PasswordHash);
        }
    }

    private static void UpsertComputers(AppDbContext dbContext)
    {
        var computers = new[]
        {
            new Computer { Id = 1, Name = "STD-01", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 2, Name = "STD-02", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Busy, HourPrice = 8m },
            new Computer { Id = 3, Name = "STD-03", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 4, Name = "STD-04", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Reserved, HourPrice = 8m },
            new Computer { Id = 5, Name = "STD-05", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 6, Name = "STD-06", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 7, Name = "STD-07", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Service, HourPrice = 8m },
            new Computer { Id = 8, Name = "STD-08", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 9, Name = "STD-09", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 10, Name = "STD-10", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Busy, HourPrice = 8m },
            new Computer { Id = 11, Name = "STD-11", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 12, Name = "STD-12", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Reserved, HourPrice = 8m },
            new Computer { Id = 13, Name = "STD-13", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 14, Name = "STD-14", Zone = "Standard", Specs = "Intel Core i5-13400F, RTX 4060, 16 GB RAM, 144 Hz", Status = PcStatuses.Free, HourPrice = 8m },
            new Computer { Id = 15, Name = "VIP-01", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Free, HourPrice = 14m },
            new Computer { Id = 16, Name = "VIP-02", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Busy, HourPrice = 14m },
            new Computer { Id = 17, Name = "VIP-03", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Reserved, HourPrice = 14m },
            new Computer { Id = 18, Name = "VIP-04", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Free, HourPrice = 14m },
            new Computer { Id = 19, Name = "VIP-05", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Free, HourPrice = 14m },
            new Computer { Id = 20, Name = "VIP-06", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Service, HourPrice = 14m },
            new Computer { Id = 21, Name = "VIP-07", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Free, HourPrice = 14m },
            new Computer { Id = 22, Name = "VIP-08", Zone = "VIP", Specs = "Ryzen 5 7600X, RTX 4070 Super, 32 GB RAM, 180 Hz", Status = PcStatuses.Reserved, HourPrice = 14m },
            new Computer { Id = 23, Name = "BA-01", Zone = "Bootcamp", Specs = "Intel Core i7-13700KF, RTX 4070 Ti, 32 GB RAM, 240 Hz", Status = PcStatuses.Busy, HourPrice = 50m },
            new Computer { Id = 24, Name = "BA-02", Zone = "Bootcamp", Specs = "Intel Core i7-13700KF, RTX 4070 Ti, 32 GB RAM, 240 Hz", Status = PcStatuses.Free, HourPrice = 50m },
            new Computer { Id = 25, Name = "BA-03", Zone = "Bootcamp", Specs = "Intel Core i7-13700KF, RTX 4070 Ti, 32 GB RAM, 240 Hz", Status = PcStatuses.Free, HourPrice = 50m },
            new Computer { Id = 26, Name = "BA-04", Zone = "Bootcamp", Specs = "Intel Core i7-13700KF, RTX 4070 Ti, 32 GB RAM, 240 Hz", Status = PcStatuses.Reserved, HourPrice = 50m },
            new Computer { Id = 27, Name = "BA-05", Zone = "Bootcamp", Specs = "Intel Core i7-13700KF, RTX 4070 Ti, 32 GB RAM, 240 Hz", Status = PcStatuses.Free, HourPrice = 50m },
            new Computer { Id = 28, Name = "RV-01", Zone = "Royal VIP", Specs = "Ryzen 7 7800X3D, RTX 4080 Super, 64 GB RAM, 240 Hz", Status = PcStatuses.Free, HourPrice = 24m },
            new Computer { Id = 29, Name = "RV-02", Zone = "Royal VIP", Specs = "Ryzen 7 7800X3D, RTX 4080 Super, 64 GB RAM, 240 Hz", Status = PcStatuses.Free, HourPrice = 24m },
            new Computer { Id = 30, Name = "RV-03", Zone = "Royal VIP", Specs = "Ryzen 7 7800X3D, RTX 4080 Super, 64 GB RAM, 240 Hz", Status = PcStatuses.Reserved, HourPrice = 24m },
            new Computer { Id = 31, Name = "RV-04", Zone = "Royal VIP", Specs = "Ryzen 7 7800X3D, RTX 4080 Super, 64 GB RAM, 240 Hz", Status = PcStatuses.Service, HourPrice = 24m },
            new Computer { Id = 32, Name = "RV-05", Zone = "Royal VIP", Specs = "Ryzen 7 7800X3D, RTX 4080 Super, 64 GB RAM, 240 Hz", Status = PcStatuses.Free, HourPrice = 24m }
        };

        UpsertRange(dbContext, computers, computer => computer.Id, (target, source) =>
        {
            target.Name = source.Name;
            target.Zone = source.Zone;
            target.Specs = source.Specs;
        });
    }

    private static void UpsertTariffs(AppDbContext dbContext)
    {
        var tariffs = new[]
        {
            new Tariff { Id = 1, Name = "Standard Hour", PricePerHour = 8m, Description = "Standard hall hourly tariff.", IsActive = true },
            new Tariff { Id = 2, Name = "VIP Hour", PricePerHour = 14m, Description = "VIP zone hourly tariff.", IsActive = true },
            new Tariff { Id = 3, Name = "Royal VIP Hour", PricePerHour = 24m, Description = "Royal VIP hourly tariff.", IsActive = true },
            new Tariff { Id = 4, Name = "Bootcamp Room", PricePerHour = 50m, Description = "Team bootcamp room hourly tariff.", IsActive = true },
            new Tariff { Id = 5, Name = "Night Pack", PricePerHour = 6m, Description = "Night package with discount.", IsActive = true },
            new Tariff { Id = 6, Name = "Morning Pack", PricePerHour = 7m, Description = "Morning package with discount.", IsActive = true },
            new Tariff { Id = 7, Name = "Student", PricePerHour = 6m, Description = "Student discount tariff.", IsActive = true },
            new Tariff { Id = 8, Name = "Weekend", PricePerHour = 10m, Description = "Weekend standard tariff.", IsActive = true },
            new Tariff { Id = 9, Name = "Tournament", PricePerHour = 12m, Description = "Tournament participant tariff.", IsActive = true },
            new Tariff { Id = 10, Name = "Inactive Promo", PricePerHour = 5m, Description = "Archived promo tariff.", IsActive = false }
        };

        UpsertRange(dbContext, tariffs, tariff => tariff.Id, (target, source) =>
        {
            target.Name = source.Name;
            target.Description = source.Description;
            target.IsActive = source.IsActive;
        });
    }

    private static void UpsertBookings(AppDbContext dbContext)
    {
        var bookings = new[]
        {
            new Booking { Id = 1, UserId = 4, ComputerId = 17, StartTime = Today.AddHours(18), EndTime = Today.AddHours(20), Status = BookingStatuses.Confirmed, CreatedAt = Today.AddHours(10) },
            new Booking { Id = 2, UserId = 5, ComputerId = 12, StartTime = Today.AddHours(19), EndTime = Today.AddHours(21), Status = BookingStatuses.Confirmed, CreatedAt = Today.AddHours(11) },
            new Booking { Id = 3, UserId = 6, ComputerId = 26, StartTime = Today.AddHours(20), EndTime = Today.AddHours(22), Status = BookingStatuses.PendingPayment, CreatedAt = Today.AddHours(12) },
            new Booking { Id = 4, UserId = 7, ComputerId = 30, StartTime = Today.AddHours(21), EndTime = Today.AddHours(23), Status = BookingStatuses.Confirmed, CreatedAt = Today.AddHours(13) },
            new Booking { Id = 5, UserId = 8, ComputerId = 4, StartTime = Today.AddHours(17), EndTime = Today.AddHours(19), Status = BookingStatuses.Confirmed, CreatedAt = Today.AddHours(9) },
            new Booking { Id = 6, UserId = 9, ComputerId = 22, StartTime = Today.AddHours(22), EndTime = Today.AddDays(1).AddHours(2), Status = BookingStatuses.PendingPayment, CreatedAt = Today.AddHours(14) },
            new Booking { Id = 7, UserId = 10, ComputerId = 24, StartTime = Today.AddDays(1).AddHours(12), EndTime = Today.AddDays(1).AddHours(14), Status = BookingStatuses.Confirmed, CreatedAt = Today.AddHours(15) },
            new Booking { Id = 8, UserId = 4, ComputerId = 2, StartTime = Today.AddDays(1).AddHours(16), EndTime = Today.AddDays(1).AddHours(18), Status = BookingStatuses.PendingPayment, CreatedAt = Today.AddHours(16) },
            new Booking { Id = 9, UserId = 5, ComputerId = 7, StartTime = Today.AddDays(1).AddHours(18), EndTime = Today.AddDays(1).AddHours(20), Status = BookingStatuses.Cancelled, CreatedAt = Today.AddHours(17) },
            new Booking { Id = 10, UserId = 6, ComputerId = 15, StartTime = Today.AddDays(2).AddHours(18), EndTime = Today.AddDays(2).AddHours(21), Status = BookingStatuses.Confirmed, CreatedAt = Today.AddHours(18) }
        };

        InsertMissingRange(dbContext, bookings, booking => booking.Id);
    }

    private static void UpsertGameSessions(AppDbContext dbContext)
    {
        var sessions = new[]
        {
            new GameSession { Id = 1, UserId = 4, ComputerId = 2, StartTime = Today.AddHours(16), EndTime = null, TotalPrice = 16m, Status = SessionStatuses.Active },
            new GameSession { Id = 2, UserId = 5, ComputerId = 10, StartTime = Today.AddHours(17), EndTime = null, TotalPrice = 24m, Status = SessionStatuses.AwaitingPayment },
            new GameSession { Id = 3, UserId = 6, ComputerId = 16, StartTime = Today.AddHours(18), EndTime = null, TotalPrice = 28m, Status = SessionStatuses.Active },
            new GameSession { Id = 4, UserId = 7, ComputerId = 23, StartTime = Today.AddHours(18), EndTime = null, TotalPrice = 50m, Status = SessionStatuses.Team },
            new GameSession { Id = 5, UserId = 8, ComputerId = 1, StartTime = Today.AddHours(12), EndTime = Today.AddHours(14), TotalPrice = 16m, Status = SessionStatuses.Closed },
            new GameSession { Id = 6, UserId = 9, ComputerId = 3, StartTime = Today.AddHours(13), EndTime = Today.AddHours(15), TotalPrice = 16m, Status = SessionStatuses.Closed },
            new GameSession { Id = 7, UserId = 10, ComputerId = 18, StartTime = Today.AddHours(14), EndTime = Today.AddHours(16), TotalPrice = 28m, Status = SessionStatuses.Closed },
            new GameSession { Id = 8, UserId = 4, ComputerId = 28, StartTime = Today.AddHours(15), EndTime = Today.AddHours(17), TotalPrice = 48m, Status = SessionStatuses.Closed },
            new GameSession { Id = 9, UserId = 5, ComputerId = 6, StartTime = Today.AddHours(19), EndTime = null, TotalPrice = 8m, Status = SessionStatuses.Active },
            new GameSession { Id = 10, UserId = 6, ComputerId = 20, StartTime = Today.AddHours(11), EndTime = Today.AddHours(12), TotalPrice = 14m, Status = SessionStatuses.Closed }
        };

        InsertMissingRange(dbContext, sessions, session => session.Id);
    }

    private static void UpsertPayments(AppDbContext dbContext)
    {
        var payments = new[]
        {
            new Payment { Id = 1, UserId = 4, Amount = 42m, PaymentType = PaymentTypes.Card, CreatedAt = Today.AddHours(9), Comment = "Balance top-up" },
            new Payment { Id = 2, UserId = 5, Amount = 24m, PaymentType = PaymentTypes.Cash, CreatedAt = Today.AddHours(10), Comment = "Standard session" },
            new Payment { Id = 3, UserId = 6, Amount = 28m, PaymentType = PaymentTypes.Card, CreatedAt = Today.AddHours(11), Comment = "VIP session" },
            new Payment { Id = 4, UserId = 7, Amount = 50m, PaymentType = PaymentTypes.Cash, CreatedAt = Today.AddHours(12), Comment = "Bootcamp room" },
            new Payment { Id = 5, UserId = 8, Amount = 16m, PaymentType = PaymentTypes.Card, CreatedAt = Today.AddHours(13), Comment = "Closed session" },
            new Payment { Id = 6, UserId = 9, Amount = 35m, PaymentType = PaymentTypes.Online, CreatedAt = Today.AddHours(14), Comment = "Package purchase" },
            new Payment { Id = 7, UserId = 10, Amount = 48m, PaymentType = PaymentTypes.Card, CreatedAt = Today.AddHours(15), Comment = "Royal VIP session" },
            new Payment { Id = 8, UserId = 4, Amount = 12m, PaymentType = PaymentTypes.Bonus, CreatedAt = Today.AddHours(16), Comment = "Loyalty bonus" },
            new Payment { Id = 9, UserId = 5, Amount = 18m, PaymentType = PaymentTypes.Cash, CreatedAt = Today.AddHours(17), Comment = "Snacks and drinks" },
            new Payment { Id = 10, UserId = 6, Amount = 14m, PaymentType = PaymentTypes.Card, CreatedAt = Today.AddHours(18), Comment = "VIP quick session" }
        };

        InsertMissingRange(dbContext, payments, payment => payment.Id);
    }

    private static void UpsertShifts(AppDbContext dbContext)
    {
        var shifts = new[]
        {
            new Shift { Id = 1, EmployeeName = "Anna Smirnova", StartTime = Today.AddDays(-4).AddHours(9), EndTime = Today.AddDays(-4).AddHours(21), CashTotal = 940m },
            new Shift { Id = 2, EmployeeName = "Dmitry Volkov", StartTime = Today.AddDays(-3).AddHours(9), EndTime = Today.AddDays(-3).AddHours(21), CashTotal = 1120m },
            new Shift { Id = 3, EmployeeName = "Anna Smirnova", StartTime = Today.AddDays(-2).AddHours(9), EndTime = Today.AddDays(-2).AddHours(21), CashTotal = 1265m },
            new Shift { Id = 4, EmployeeName = "Dmitry Volkov", StartTime = Today.AddDays(-1).AddHours(9), EndTime = Today.AddDays(-1).AddHours(21), CashTotal = 1080m },
            new Shift { Id = 5, EmployeeName = "Anna Smirnova", StartTime = Today.AddHours(9), EndTime = null, CashTotal = 1248m },
            new Shift { Id = 6, EmployeeName = "Dmitry Volkov", StartTime = Today.AddDays(1).AddHours(9), EndTime = Today.AddDays(1).AddHours(21), CashTotal = 0m },
            new Shift { Id = 7, EmployeeName = "Anna Smirnova", StartTime = Today.AddDays(2).AddHours(9), EndTime = Today.AddDays(2).AddHours(21), CashTotal = 0m },
            new Shift { Id = 8, EmployeeName = "Dmitry Volkov", StartTime = Today.AddDays(3).AddHours(9), EndTime = Today.AddDays(3).AddHours(21), CashTotal = 0m },
            new Shift { Id = 9, EmployeeName = "Anna Smirnova", StartTime = Today.AddDays(4).AddHours(9), EndTime = Today.AddDays(4).AddHours(21), CashTotal = 0m },
            new Shift { Id = 10, EmployeeName = "Dmitry Volkov", StartTime = Today.AddDays(5).AddHours(9), EndTime = Today.AddDays(5).AddHours(21), CashTotal = 0m }
        };

        InsertMissingRange(dbContext, shifts, shift => shift.Id);
    }

    private static void UpsertRange<TEntity, TKey>(
        AppDbContext dbContext,
        IEnumerable<TEntity> sourceItems,
        Func<TEntity, TKey> keySelector,
        Action<TEntity, TEntity> update)
        where TEntity : class
        where TKey : notnull
    {
        var dbSet = dbContext.Set<TEntity>();
        var existing = dbSet.AsEnumerable().ToDictionary(keySelector);

        foreach (var source in sourceItems)
        {
            var key = keySelector(source);
            if (existing.TryGetValue(key, out var target))
            {
                update(target, source);
            }
            else
            {
                dbSet.Add(source);
            }
        }
    }

    private static void InsertMissingRange<TEntity, TKey>(
        AppDbContext dbContext,
        IEnumerable<TEntity> sourceItems,
        Func<TEntity, TKey> keySelector)
        where TEntity : class
        where TKey : notnull
    {
        var dbSet = dbContext.Set<TEntity>();
        var existingKeys = dbSet.AsEnumerable().Select(keySelector).ToHashSet();

        foreach (var source in sourceItems)
        {
            if (!existingKeys.Contains(keySelector(source)))
            {
                dbSet.Add(source);
            }
        }
    }
}
