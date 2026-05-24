using System;
using System.Collections.Generic;
using System.Linq;

namespace VictusLounge.Services;

public static class BookingRules
{
    public const int MaxSingleSeats = 1;
    public const int MaxCompanySeats = 5;

    public static string[] NormalizeSeats(IEnumerable<string> seats)
    {
        return seats
            .Where(seat => !string.IsNullOrWhiteSpace(seat))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static BookingValidationResult Validate(
        IReadOnlyCollection<string> seats,
        bool isCompanyBooking,
        DateTime start,
        DateTime end,
        string package,
        int duration,
        DateTime now)
    {
        var seatValidation = ValidateSeats(seats, isCompanyBooking);
        if (!seatValidation.Success)
        {
            return seatValidation;
        }

        var timeValidation = ValidateTime(start, end, now);
        if (!timeValidation.Success)
        {
            return timeValidation;
        }

        return ValidatePackage(package, start.Hour, start.Minute, duration);
    }

    public static BookingValidationResult ValidateSeats(IReadOnlyCollection<string> seats, bool isCompanyBooking)
    {
        if (seats.Count == 0)
        {
            return BookingValidationResult.Fail("Выберите хотя бы один свободный ПК перед подтверждением.");
        }

        if (!isCompanyBooking && seats.Count > MaxSingleSeats)
        {
            return BookingValidationResult.Fail("Одиночная бронь может содержать только 1 ПК.");
        }

        if (isCompanyBooking && seats.Count > MaxCompanySeats)
        {
            return BookingValidationResult.Fail("Групповая бронь ограничена 5 ПК.");
        }

        return BookingValidationResult.Ok();
    }

    public static BookingValidationResult ValidateTime(DateTime start, DateTime end, DateTime now)
    {
        if (end <= start)
        {
            return BookingValidationResult.Fail("Окончание брони должно быть позже её начала.");
        }

        if (start < now.AddMinutes(-15))
        {
            return BookingValidationResult.Fail("Нельзя бронировать прошедшее время. Выберите ближайший свободный час.");
        }

        return BookingValidationResult.Ok();
    }

    public static BookingValidationResult ValidatePackage(string package, int hour, int minute, int duration)
    {
        if (!IsHourAllowed(package, hour))
        {
            return BookingValidationResult.Fail("Выбранный пакет недоступен для этого часа.");
        }

        if (!IsMinuteAllowed(package, minute))
        {
            return BookingValidationResult.Fail("Пакетные тарифы стартуют ровно в выбранный час.");
        }

        if (package == "night" && duration != 8)
        {
            return BookingValidationResult.Fail("Night Pack должен длиться 8 часов.");
        }

        if (package == "morning" && duration != 3)
        {
            return BookingValidationResult.Fail("Morning Pack должен длиться 3 часа.");
        }

        return BookingValidationResult.Ok();
    }

    public static bool IsHourAllowed(string package, int hour)
    {
        return package switch
        {
            "night" => IsNightPackHour(hour),
            "morning" => IsMorningPackHour(hour),
            _ => true
        };
    }

    public static bool IsMinuteAllowed(string package, int minute)
    {
        return package == "regular" || minute == 0;
    }

    public static bool IsNightPackHour(int hour)
    {
        return hour is 22 or 23 or 0;
    }

    public static bool IsMorningPackHour(int hour)
    {
        return hour is 6 or 7 or 8;
    }

    public static string GetPackageDescription(string package, int duration)
    {
        return package switch
        {
            "night" => "Night Pack: 8 часов, старт только в 22:00, 23:00 или 00:00, скидка 25%.",
            "morning" => "Morning Pack: 3 часа, старт только в 06:00, 07:00 или 08:00, скидка 20%.",
            _ => $"Обычный тариф: {duration} ч, скидка Gold 10%."
        };
    }
}

public sealed record BookingValidationResult(bool Success, string? ErrorMessage)
{
    public static BookingValidationResult Ok()
    {
        return new BookingValidationResult(true, null);
    }

    public static BookingValidationResult Fail(string errorMessage)
    {
        return new BookingValidationResult(false, errorMessage);
    }
}
