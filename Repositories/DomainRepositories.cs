using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;

namespace VictusLounge.Repositories;

public interface IUserRepository : IRepository<User>
{
    User? GetByLogin(string login);
    User? GetByIdNoTracking(int id);
    int? GetFirstAdminId();
}

public interface IComputerRepository : IRepository<Computer>
{
    Computer? GetByName(string name);
    Task<Computer?> GetByNameAsync(string name);
    List<Computer> GetOrderedNoTracking();
    Dictionary<int, Computer> GetDictionaryNoTracking();
    List<Computer> GetByZone(string zone);
}

public interface ITariffRepository : IRepository<Tariff>
{
    List<Tariff> GetActiveOrdered();
    Tariff? GetByNamePart(string namePart);
}

public interface IBookingRepository : IRepository<Booking>
{
    bool HasTimeConflict(int computerId, DateTime start, DateTime end, int? excludedBookingId = null);
    Task<bool> HasTimeConflictAsync(int computerId, DateTime start, DateTime end, int? excludedBookingId = null);
    bool HasImminentBooking(int computerId, DateTime now, int? excludedBookingId = null);
    Booking? GetPendingForUser(int userId, DateTime cutoff);
    List<Booking> GetTodaysPending(DateTime today);
}

public interface IGameSessionRepository : IRepository<GameSession>
{
    bool HasOpenSession(int computerId, DateTime now, int? excludedSessionId = null);
    bool HasTimeConflict(int computerId, DateTime start, DateTime end);
    Task<bool> HasTimeConflictAsync(int computerId, DateTime start, DateTime end);
    GameSession? GetOpenForComputer(int computerId);
    decimal? GetOpenSessionAmount(string computerName);
    string? GetFirstPendingPaymentComputerName(DateTime now);
    bool TryGetActiveIndividualSession(int userId, out string computerName);
    List<GameSession> GetActive(DateTime now, int take = 5);
}

public interface IPaymentRepository : IRepository<Payment>
{
    List<Payment> GetRecentForUser(int userId, int take);
    List<Payment> GetAdminLogs(int take);
    decimal SumForDate(DateTime date, string paymentType);
    decimal SumOnlineForDate(DateTime date);
    int CountPendingForDate(DateTime date);
}

public interface IShiftRepository : IRepository<Shift>
{
    Shift? GetCurrent();
    Shift? GetCurrentOrLatest();
    List<Shift> GetRecent(int take);
}

public interface IPromoCodeRepository : IRepository<PromoCode>
{
    PromoCode? GetActiveByCode(string code);
}

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public User? GetByLogin(string login)
    {
        return FirstOrDefault(user => user.Login == login);
    }

    public User? GetByIdNoTracking(int id)
    {
        return QueryNoTracking().FirstOrDefault(user => user.Id == id);
    }

    public int? GetFirstAdminId()
    {
        return QueryNoTracking()
            .Where(user => user.Role == "Admin")
            .OrderBy(user => user.Id)
            .Select(user => (int?)user.Id)
            .FirstOrDefault();
    }
}

public class ComputerRepository : Repository<Computer>, IComputerRepository
{
    public ComputerRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Computer? GetByName(string name)
    {
        return FirstOrDefault(computer => computer.Name == name);
    }

    public Task<Computer?> GetByNameAsync(string name)
    {
        return Query().FirstOrDefaultAsync(computer => computer.Name == name);
    }

    public List<Computer> GetOrderedNoTracking()
    {
        return QueryNoTracking().OrderBy(computer => computer.Id).ToList();
    }

    public Dictionary<int, Computer> GetDictionaryNoTracking()
    {
        return QueryNoTracking().ToDictionary(computer => computer.Id);
    }

    public List<Computer> GetByZone(string zone)
    {
        return Query().Where(computer => computer.Zone == zone).ToList();
    }
}

public class TariffRepository : Repository<Tariff>, ITariffRepository
{
    public TariffRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public List<Tariff> GetActiveOrdered()
    {
        return QueryNoTracking()
            .Where(tariff => tariff.IsActive)
            .OrderBy(tariff => tariff.Id)
            .ToList();
    }

    public Tariff? GetByNamePart(string namePart)
    {
        return FirstOrDefault(tariff => tariff.Name.Contains(namePart));
    }
}

public class BookingRepository : Repository<Booking>, IBookingRepository
{
    public BookingRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public bool HasTimeConflict(int computerId, DateTime start, DateTime end, int? excludedBookingId = null)
    {
        return Query().Any(booking =>
            (!excludedBookingId.HasValue || booking.Id != excludedBookingId.Value)
            && booking.ComputerId == computerId
            && booking.Status != BookingStatuses.Cancelled
            && booking.StartTime < end
            && booking.EndTime > start);
    }

    public Task<bool> HasTimeConflictAsync(int computerId, DateTime start, DateTime end, int? excludedBookingId = null)
    {
        return Query().AnyAsync(booking =>
            (!excludedBookingId.HasValue || booking.Id != excludedBookingId.Value)
            && booking.ComputerId == computerId
            && booking.Status != BookingStatuses.Cancelled
            && booking.StartTime < end
            && booking.EndTime > start);
    }

    public bool HasImminentBooking(int computerId, DateTime now, int? excludedBookingId = null)
    {
        return Query().Any(booking =>
            (!excludedBookingId.HasValue || booking.Id != excludedBookingId.Value)
            && booking.ComputerId == computerId
            && booking.Status != BookingStatuses.Cancelled
            && booking.StartTime <= now.AddMinutes(15)
            && booking.EndTime > now);
    }

    public Booking? GetPendingForUser(int userId, DateTime cutoff)
    {
        return Query()
            .Where(booking => booking.UserId == userId
                && booking.Status == BookingStatuses.PendingPayment
                && booking.StartTime >= cutoff)
            .OrderByDescending(booking => booking.CreatedAt)
            .ThenByDescending(booking => booking.Id)
            .FirstOrDefault();
    }

    public List<Booking> GetTodaysPending(DateTime today)
    {
        var tomorrow = today.AddDays(1);
        return Query()
            .Where(booking => booking.Status == BookingStatuses.PendingPayment
                && booking.CreatedAt >= today
                && booking.CreatedAt < tomorrow)
            .ToList();
    }
}

public class GameSessionRepository : Repository<GameSession>, IGameSessionRepository
{
    private readonly AppDbContext _dbContext;

    public GameSessionRepository(AppDbContext dbContext) : base(dbContext)
    {
        _dbContext = dbContext;
    }

    public bool HasOpenSession(int computerId, DateTime now, int? excludedSessionId = null)
    {
        return Query().Any(session =>
            (!excludedSessionId.HasValue || session.Id != excludedSessionId.Value)
            && session.ComputerId == computerId
            && session.Status != SessionStatuses.Closed
            && session.StartTime <= now
            && (session.EndTime == null || session.EndTime > now));
    }

    public bool HasTimeConflict(int computerId, DateTime start, DateTime end)
    {
        return Query().Any(session =>
            session.ComputerId == computerId
            && session.Status != SessionStatuses.Closed
            && session.StartTime < end
            && (session.EndTime == null || session.EndTime > start));
    }

    public Task<bool> HasTimeConflictAsync(int computerId, DateTime start, DateTime end)
    {
        return Query().AnyAsync(session =>
            session.ComputerId == computerId
            && session.Status != SessionStatuses.Closed
            && session.StartTime < end
            && (session.EndTime == null || session.EndTime > start));
    }

    public GameSession? GetOpenForComputer(int computerId)
    {
        return Query()
            .Where(session => session.ComputerId == computerId && session.EndTime == null)
            .OrderByDescending(session => session.StartTime)
            .FirstOrDefault();
    }

    public decimal? GetOpenSessionAmount(string computerName)
    {
        return QueryNoTracking()
            .Where(session => session.EndTime == null
                && _dbContext.Computers.Any(computer => computer.Id == session.ComputerId && computer.Name == computerName))
            .OrderByDescending(session => session.StartTime)
            .Select(session => (decimal?)session.TotalPrice)
            .FirstOrDefault();
    }

    public string? GetFirstPendingPaymentComputerName(DateTime now)
    {
        return QueryNoTracking()
            .Where(session => session.Status == SessionStatuses.AwaitingPayment
                && (session.EndTime == null || session.EndTime > now))
            .OrderBy(session => session.StartTime)
            .Select(session => _dbContext.Computers
                .Where(computer => computer.Id == session.ComputerId)
                .Select(computer => computer.Name)
                .FirstOrDefault())
            .FirstOrDefault();
    }

    public bool TryGetActiveIndividualSession(int userId, out string computerName)
    {
        var now = DateTime.Now;
        var sessionInfo = QueryNoTracking()
            .Where(session => session.UserId == userId
                && session.Status != SessionStatuses.Closed
                && session.Status != SessionStatuses.Team
                && (session.EndTime == null || session.EndTime > now))
            .OrderByDescending(session => session.StartTime)
            .Select(session => new
            {
                ComputerName = _dbContext.Computers
                    .Where(computer => computer.Id == session.ComputerId)
                    .Select(computer => computer.Name)
                    .FirstOrDefault()
            })
            .FirstOrDefault();

        computerName = sessionInfo?.ComputerName ?? "другом ПК";
        return sessionInfo is not null;
    }

    public List<GameSession> GetActive(DateTime now, int take = 5)
    {
        return QueryNoTracking()
            .Where(session => session.Status != SessionStatuses.Closed
                && session.StartTime <= now
                && (session.EndTime == null || session.EndTime > now))
            .OrderBy(session => session.StartTime)
            .Take(take)
            .ToList();
    }
}

public class PaymentRepository : Repository<Payment>, IPaymentRepository
{
    public PaymentRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public List<Payment> GetRecentForUser(int userId, int take)
    {
        return QueryNoTracking()
            .Where(payment => payment.UserId == userId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(take)
            .ToList();
    }

    public List<Payment> GetAdminLogs(int take)
    {
        return QueryNoTracking()
            .Where(payment => payment.PaymentType == PaymentTypes.AdminLog)
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(take)
            .ToList();
    }

    public decimal SumForDate(DateTime date, string paymentType)
    {
        return QueryNoTracking()
            .Where(payment => payment.CreatedAt.Date == date.Date)
            .Where(payment => payment.PaymentType == paymentType)
            .Sum(payment => payment.Amount);
    }

    public decimal SumOnlineForDate(DateTime date)
    {
        return QueryNoTracking()
            .Where(payment => payment.CreatedAt.Date == date.Date)
            .Where(payment => payment.Amount > 0
                && (payment.PaymentType == PaymentTypes.Card || payment.PaymentType == PaymentTypes.Online))
            .Sum(payment => payment.Amount);
    }

    public int CountPendingForDate(DateTime date)
    {
        return Query().Count(payment =>
            payment.PaymentType.StartsWith(PaymentTypes.Pending)
            && payment.CreatedAt.Date == date.Date);
    }
}

public class ShiftRepository : Repository<Shift>, IShiftRepository
{
    public ShiftRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public Shift? GetCurrent()
    {
        return Query()
            .OrderByDescending(shift => shift.StartTime)
            .FirstOrDefault(shift => shift.EndTime == null);
    }

    public Shift? GetCurrentOrLatest()
    {
        return GetCurrent()
            ?? Query().OrderByDescending(shift => shift.StartTime).FirstOrDefault();
    }

    public List<Shift> GetRecent(int take)
    {
        return QueryNoTracking()
            .OrderByDescending(shift => shift.StartTime)
            .Take(take)
            .ToList();
    }
}

public class PromoCodeRepository : Repository<PromoCode>, IPromoCodeRepository
{
    public PromoCodeRepository(AppDbContext dbContext) : base(dbContext)
    {
    }

    public PromoCode? GetActiveByCode(string code)
    {
        return QueryNoTracking().FirstOrDefault(promoCode => promoCode.IsActive && promoCode.Code == code);
    }
}
