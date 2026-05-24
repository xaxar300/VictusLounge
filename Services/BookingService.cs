using System;
using System.Collections.Generic;
using System.Linq;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge.Services;

public sealed class BookingService
{
    public BookingCreateResult CreateBooking(BookingCreateRequest request)
    {
        var seats = request.Seats
            .Where(seat => !string.IsNullOrWhiteSpace(seat))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (seats.Length == 0)
        {
            return BookingCreateResult.Fail("Выберите хотя бы один свободный ПК перед подтверждением.");
        }

        if (!request.IsCompanyBooking && seats.Length > 1)
        {
            return BookingCreateResult.Fail("Одиночная бронь может содержать только 1 ПК.");
        }

        if (request.IsCompanyBooking && seats.Length > 5)
        {
            return BookingCreateResult.Fail("Групповая бронь ограничена 5 ПК.");
        }

        var start = request.Date.Date.AddHours(request.Hour).AddMinutes(request.Minute);
        var end = start.AddHours(request.Duration);

        if (end <= start)
        {
            return BookingCreateResult.Fail("Окончание брони должно быть позже её начала.");
        }

        if (start < DateTime.Now.AddMinutes(-15))
        {
            return BookingCreateResult.Fail("Нельзя бронировать прошедшее время. Выберите ближайший свободный час.");
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var resolvedComputers = ResolveComputers(unitOfWork, seats, start, end, out var error);
            if (error is not null)
            {
                return BookingCreateResult.Fail(error);
            }

            var nextBookingId = unitOfWork.Bookings.GetNextId(booking => booking.Id);
            var isImminent = start.Date == DateTime.Today && start <= DateTime.Now.AddMinutes(15);

            foreach (var seat in seats)
            {
                var computer = resolvedComputers[seat];

                unitOfWork.Bookings.Add(new Booking
                {
                    Id = nextBookingId++,
                    UserId = request.UserId,
                    ComputerId = computer.Id,
                    StartTime = start,
                    EndTime = end,
                    Status = BookingStatuses.PendingPayment,
                    Package = request.Package,
                    TotalPrice = Math.Round(computer.HourPrice * request.Duration * GetDiscountFactor(request.Package), 2),
                    CreatedAt = DateTime.Now
                });

                if (isImminent)
                {
                    computer.Status = PcStatuses.Reserved;
                }
            }

            unitOfWork.SaveChanges();
            return BookingCreateResult.Ok();
        }
        catch (Exception ex)
        {
            return BookingCreateResult.Fail("Не удалось сохранить бронь: проверьте подключение к SQL Server.", ex);
        }
    }

    private static Dictionary<string, Computer> ResolveComputers(
        IUnitOfWork unitOfWork,
        IEnumerable<string> seats,
        DateTime start,
        DateTime end,
        out string? error)
    {
        var resolvedComputers = new Dictionary<string, Computer>(StringComparer.Ordinal);
        error = null;

        foreach (var seat in seats)
        {
            var computer = unitOfWork.Computers.GetByName(seat);
            if (computer is null)
            {
                error = $"ПК {seat} не найден в базе данных.";
                return resolvedComputers;
            }

            if (PcStatusNormalizer.Normalize(computer.Status) == PcStatuses.Service)
            {
                error = $"ПК {seat} находится в обслуживании. Выберите другое место.";
                return resolvedComputers;
            }

            if (unitOfWork.Bookings.HasTimeConflict(computer.Id, start, end))
            {
                error = $"ПК {seat} уже занят на это время. Выберите другой интервал или место.";
                return resolvedComputers;
            }

            if (unitOfWork.GameSessions.HasTimeConflict(computer.Id, start, end))
            {
                error = $"ПК {seat} уже занят игровой сессией на это время. Выберите другой интервал или место.";
                return resolvedComputers;
            }

            resolvedComputers[seat] = computer;
        }

        return resolvedComputers;
    }

    private static decimal GetDiscountFactor(string package)
    {
        return package switch
        {
            "night" => 0.75m,
            "morning" => 0.8m,
            _ => 0.9m
        };
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
