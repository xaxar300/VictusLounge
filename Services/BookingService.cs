using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;
using VictusLounge.Services.Pricing;

namespace VictusLounge.Services;

public sealed class BookingService
{
    public BookingCreateResult CreateBooking(BookingCreateRequest request)
    {
        return CreateBookingAsync(request).GetAwaiter().GetResult();
    }

    public async Task<BookingCreateResult> CreateBookingAsync(BookingCreateRequest request)
    {
        var seats = BookingRules.NormalizeSeats(request.Seats);
        var start = request.Date.Date.AddHours(request.Hour).AddMinutes(request.Minute);
        var end = start.AddHours(request.Duration);
        var validation = BookingRules.Validate(
            seats,
            request.IsCompanyBooking,
            start,
            end,
            request.Package,
            request.Duration,
            DateTime.Now);
        if (!validation.Success)
        {
            return BookingCreateResult.Fail(validation.ErrorMessage ?? "Бронь не прошла проверку правил.");
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var (resolvedComputers, error) = await ResolveComputersAsync(unitOfWork, seats, start, end);
            if (error is not null)
            {
                return BookingCreateResult.Fail(error);
            }

            var nextBookingId = await unitOfWork.Bookings.GetNextIdAsync(booking => booking.Id);
            var isImminent = start.Date == DateTime.Today && start <= DateTime.Now.AddMinutes(15);

            var userSessions = unitOfWork.GameSessions
                .QueryNoTracking()
                .Where(session => session.UserId == request.UserId)
                .ToList();
            var tier = LoyaltyTierService.GetTier(LoyaltyTierService.CalculatePlayedHours(userSessions));
            var pricingStrategy = BookingPricingStrategyFactory.Create(request.Package, tier);

            foreach (var seat in seats)
            {
                var computer = resolvedComputers[seat];
                var totalPrice = pricingStrategy.Calculate(new BookingPriceContext(
                    computer.HourPrice,
                    request.Duration,
                    SeatsCount: 1));

                unitOfWork.Bookings.Add(new Booking
                {
                    Id = nextBookingId++,
                    UserId = request.UserId,
                    ComputerId = computer.Id,
                    StartTime = start,
                    EndTime = end,
                    Status = BookingStatuses.PendingPayment,
                    Package = request.Package,
                    TotalPrice = Math.Round(totalPrice, 2),
                    CreatedAt = DateTime.Now
                });

                if (isImminent)
                {
                    computer.Status = PcStatuses.Reserved;
                }
            }

            await unitOfWork.SaveChangesAsync();
            return BookingCreateResult.Ok();
        }
        catch (Exception ex)
        {
            return BookingCreateResult.Fail("Не удалось сохранить бронь: проверьте подключение к SQL Server.", ex);
        }
    }

    private static async Task<(Dictionary<string, Computer> Computers, string? Error)> ResolveComputersAsync(
        IUnitOfWork unitOfWork,
        IEnumerable<string> seats,
        DateTime start,
        DateTime end)
    {
        var resolvedComputers = new Dictionary<string, Computer>(StringComparer.Ordinal);

        foreach (var seat in seats)
        {
            var computer = await unitOfWork.Computers.GetByNameAsync(seat);
            if (computer is null)
            {
                return (resolvedComputers, $"ПК {seat} не найден в базе данных.");
            }

            if (PcStatusNormalizer.Normalize(computer.Status) == PcStatuses.Service)
            {
                return (resolvedComputers, $"ПК {seat} находится в обслуживании. Выберите другое место.");
            }

            if (await unitOfWork.Bookings.HasTimeConflictAsync(computer.Id, start, end))
            {
                return (resolvedComputers, $"ПК {seat} уже занят на это время. Выберите другой интервал или место.");
            }

            if (HasGameSessionConflictForBooking(unitOfWork, computer.Id, start, end))
            {
                return (resolvedComputers, $"ПК {seat} уже занят игровой сессией на это время. Выберите другой интервал или место.");
            }

            resolvedComputers[seat] = computer;
        }

        return (resolvedComputers, null);
    }

    private static bool HasGameSessionConflictForBooking(
        IUnitOfWork unitOfWork,
        int computerId,
        DateTime start,
        DateTime end)
    {
        return unitOfWork.GameSessions.Query().Any(session =>
            session.ComputerId == computerId
            && session.Status != SessionStatuses.Closed
            && session.StartTime < end
            && ((session.EndTime != null && session.EndTime > start)
                || (session.EndTime == null && start.Date == DateTime.Today)));
    }

}

public sealed record BookingCreateRequest(
    int UserId,
    IReadOnlyCollection<string> Seats,
    bool IsCompanyBooking,
    DateTime Date,
    int Hour,
    int Minute,
    int Duration,
    string Package);

public sealed record BookingCreateResult(bool Success, string? ErrorMessage, Exception? Exception)
{
    public static BookingCreateResult Ok()
    {
        return new BookingCreateResult(true, null, null);
    }

    public static BookingCreateResult Fail(string errorMessage, Exception? exception = null)
    {
        return new BookingCreateResult(false, errorMessage, exception);
    }
}
