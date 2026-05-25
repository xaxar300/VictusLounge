using System;
using System.Collections.Generic;
using System.Linq;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge.Services;

public sealed class AdminOperationsService
{
    public AdminOperationResult CreateGuestSession(string computerName, decimal amount, int currentUserId)
    {
        using var unitOfWork = new UnitOfWork();
        var computer = unitOfWork.Computers.GetByName(computerName);
        if (computer is null)
        {
            return AdminOperationResult.Fail($"{computerName}: PC not found.");
        }

        var now = DateTime.Now;
        if (unitOfWork.GameSessions.HasOpenSession(computer.Id, now))
        {
            return AdminOperationResult.Fail($"{computerName}: PC already has an open session.");
        }

        var currentUser = unitOfWork.Users.GetById(currentUserId);
        if (currentUser is not null
            && StatusMapper.ToUserRole(currentUser.Role) == UserRole.Client
            && unitOfWork.GameSessions.TryGetActiveIndividualSession(currentUserId, out var activeSessionComputer))
        {
            return AdminOperationResult.Fail($"Client already has an active session on {activeSessionComputer}.");
        }

        unitOfWork.GameSessions.Add(new GameSession
        {
            Id = unitOfWork.GameSessions.GetNextId(session => session.Id),
            UserId = currentUserId,
            ComputerId = computer.Id,
            StartTime = now,
            EndTime = null,
            TotalPrice = amount,
            Status = SessionStatus.Active.ToStorageValue()
        });

        unitOfWork.Payments.Add(new Payment
        {
            Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
            UserId = currentUserId,
            Amount = amount,
            PaymentType = PaymentTypes.Cash,
            CreatedAt = now,
            Comment = $"Guest session: {computerName}"
        });

        computer.Status = PcStatus.Busy.ToStorageValue();
        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok(currentUserId);
    }

    public AdminOperationResult ConfirmPayment(string computerName, decimal amount, int currentUserId)
    {
        using var unitOfWork = new UnitOfWork();
        var computer = unitOfWork.Computers.GetByName(computerName);
        if (computer is null)
        {
            return AdminOperationResult.Fail($"{computerName}: PC not found.");
        }

        var session = unitOfWork.GameSessions.GetOpenForComputer(computer.Id);
        if (session is not null)
        {
            session.Status = SessionStatus.Active.ToStorageValue();
            session.TotalPrice += amount;
        }

        var booking = unitOfWork.Bookings.Query()
            .Where(item => item.ComputerId == computer.Id && item.Status == BookingStatuses.PendingPayment)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (booking is not null)
        {
            booking.Status = BookingStatus.Confirmed.ToStorageValue();
        }

        var pendingPayment = unitOfWork.Payments.Query()
            .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending) && item.Amount == amount)
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();

        if (pendingPayment is not null)
        {
            pendingPayment.PaymentType = PaymentTypes.Cash;
            pendingPayment.Comment = $"Payment confirmed: {computerName}";
        }
        else
        {
            var paymentUserId = session?.UserId ?? booking?.UserId ?? ResolveCurrentOrAdminUserId(unitOfWork, currentUserId);
            if (paymentUserId is null)
            {
                return AdminOperationResult.Fail("Payment user was not found.");
            }

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = paymentUserId.Value,
                Amount = amount,
                PaymentType = PaymentTypes.Cash,
                CreatedAt = DateTime.Now,
                Comment = $"Payment confirmed: {computerName}"
            });
        }

        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok(session?.UserId ?? booking?.UserId ?? currentUserId);
    }

    public AdminOperationResult SettlePendingPayments(decimal amountPerPayment)
    {
        using var unitOfWork = new UnitOfWork();
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        foreach (var booking in unitOfWork.Bookings.Query()
            .Where(item => item.Status == BookingStatuses.PendingPayment
                && item.CreatedAt >= today
                && item.CreatedAt < tomorrow))
        {
            booking.Status = BookingStatus.Confirmed.ToStorageValue();
        }

        foreach (var session in unitOfWork.GameSessions.Query()
            .Where(item => item.Status == SessionStatuses.AwaitingPayment
                && item.StartTime >= today
                && item.StartTime < tomorrow))
        {
            session.Status = SessionStatus.Active.ToStorageValue();
        }

        foreach (var payment in unitOfWork.Payments.Query()
            .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending)
                && item.CreatedAt >= today
                && item.CreatedAt < tomorrow))
        {
            if (payment.Amount > 0
                && payment.Comment.StartsWith("Pending balance top-up", StringComparison.OrdinalIgnoreCase)
                && unitOfWork.Users.GetById(payment.UserId) is { } paymentUser)
            {
                paymentUser.Balance += payment.Amount;
                var playedHours = LoyaltyTierService.CalculatePlayedHours(unitOfWork.GameSessions
                    .QueryNoTracking()
                    .Where(session => session.UserId == paymentUser.Id)
                    .ToList());
                paymentUser.LoyaltyTier = LoyaltyTierService.GetTier(playedHours);
            }

            payment.PaymentType = PaymentTypes.Cash;
            payment.Comment = $"{payment.Comment}; confirmed by admin";
        }

        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok();
    }

    public AdminOperationResult CloseSession(string computerName)
    {
        using var unitOfWork = new UnitOfWork();
        var computer = unitOfWork.Computers.GetByName(computerName);
        if (computer is null)
        {
            return AdminOperationResult.Fail($"{computerName}: PC not found.");
        }

        var session = unitOfWork.GameSessions.GetOpenForComputer(computer.Id);
        int? userId = null;
        if (session is not null)
        {
            userId = session.UserId;
            session.EndTime = DateTime.Now;
            session.Status = SessionStatus.Closed.ToStorageValue();
        }

        computer.Status = PcStatus.Free.ToStorageValue();
        unitOfWork.SaveChanges();
        if (userId is not null)
        {
            RefreshStoredClientTier(unitOfWork, userId.Value);
        }

        return AdminOperationResult.Ok(userId);
    }

    public AdminOperationResult ExtendSession(string computerName, decimal amount)
    {
        using var unitOfWork = new UnitOfWork();
        var computer = unitOfWork.Computers.GetByName(computerName);
        if (computer is null)
        {
            return AdminOperationResult.Fail($"{computerName}: PC not found.");
        }

        var session = unitOfWork.GameSessions.GetOpenForComputer(computer.Id);
        if (session is null)
        {
            return AdminOperationResult.Fail($"{computerName}: open session was not found.");
        }

        session.TotalPrice += amount;
        unitOfWork.Payments.Add(new Payment
        {
            Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
            UserId = session.UserId,
            Amount = amount,
            PaymentType = PaymentTypes.Online,
            CreatedAt = DateTime.Now,
            Comment = $"Session extension: {computerName}"
        });

        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok(session.UserId);
    }

    public void SaveShiftState(bool closeShift, string employeeName, decimal cashTotal)
    {
        using var unitOfWork = new UnitOfWork();
        var shift = unitOfWork.Shifts.GetCurrentOrLatest();

        if (shift is null)
        {
            shift = new Shift
            {
                Id = unitOfWork.Shifts.GetNextId(item => item.Id),
                EmployeeName = employeeName,
                StartTime = DateTime.Now,
                CashTotal = cashTotal
            };
            unitOfWork.Shifts.Add(shift);
        }

        shift.EmployeeName = employeeName;
        shift.CashTotal = cashTotal;
        shift.EndTime = closeShift ? DateTime.Now : null;
        unitOfWork.SaveChanges();
    }

    public AdminOperationResult SaveShiftExpense(decimal amount, string comment, int currentUserId)
    {
        using var unitOfWork = new UnitOfWork();
        var paymentUserId = ResolveCurrentOrAdminUserId(unitOfWork, currentUserId);
        if (paymentUserId is null)
        {
            return AdminOperationResult.Fail("Expense user was not found.");
        }

        unitOfWork.Payments.Add(new Payment
        {
            Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
            UserId = paymentUserId.Value,
            Amount = -amount,
            PaymentType = PaymentTypes.Cash,
            CreatedAt = DateTime.Now,
            Comment = comment
        });

        var shift = unitOfWork.Shifts.GetCurrent();
        if (shift is not null)
        {
            shift.CashTotal = Math.Max(0, shift.CashTotal - amount);
        }

        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok(paymentUserId.Value);
    }

    public void SaveTariffRate(string namePart, decimal price)
    {
        using var unitOfWork = new UnitOfWork();
        var tariff = unitOfWork.Tariffs.GetByNamePart(namePart);
        if (tariff is not null)
        {
            tariff.PricePerHour = price;
        }

        var zone = namePart switch
        {
            "Standard" => "Standard",
            "VIP" => "VIP",
            "Royal" => "Royal VIP",
            "Bootcamp" => "Bootcamp",
            _ => namePart
        };

        foreach (var computer in unitOfWork.Computers.GetByZone(zone))
        {
            computer.HourPrice = price;
        }

        unitOfWork.SaveChanges();
    }

    public void UpsertManualShift(string employeeName, decimal cashTotal)
    {
        using var unitOfWork = new UnitOfWork();
        var now = DateTime.Now;
        var shift = unitOfWork.Shifts.Query()
            .OrderByDescending(item => item.StartTime)
            .FirstOrDefault(item => item.EmployeeName == employeeName && item.EndTime == null);

        if (shift is null)
        {
            shift = new Shift
            {
                Id = unitOfWork.Shifts.GetNextId(item => item.Id),
                EmployeeName = employeeName,
                StartTime = now,
                CashTotal = cashTotal
            };
            unitOfWork.Shifts.Add(shift);
        }
        else
        {
            shift.CashTotal = cashTotal;
        }

        unitOfWork.SaveChanges();
    }

    public AdminOperationResult RescheduleBooking(int bookingId, DateTime newStart, double durationHours)
    {
        using var unitOfWork = new UnitOfWork();
        var booking = unitOfWork.Bookings.GetById(bookingId);
        if (booking is null || StatusMapper.ToBookingStatus(booking.Status) == BookingStatus.Cancelled)
        {
            return AdminOperationResult.Fail($"Booking #{bookingId} was not found or is cancelled.");
        }

        var newEnd = newStart.AddHours(durationHours);
        var hasConflict = unitOfWork.Bookings.HasTimeConflict(booking.ComputerId, newStart, newEnd, booking.Id)
            || unitOfWork.GameSessions.HasTimeConflict(booking.ComputerId, newStart, newEnd);

        if (hasConflict)
        {
            return AdminOperationResult.Fail("Selected interval already has a booking or session.");
        }

        booking.StartTime = newStart;
        booking.EndTime = newEnd;
        booking.CreatedAt = DateTime.Now;
        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok(booking.UserId);
    }

    public AdminOperationResult CancelBooking(int bookingId)
    {
        using var unitOfWork = new UnitOfWork();
        var booking = unitOfWork.Bookings.GetById(bookingId);
        if (booking is null || StatusMapper.ToBookingStatus(booking.Status) == BookingStatus.Cancelled)
        {
            return AdminOperationResult.Fail($"Booking #{bookingId} was not found or is cancelled.");
        }

        booking.Status = BookingStatus.Cancelled.ToStorageValue();
        unitOfWork.SaveChanges();
        return AdminOperationResult.Ok(booking.UserId);
    }

    public int GetLatestActiveBookingId()
    {
        using var unitOfWork = new UnitOfWork();
        return unitOfWork.Bookings
            .QueryNoTracking()
            .Where(booking => booking.Status != BookingStatuses.Cancelled)
            .OrderByDescending(booking => booking.CreatedAt)
            .Select(booking => booking.Id)
            .FirstOrDefault();
    }

    public decimal? GetOpenSessionAmount(string computerName)
    {
        using var unitOfWork = new UnitOfWork();
        return unitOfWork.GameSessions.GetOpenSessionAmount(computerName);
    }

    public decimal? GetPendingPaymentAmount(string computerName)
    {
        using var unitOfWork = new UnitOfWork();
        var sessionAmount = unitOfWork.GameSessions.GetOpenSessionAmount(computerName);
        if (sessionAmount is > 0)
        {
            return sessionAmount;
        }

        var computer = unitOfWork.Computers.GetByName(computerName);
        if (computer is null)
        {
            return null;
        }

        return unitOfWork.Bookings.QueryNoTracking()
            .Where(booking => booking.ComputerId == computer.Id
                && booking.Status == BookingStatuses.PendingPayment
                && booking.EndTime > DateTime.Now)
            .OrderByDescending(booking => booking.CreatedAt)
            .Select(booking => (decimal?)booking.TotalPrice)
            .FirstOrDefault();
    }

    public string? GetFirstPendingPaymentComputerName()
    {
        using var unitOfWork = new UnitOfWork();
        return unitOfWork.GameSessions.GetFirstPendingPaymentComputerName(DateTime.Now);
    }

    public IReadOnlyList<AdminSessionInfo> GetActiveSessions(DateTime now)
    {
        using var unitOfWork = new UnitOfWork();
        var sessions = unitOfWork.GameSessions.GetActive(now);
        var computers = unitOfWork.Computers.GetDictionaryNoTracking();
        var users = unitOfWork.Users.QueryNoTracking().ToDictionary(user => user.Id);

        return sessions.Select(session =>
        {
            computers.TryGetValue(session.ComputerId, out var computer);
            users.TryGetValue(session.UserId, out var user);
            var computerName = computer?.Name ?? $"PC-{session.ComputerId}";
            var status = StatusMapper.ToSessionStatus(session.Status);
            return new AdminSessionInfo(
                computerName,
                user?.FullName ?? $"User #{session.UserId}",
                session.EndTime?.ToString("HH:mm") ?? "open",
                status,
                FormatSessionStatus(status),
                ResolveSessionActionText(status),
                ResolveSessionAction(computerName, status),
                status == SessionStatus.AwaitingPayment);
        }).ToList();
    }

    public IReadOnlyList<AdminTaskQueueInfo> GetTaskQueue(DateTime now)
    {
        using var unitOfWork = new UnitOfWork();
        var items = new List<AdminTaskQueueInfo>();
        var computers = unitOfWork.Computers.GetDictionaryNoTracking();
        var users = unitOfWork.Users.QueryNoTracking().ToDictionary(user => user.Id);

        var pendingBookings = unitOfWork.Bookings
            .QueryNoTracking()
            .Where(booking => booking.Status == BookingStatuses.PendingPayment && booking.EndTime > now)
            .OrderBy(booking => booking.StartTime)
            .Take(2)
            .ToList();

        foreach (var booking in pendingBookings)
        {
            var computerName = computers.TryGetValue(booking.ComputerId, out var computer)
                ? computer.Name
                : $"PC-{booking.ComputerId}";
            var clientName = users.TryGetValue(booking.UserId, out var user)
                ? user.FullName
                : $"User #{booking.UserId}";

            items.Add(new AdminTaskQueueInfo(
                "Оплатить бронь",
                $"{computerName} · {clientName} · {booking.StartTime:dd.MM HH:mm}",
                AdminTaskType.Payment,
                "Оплата",
                $"admin-payment|{computerName}",
                IsPrimaryAction: true));
        }

        var pendingSessions = unitOfWork.GameSessions
            .QueryNoTracking()
            .Where(session => session.Status == SessionStatuses.AwaitingPayment
                && (session.EndTime == null || session.EndTime > now))
            .OrderBy(session => session.StartTime)
            .Take(2)
            .ToList();

        foreach (var session in pendingSessions)
        {
            var computerName = computers.TryGetValue(session.ComputerId, out var computer)
                ? computer.Name
                : $"PC-{session.ComputerId}";
            var clientName = users.TryGetValue(session.UserId, out var user)
                ? user.FullName
                : $"User #{session.UserId}";

            items.Add(new AdminTaskQueueInfo(
                "Принять оплату сессии",
                $"{computerName} · {clientName} · с {session.StartTime:HH:mm}",
                AdminTaskType.Payment,
                "Оплата",
                $"admin-pay-session|{computerName}",
                IsPrimaryAction: true));
        }

        var serviceComputers = unitOfWork.Computers
            .QueryNoTracking()
            .ToList()
            .Where(computer => StatusMapper.ToPcStatus(computer.Status) == PcStatus.Service)
            .OrderBy(computer => computer.Name)
            .Take(2);

        foreach (var computer in serviceComputers)
        {
            items.Add(new AdminTaskQueueInfo(
                "Проверить сервис",
                $"{computer.Name} · {computer.Zone}",
                AdminTaskType.Service,
                "Снять",
                $"admin-clear-service|{computer.Name}",
                IsPrimaryAction: false));
        }

        return items;
    }

    public void SetComputerStatus(string computerName, PcStatus status)
    {
        using var unitOfWork = new UnitOfWork();
        var computer = unitOfWork.Computers.GetByName(computerName);
        if (computer is null)
        {
            return;
        }

        computer.Status = status.ToStorageValue();
        unitOfWork.SaveChanges();
    }

    private static int? ResolveCurrentOrAdminUserId(IUnitOfWork unitOfWork, int currentUserId)
    {
        if (currentUserId > 0 && unitOfWork.Users.Any(user => user.Id == currentUserId))
        {
            return currentUserId;
        }

        return unitOfWork.Users.GetFirstAdminId();
    }

    private static void RefreshStoredClientTier(IUnitOfWork unitOfWork, int userId)
    {
        var user = unitOfWork.Users.GetById(userId);
        if (user is null)
        {
            return;
        }

        var playedHours = LoyaltyTierService.CalculatePlayedHours(unitOfWork.GameSessions
            .QueryNoTracking()
            .Where(session => session.UserId == userId)
            .ToList());
        user.LoyaltyTier = LoyaltyTierService.GetTier(playedHours);
        unitOfWork.SaveChanges();
    }

    private static string FormatSessionStatus(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.AwaitingPayment => "Ожидает",
            SessionStatus.Team => "Команда",
            SessionStatus.Active => "Оплачено",
            _ => status.ToString()
        };
    }

    private static string ResolveSessionActionText(SessionStatus status)
    {
        return status switch
        {
            SessionStatus.AwaitingPayment => "Оплата",
            SessionStatus.Team => "Продлить",
            _ => "Закрыть"
        };
    }

    private static string ResolveSessionAction(string computerName, SessionStatus status)
    {
        return status switch
        {
            SessionStatus.AwaitingPayment => $"admin-pay-session|{computerName}",
            SessionStatus.Team => $"admin-extend-session|{computerName}",
            _ => $"admin-close-session|{computerName}"
        };
    }
}

public sealed record AdminOperationResult(bool Success, string? ErrorMessage, int? AffectedUserId)
{
    public static AdminOperationResult Ok(int? affectedUserId = null)
    {
        return new AdminOperationResult(true, null, affectedUserId);
    }

    public static AdminOperationResult Fail(string errorMessage)
    {
        return new AdminOperationResult(false, errorMessage, null);
    }
}

public sealed record AdminSessionInfo(
    string ComputerName,
    string ClientName,
    string EndText,
    SessionStatus Status,
    string StatusText,
    string ActionText,
    string ActionCommandParameter,
    bool IsPrimaryAction);

public sealed record AdminTaskQueueInfo(
    string Title,
    string Details,
    AdminTaskType Kind,
    string ActionText,
    string ActionCommandParameter,
    bool IsPrimaryAction);

public enum AdminTaskType
{
    Payment,
    Service
}
