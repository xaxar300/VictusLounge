using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VictusLounge.Services.Facades;

public sealed class BookingFacade
{
    private readonly BookingService _bookingService;

    public BookingFacade()
        : this(new BookingService())
    {
    }

    public BookingFacade(BookingService bookingService)
    {
        _bookingService = bookingService;
    }

    public BookingSeatSelectionResult SelectSeat(BookingSeatSelectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Seat))
        {
            return BookingSeatSelectionResult.Fail(
                request.CurrentSeats,
                "ПК не выбран.");
        }

        var seats = request.CurrentSeats
            .Where(seat => !string.IsNullOrWhiteSpace(seat))
            .ToHashSet(StringComparer.Ordinal);

        if (!request.IsCompanyBooking)
        {
            return BookingSeatSelectionResult.Ok([request.Seat]);
        }

        if (!seats.Contains(request.Seat) && seats.Count >= BookingRules.MaxCompanySeats)
        {
            return BookingSeatSelectionResult.Fail(
                seats,
                $"Групповая бронь ограничена {BookingRules.MaxCompanySeats} ПК.");
        }

        if (!seats.Add(request.Seat))
        {
            seats.Remove(request.Seat);
        }

        return BookingSeatSelectionResult.Ok(seats);
    }

    public async Task<BookingFacadeResult> ConfirmBookingAsync(BookingFacadeRequest request)
    {
        if (!request.IsUserSignedIn)
        {
            return BookingFacadeResult.Fail(
                BookingFacadeFailure.AuthRequired,
                "Войдите в систему перед бронированием.");
        }

        var start = request.Date.Date.AddHours(request.Hour).AddMinutes(request.Minute);
        var end = start.AddHours(request.Duration);
        var validation = BookingRules.Validate(
            request.Seats,
            request.IsCompanyBooking,
            start,
            end,
            request.Package,
            request.Duration,
            DateTime.Now);

        if (!validation.Success)
        {
            return BookingFacadeResult.Fail(
                BookingFacadeFailure.Validation,
                validation.ErrorMessage ?? "Бронь не прошла проверку правил.");
        }

        var result = await _bookingService.CreateBookingAsync(new BookingCreateRequest(
            request.UserId,
            request.Seats,
            request.IsCompanyBooking,
            request.Date,
            request.Hour,
            request.Minute,
            request.Duration,
            request.Package));

        return result.Success
            ? BookingFacadeResult.Ok()
            : BookingFacadeResult.Fail(
                BookingFacadeFailure.Persistence,
                result.ErrorMessage ?? "Не удалось сохранить бронь.",
                result.Exception);
    }
}

public sealed record BookingFacadeRequest(
    int UserId,
    bool IsUserSignedIn,
    IReadOnlyCollection<string> Seats,
    bool IsCompanyBooking,
    DateTime Date,
    int Hour,
    int Minute,
    int Duration,
    string Package);

public sealed record BookingSeatSelectionRequest(
    IReadOnlyCollection<string> CurrentSeats,
    string Seat,
    bool IsCompanyBooking);

public sealed record BookingSeatSelectionResult(
    bool Success,
    IReadOnlyCollection<string> Seats,
    string? ErrorMessage)
{
    public static BookingSeatSelectionResult Ok(IReadOnlyCollection<string> seats)
    {
        return new BookingSeatSelectionResult(true, seats.Order(StringComparer.Ordinal).ToArray(), null);
    }

    public static BookingSeatSelectionResult Fail(IReadOnlyCollection<string> seats, string errorMessage)
    {
        return new BookingSeatSelectionResult(false, seats.Order(StringComparer.Ordinal).ToArray(), errorMessage);
    }
}

public sealed record BookingFacadeResult(
    bool Success,
    BookingFacadeFailure Failure,
    string? ErrorMessage,
    Exception? Exception)
{
    public static BookingFacadeResult Ok()
    {
        return new BookingFacadeResult(true, BookingFacadeFailure.None, null, null);
    }

    public static BookingFacadeResult Fail(
        BookingFacadeFailure failure,
        string errorMessage,
        Exception? exception = null)
    {
        return new BookingFacadeResult(false, failure, errorMessage, exception);
    }
}

public enum BookingFacadeFailure
{
    None,
    AuthRequired,
    Validation,
    Persistence
}
