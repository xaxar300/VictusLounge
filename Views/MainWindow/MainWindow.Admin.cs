using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge;

public partial class MainWindow
{
    private bool SaveGuestSession(string computerName, decimal amount)
    {
        if (!EnsureSignedInForDatabaseWrite())
        {
            return false;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var computer = unitOfWork.Computers.GetByName(computerName);
            if (computer is null)
            {
                return false;
            }

            var now = DateTime.Now;
            var hasComputerSessionConflict = unitOfWork.GameSessions.HasOpenSession(computer.Id, now);
            if (hasComputerSessionConflict)
            {
                ShowStatus("РџРљ Р·Р°РЅСЏС‚", $"{computerName}: СѓР¶Рµ РµСЃС‚СЊ РЅРµР·Р°РєСЂС‹С‚Р°СЏ СЃРµСЃСЃРёСЏ РЅР° СЌС‚РѕРј РџРљ.");
                return false;
            }

            var currentUser = unitOfWork.Users.GetById(_currentUserId);
            if (currentUser is not null
                && NormalizeRole(currentUser.Role) == "client"
                && HasActiveIndividualSession(unitOfWork, _currentUserId, out var activeSessionComputer))
            {
                ShowStatus("РЎРµСЃСЃРёСЏ СѓР¶Рµ Р°РєС‚РёРІРЅР°", $"РЈ РєР»РёРµРЅС‚Р° СѓР¶Рµ РµСЃС‚СЊ Р°РєС‚РёРІРЅР°СЏ СЃРµСЃСЃРёСЏ РЅР° {activeSessionComputer}. РЎРЅР°С‡Р°Р»Р° Р·Р°РІРµСЂС€РёС‚Рµ РµРµ.");
                return false;
            }

            unitOfWork.GameSessions.Add(new GameSession
            {
                Id = unitOfWork.GameSessions.GetNextId(session => session.Id),
                UserId = _currentUserId,
                ComputerId = computer.Id,
                StartTime = now,
                EndTime = null,
                TotalPrice = amount,
                Status = SessionStatuses.Active
            });

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = _currentUserId,
                Amount = amount,
                PaymentType = PaymentTypes.Cash,
                CreatedAt = now,
                Comment = $"Guest session: {computerName}"
            });

            computer.Status = PcStatuses.Busy;
            unitOfWork.SaveChanges();
            LoadDatabaseState();
            var refreshedUser = unitOfWork.Users.GetByIdNoTracking(_currentUserId);
            if (refreshedUser is not null)
            {
                RefreshClientUx(unitOfWork, refreshedUser);
            }

            return true;
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ СЃРµСЃСЃРёРё", ex);
            return false;
        }
    }

    private void SavePaymentConfirmation(string computerName, decimal amount)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var computer = unitOfWork.Computers.GetByName(computerName);
            if (computer is null)
            {
                return;
            }

            var session = unitOfWork.GameSessions.GetOpenForComputer(computer.Id);
            if (session is not null)
            {
                session.Status = SessionStatuses.Active;
                session.TotalPrice += amount;
            }

            var booking = unitOfWork.Bookings.Query()
                .Where(item => item.ComputerId == computer.Id && item.Status == BookingStatuses.PendingPayment)
                .OrderByDescending(item => item.CreatedAt)
                .FirstOrDefault();
            if (booking is not null)
            {
                booking.Status = BookingStatuses.Confirmed;
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
                var paymentUserId = session?.UserId ?? booking?.UserId ?? ResolveCurrentOrAdminUserId(unitOfWork);
                if (paymentUserId is null)
                {
                    ShowStatus("Р’РѕР№РґРёС‚Рµ РІ СЃРёСЃС‚РµРјСѓ", "РћРїР»Р°С‚Р° РЅРµ СЃРѕС…СЂР°РЅРµРЅР°: РЅРµ РЅР°Р№РґРµРЅ РїРѕР»СЊР·РѕРІР°С‚РµР»СЊ РґР»СЏ Р·Р°РїРёСЃРё РїР»Р°С‚РµР¶Р°.");
                    return;
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
            LoadDatabaseState();

            var currentUser = unitOfWork.Users.GetByIdNoTracking(_currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(unitOfWork, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РїРѕРґС‚РІРµСЂР¶РґРµРЅРёСЏ РѕРїР»Р°С‚С‹", ex);
        }
    }

    private void SaveAllPendingPaymentsAsCash(decimal amountPerPayment)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var pendingBookings = unitOfWork.Bookings.Query()
                .Where(item => item.Status == BookingStatuses.PendingPayment
                    && item.CreatedAt >= today
                    && item.CreatedAt < tomorrow)
                .ToList();
            foreach (var booking in pendingBookings)
            {
                booking.Status = BookingStatuses.Confirmed;
            }

            var pendingSessions = unitOfWork.GameSessions.Query()
                .Where(item => item.Status == SessionStatuses.AwaitingPayment
                    && item.StartTime >= today
                    && item.StartTime < tomorrow)
                .ToList();
            foreach (var session in pendingSessions)
            {
                session.Status = SessionStatuses.Active;
            }

            var pendingPayments = unitOfWork.Payments.Query()
                .Where(item => item.PaymentType.StartsWith(PaymentTypes.Pending)
                    && item.CreatedAt >= today
                    && item.CreatedAt < tomorrow)
                .ToList();
            foreach (var payment in pendingPayments)
            {
                if (payment.Amount > 0
                    && payment.Comment.StartsWith("Pending balance top-up", StringComparison.OrdinalIgnoreCase)
                    && unitOfWork.Users.GetById(payment.UserId) is { } paymentUser)
                {
                    paymentUser.Balance += payment.Amount;
                    paymentUser.LoyaltyTier = BetterTier(paymentUser.LoyaltyTier, GetClientTier(paymentUser.Balance));
                }

                payment.PaymentType = PaymentTypes.Cash;
                payment.Comment = $"{payment.Comment}; confirmed by admin";
            }

            unitOfWork.SaveChanges();
            LoadDatabaseState();

            var currentUser = unitOfWork.Users.GetByIdNoTracking(_currentUserId);
            if (currentUser is not null)
            {
                RefreshClientUx(unitOfWork, currentUser);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°РєСЂС‹С‚РёСЏ РѕРїР»Р°С‚", ex);
        }
    }

    private void SaveSessionClosed(string computerName)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var computer = unitOfWork.Computers.GetByName(computerName);
            if (computer is null)
            {
                return;
            }

            var session = unitOfWork.GameSessions.GetOpenForComputer(computer.Id);
            if (session is not null)
            {
                session.EndTime = DateTime.Now;
                session.Status = SessionStatuses.Closed;
            }

            computer.Status = PcStatuses.Free;
            unitOfWork.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°РєСЂС‹С‚РёСЏ СЃРµСЃСЃРёРё", ex);
        }
    }

    private void SaveSessionExtension(string computerName, decimal amount)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var computer = unitOfWork.Computers.GetByName(computerName);
            if (computer is null)
            {
                return;
            }

            var session = unitOfWork.GameSessions.GetOpenForComputer(computer.Id);
            if (session is null)
            {
                ShowStatus("РЎРµСЃСЃРёСЏ РЅРµ РЅР°Р№РґРµРЅР°", $"{computerName}: РЅРµС‚ РѕС‚РєСЂС‹С‚РѕР№ СЃРµСЃСЃРёРё РґР»СЏ РїСЂРѕРґР»РµРЅРёСЏ.");
                return;
            }

            var paymentUserId = session.UserId;
            session.TotalPrice += amount;

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = paymentUserId,
                Amount = amount,
                PaymentType = PaymentTypes.Online,
                CreatedAt = DateTime.Now,
                Comment = $"Session extension: {computerName}"
            });

            unitOfWork.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РїСЂРѕРґР»РµРЅРёСЏ СЃРµСЃСЃРёРё", ex);
        }
    }

    private void SaveShiftState(bool closeShift)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var shift = unitOfWork.Shifts.GetCurrentOrLatest();

            if (shift is null)
            {
                shift = new Shift
                {
                    Id = unitOfWork.Shifts.GetNextId(item => item.Id),
                    EmployeeName = _currentUserFullName,
                    StartTime = DateTime.Now,
                    CashTotal = _shiftCash
                };
                unitOfWork.Shifts.Add(shift);
            }

            shift.EmployeeName = _currentUserFullName;
            shift.CashTotal = _shiftCash;
            shift.EndTime = closeShift ? DateTime.Now : null;
            unitOfWork.SaveChanges();
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ СЃРјРµРЅС‹", ex);
        }
    }

    private void SaveShiftExpense(decimal amount, string comment)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var paymentUserId = ResolveCurrentOrAdminUserId(unitOfWork);
            if (paymentUserId is null)
            {
                ShowStatus("Р’РѕР№РґРёС‚Рµ РІ СЃРёСЃС‚РµРјСѓ", "Р Р°СЃС…РѕРґ СЃРјРµРЅС‹ РЅРµ СЃРѕС…СЂР°РЅРµРЅ: РЅРµ РЅР°Р№РґРµРЅ РїРѕР»СЊР·РѕРІР°С‚РµР»СЊ РґР»СЏ Р·Р°РїРёСЃРё РїР»Р°С‚РµР¶Р°.");
                return;
            }

            // Demo finance model: negative Payment.Amount marks cash expenses.
            // Income/expense separation is documented in README as a production improvement.
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
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ СЂР°СЃС…РѕРґР°", ex);
        }
    }

    private void SaveTariffRate(string namePart, decimal price)
    {
        try
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
            LoadDatabaseState();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ С‚Р°СЂРёС„Р°", ex);
        }
    }

    private string? PromptText(string title, string prompt, string defaultValue = "")
    {
        var inputBox = new TextBox
        {
            Text = defaultValue,
            MinWidth = 320,
            Margin = new Thickness(0, 8, 0, 14),
            Foreground = (Brush)FindResource("TextBrush"),
            Background = (Brush)FindResource("SurfaceBrush"),
            BorderBrush = (Brush)FindResource("LineBrush"),
            Padding = new Thickness(10, 6, 10, 6)
        };

        var dialog = new Window
        {
            Title = title,
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)FindResource("PanelBrush"),
            Foreground = (Brush)FindResource("TextBrush"),
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    new TextBlock
                    {
                        Text = prompt,
                        Foreground = (Brush)FindResource("TextBrush"),
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 420
                    },
                    inputBox,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children =
                        {
                            new Button
                            {
                                Content = "OK",
                                IsDefault = true,
                                MinWidth = 86,
                                Margin = new Thickness(0, 0, 8, 0),
                                Style = (Style)FindResource("PrimaryButtonStyle")
                            },
                            new Button
                            {
                                Content = "РћС‚РјРµРЅР°",
                                IsCancel = true,
                                MinWidth = 86,
                                Style = (Style)FindResource("GhostButtonStyle")
                            }
                        }
                    }
                }
            }
        };

        if (dialog.Content is StackPanel panel
            && panel.Children.OfType<StackPanel>().LastOrDefault() is { } buttons)
        {
            foreach (var button in buttons.Children.OfType<Button>())
            {
                if (button.IsDefault)
                {
                    button.Click += (_, _) => dialog.DialogResult = true;
                }
            }
        }

        inputBox.SelectAll();
        return dialog.ShowDialog() == true ? inputBox.Text.Trim() : null;
    }

    private bool PromptMoney(string title, string prompt, string defaultValue, out decimal amount)
    {
        amount = 0m;
        var raw = PromptText(title, prompt, defaultValue);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (TryParseMoney(raw, out amount))
        {
            return true;
        }

        ShowStatus("РќРµРєРѕСЂСЂРµРєС‚РЅР°СЏ СЃСѓРјРјР°", "Р’РІРµРґРёС‚Рµ РїРѕР»РѕР¶РёС‚РµР»СЊРЅРѕРµ С‡РёСЃР»Рѕ, РЅР°РїСЂРёРјРµСЂ 18 РёР»Рё 18,50.");
        return false;
    }

    private string GetFirstFreeComputerName()
    {
        return _computers.FirstOrDefault(computer => NormalizePcStatus(computer.Status) == PcStatuses.Free)?.Name ?? "STD-01";
    }

    private string GetFirstServiceComputerName()
    {
        return _computers.FirstOrDefault(computer => NormalizePcStatus(computer.Status) == PcStatuses.Service)?.Name ?? "STD-07";
    }

    private string? GetFirstPendingPaymentComputerName()
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            return unitOfWork.GameSessions.GetFirstPendingPaymentComputerName(DateTime.Now);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РїРѕРёСЃРєР° РѕРїР»Р°С‚С‹", ex);
            return null;
        }
    }

    private void ChangeTariffManually(string tariffName, int currentRate)
    {
        if (!PromptMoney($"{tariffName}: С‚Р°СЂРёС„", "Р’РІРµРґРёС‚Рµ РЅРѕРІСѓСЋ С†РµРЅСѓ BYN/С‡Р°СЃ:", currentRate.ToString(System.Globalization.CultureInfo.InvariantCulture), out var price))
        {
            return;
        }

        var roundedPrice = Math.Round(price, 0);
        SaveTariffRate(tariffName, roundedPrice);
        RefreshAdminUx();
        AddAdminLog($"{tariffName} rate changed to {roundedPrice:0} BYN/h");
        ShowStatus($"{tariffName} РѕР±РЅРѕРІР»РµРЅ", $"РќРѕРІС‹Р№ С‚Р°СЂРёС„ {tariffName}: {roundedPrice:0} BYN/С‡Р°СЃ. РњРµС‚СЂРёРєРё РїРµСЂРµСЃС‡РёС‚Р°РЅС‹.");
    }

    private void UpsertManualShift(string employeeName)
    {
        try
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
                    CashTotal = _shiftCash
                };
                unitOfWork.Shifts.Add(shift);
            }
            else
            {
                shift.CashTotal = _shiftCash;
            }

            unitOfWork.SaveChanges();
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ СЂР°СЃРїРёСЃР°РЅРёСЏ", ex);
        }
    }

    private string SaveShiftReport()
    {
        var reportsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Reports");
        System.IO.Directory.CreateDirectory(reportsDir);
        var path = System.IO.Path.Combine(reportsDir, $"shift-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines = new[]
        {
            $"Shift report: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Admin: {_currentUserFullName}",
            $"Cash: {_shiftCash:0.##} BYN",
            $"Online: {_shiftOnline:0.##} BYN",
            $"Active sessions: {_adminActiveSessions}",
            $"Pending payments: {_adminPaymentQueue}",
            $"Support queue: {_adminSupportQueue}"
        };
        System.IO.File.WriteAllLines(path, lines);
        return path;
    }

    private string SaveOwnerReport()
    {
        var reportsDir = System.IO.Path.Combine(AppContext.BaseDirectory, "Reports");
        System.IO.Directory.CreateDirectory(reportsDir);
        var path = System.IO.Path.Combine(reportsDir, $"owner-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        var lines = new[]
        {
            $"Owner report: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"Revenue: {_ownerRevenue} BYN",
            $"Load: {_ownerLoad}%",
            $"Average check: {_ownerAverageCheck} BYN",
            $"Repeat rate: {_ownerRepeatRate}%",
            $"Rates: Standard {_standardRate}, VIP {_vipRate}, Bootcamp {_bootcampRate}, Royal {_royalRate} BYN/h"
        };
        System.IO.File.WriteAllLines(path, lines);
        return path;
    }

    private void RescheduleBookingManually()
    {
        var bookingIdText = PromptText("РџРµСЂРµРЅРµСЃС‚Рё Р±СЂРѕРЅСЊ", "Р’РІРµРґРёС‚Рµ ID Р±СЂРѕРЅРё:", GetLatestActiveBookingId().ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!int.TryParse(bookingIdText, out var bookingId))
        {
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РёР·РјРµРЅРµРЅР°", "Р’РІРµРґРёС‚Рµ С‡РёСЃР»РѕРІРѕР№ ID Р±СЂРѕРЅРё.");
            return;
        }

        var startText = PromptText("РџРµСЂРµРЅРµСЃС‚Рё Р±СЂРѕРЅСЊ", "РќРѕРІРѕРµ РЅР°С‡Р°Р»Рѕ РІ С„РѕСЂРјР°С‚Рµ yyyy-MM-dd HH:mm:", DateTime.Now.AddHours(1).ToString("yyyy-MM-dd HH:00"));
        if (!DateTime.TryParse(startText, out var newStart))
        {
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РёР·РјРµРЅРµРЅР°", "РќРµ СѓРґР°Р»РѕСЃСЊ СЂР°СЃРїРѕР·РЅР°С‚СЊ РґР°С‚Сѓ Рё РІСЂРµРјСЏ РЅР°С‡Р°Р»Р°.");
            return;
        }

        var durationText = PromptText("РџРµСЂРµРЅРµСЃС‚Рё Р±СЂРѕРЅСЊ", "Р”Р»РёС‚РµР»СЊРЅРѕСЃС‚СЊ РІ С‡Р°СЃР°С…:", "2");
        if (!double.TryParse(
                durationText?.Replace(',', '.'),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var durationHours)
            || durationHours <= 0)
        {
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РёР·РјРµРЅРµРЅР°", "Р’РІРµРґРёС‚Рµ РїРѕР»РѕР¶РёС‚РµР»СЊРЅСѓСЋ РґР»РёС‚РµР»СЊРЅРѕСЃС‚СЊ РІ С‡Р°СЃР°С….");
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var booking = unitOfWork.Bookings.GetById(bookingId);
            if (booking is null || booking.Status == BookingStatuses.Cancelled)
            {
                ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РЅР°Р№РґРµРЅР°", $"Р‘СЂРѕРЅСЊ #{bookingId} РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚ РёР»Рё СѓР¶Рµ РѕС‚РјРµРЅРµРЅР°.");
                return;
            }

            var newEnd = newStart.AddHours(durationHours);
            var hasConflict = unitOfWork.Bookings.HasTimeConflict(booking.ComputerId, newStart, newEnd, booking.Id)
                || unitOfWork.GameSessions.HasTimeConflict(booking.ComputerId, newStart, newEnd);

            if (hasConflict)
            {
                ShowStatus("РљРѕРЅС„Р»РёРєС‚ СЂР°СЃРїРёСЃР°РЅРёСЏ", "РќР° РІС‹Р±СЂР°РЅРЅРѕРµ РІСЂРµРјСЏ СѓР¶Рµ РµСЃС‚СЊ Р±СЂРѕРЅСЊ РёР»Рё СЃРµСЃСЃРёСЏ РЅР° СЌС‚РѕРј РџРљ.");
                return;
            }

            booking.StartTime = newStart;
            booking.EndTime = newEnd;
            booking.CreatedAt = DateTime.Now;
            unitOfWork.SaveChanges();
            LoadDatabaseState();
            AddAdminLog($"Booking #{bookingId} rescheduled");
            ShowStatus("Р‘СЂРѕРЅСЊ РїРµСЂРµРЅРµСЃРµРЅР°", $"Р‘СЂРѕРЅСЊ #{bookingId}: {newStart:dd.MM HH:mm}-{newEnd:HH:mm}.");
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РїРµСЂРµРЅРѕСЃР° Р±СЂРѕРЅРё", ex);
        }
    }

    private void CancelBookingManually()
    {
        var bookingIdText = PromptText("РћС‚РјРµРЅРёС‚СЊ Р±СЂРѕРЅСЊ", "Р’РІРµРґРёС‚Рµ ID Р±СЂРѕРЅРё:", GetLatestActiveBookingId().ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!int.TryParse(bookingIdText, out var bookingId))
        {
            ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РѕС‚РјРµРЅРµРЅР°", "Р’РІРµРґРёС‚Рµ С‡РёСЃР»РѕРІРѕР№ ID Р±СЂРѕРЅРё.");
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var booking = unitOfWork.Bookings.GetById(bookingId);
            if (booking is null || booking.Status == BookingStatuses.Cancelled)
            {
                ShowStatus("Р‘СЂРѕРЅСЊ РЅРµ РЅР°Р№РґРµРЅР°", $"Р‘СЂРѕРЅСЊ #{bookingId} РѕС‚СЃСѓС‚СЃС‚РІСѓРµС‚ РёР»Рё СѓР¶Рµ РѕС‚РјРµРЅРµРЅР°.");
                return;
            }

            booking.Status = BookingStatuses.Cancelled;
            unitOfWork.SaveChanges();
            LoadDatabaseState();
            AddAdminLog($"Booking #{bookingId} cancelled");
            ShowStatus("Р‘СЂРѕРЅСЊ РѕС‚РјРµРЅРµРЅР°", $"Р‘СЂРѕРЅСЊ #{bookingId} РѕС‚РјРµРЅРµРЅР° Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂРѕРј.");
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° РѕС‚РјРµРЅС‹ Р±СЂРѕРЅРё", ex);
        }
    }

    private int GetLatestActiveBookingId()
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            return unitOfWork.Bookings
                .QueryNoTracking()
                .Where(booking => booking.Status != BookingStatuses.Cancelled)
                .OrderByDescending(booking => booking.CreatedAt)
                .Select(booking => booking.Id)
                .FirstOrDefault();
        }
        catch
        {
            return 0;
        }
    }

    private void AdminAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        var action = element.Tag?.ToString() ?? "admin-action";
        if (HandleAdminSessionAction(action))
        {
            return;
        }

        switch (action)
        {
            case "admin-new-session":
                var sessionComputer = PromptText("РќРѕРІР°СЏ СЃРµСЃСЃРёСЏ", "Р’РІРµРґРёС‚Рµ РёРјСЏ РџРљ РґР»СЏ Р·Р°РїСѓСЃРєР° СЃРµСЃСЃРёРё:", GetFirstFreeComputerName());
                if (string.IsNullOrWhiteSpace(sessionComputer))
                {
                    break;
                }

                if (!PromptMoney("РќРѕРІР°СЏ СЃРµСЃСЃРёСЏ", "Р’РІРµРґРёС‚Рµ СЃСѓРјРјСѓ РѕРїР»Р°С‚С‹:", "8", out var sessionAmount))
                {
                    break;
                }

                if (SaveGuestSession(sessionComputer, sessionAmount))
                {
                    _shiftCash += sessionAmount;
                    SetPcStatus(sessionComputer, PcStatuses.Busy);
                    RefreshAdminUx();
                    AddAdminLog($"{sessionComputer} started as guest session");
                    ShowStatus("РќРѕРІР°СЏ СЃРµСЃСЃРёСЏ", $"Р—Р°РїСѓС‰РµРЅР° РіРѕСЃС‚РµРІР°СЏ СЃРµСЃСЃРёСЏ РЅР° {sessionComputer}. РљР°СЂС‚Р° Рё Р±СЂРѕРЅСЊ РѕР±РЅРѕРІР»РµРЅС‹.");
                }
                break;

            case "admin-payment":
            case "admin-pay-std10":
                var paymentComputer = PromptText("РћС‚РјРµС‚РёС‚СЊ РѕРїР»Р°С‚Сѓ", "Р’РІРµРґРёС‚Рµ РџРљ РѕР¶РёРґР°СЋС‰РµР№ РѕРїР»Р°С‚С‹:", GetFirstPendingPaymentComputerName() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(paymentComputer))
                {
                    PayAdminSession(paymentComputer);
                }
                break;

            case "admin-settle-all":
                if (!PromptMoney("Р—Р°РєСЂС‹С‚СЊ РІСЃРµ РѕРїР»Р°С‚С‹", "РЎСѓРјРјР° РїРѕ СѓРјРѕР»С‡Р°РЅРёСЋ РґР»СЏ РїР»Р°С‚РµР¶РµР№ Р±РµР· С†РµРЅС‹:", "18", out var settlementAmount))
                {
                    break;
                }

                SaveAllPendingPaymentsAsCash(settlementAmount);
                _adminPaymentQueue = 0;
                RefreshAdminUx();
                AddAdminLog("All pending payments settled");
                ShowStatus("РћРїР»Р°С‚С‹ Р·Р°РєСЂС‹С‚С‹", "Р’СЃРµ РѕР¶РёРґР°СЋС‰РёРµ РїР»Р°С‚РµР¶Рё РѕС‚РјРµС‡РµРЅС‹ РєР°Рє РѕРїР»Р°С‡РµРЅРЅС‹Рµ, РєР°СЃСЃР° РїРµСЂРµСЃС‡РёС‚Р°РЅР°.");
                break;

            case "admin-reschedule-booking":
                RescheduleBookingManually();
                break;

            case "admin-cancel-booking":
                CancelBookingManually();
                break;

            case "admin-service":
                var serviceComputer = PromptText("РџРѕСЃС‚Р°РІРёС‚СЊ РџРљ РІ СЃРµСЂРІРёСЃ", "Р’РІРµРґРёС‚Рµ РёРјСЏ РџРљ:", _selectedMapPc ?? GetFirstFreeComputerName());
                if (!string.IsNullOrWhiteSpace(serviceComputer))
                {
                    SetPcStatus(serviceComputer, PcStatuses.Service);
                    LoadDatabaseState();
                    RefreshAdminUx();
                    AddAdminLog($"{serviceComputer} moved to service");
                    ShowStatus("РЎРµСЂРІРёСЃ", $"{serviceComputer} РїРµСЂРµРІРµРґРµРЅ РІ РѕР±СЃР»СѓР¶РёРІР°РЅРёРµ. РљР°СЂС‚Р° Рё РІС‹Р±РѕСЂ Р±СЂРѕРЅРё РѕР±РЅРѕРІР»РµРЅС‹.");
                }
                break;

            case "admin-clear-service":
                var clearServiceComputer = PromptText("РЎРЅСЏС‚СЊ СЃРµСЂРІРёСЃ СЃ РџРљ", "Р’РІРµРґРёС‚Рµ РёРјСЏ РџРљ:", GetFirstServiceComputerName());
                if (!string.IsNullOrWhiteSpace(clearServiceComputer))
                {
                    SetPcStatus(clearServiceComputer, PcStatuses.Free);
                    LoadDatabaseState();
                    RefreshAdminUx();
                    AddAdminLog($"Service released {clearServiceComputer}");
                    ShowStatus("РЎРµСЂРІРёСЃ СЃРЅСЏС‚", $"{clearServiceComputer} РІРµСЂРЅСѓР»СЃСЏ РёР· РѕР±СЃР»СѓР¶РёРІР°РЅРёСЏ Рё РґРѕСЃС‚СѓРїРµРЅ РґР»СЏ Р±СЂРѕРЅРё.");
                }
                break;

            case "shift-close":
                _shiftClosed = !_shiftClosed;
                SaveShiftState(_shiftClosed);
                RefreshAdminUx();
                AddAdminLog(_shiftClosed ? "Shift closed" : "Shift reopened");
                ShowStatus(_shiftClosed ? "РЎРјРµРЅР° Р·Р°РєСЂС‹С‚Р°" : "РЎРјРµРЅР° СЃРЅРѕРІР° Р°РєС‚РёРІРЅР°", _shiftClosed ? "РљР°СЃСЃР° Р·Р°Р±Р»РѕРєРёСЂРѕРІР°РЅР° РґР»СЏ РЅРѕРІС‹С… СЂР°СЃС…РѕРґРѕРІ, РѕС‚С‡РµС‚ РіРѕС‚РѕРІ." : "РћРїРµСЂР°С†РёРё СЃРјРµРЅС‹ СЃРЅРѕРІР° РґРѕСЃС‚СѓРїРЅС‹.");
                break;

            case "shift-expense":
                if (_shiftClosed)
                {
                    ShowStatus("РЎРјРµРЅР° Р·Р°РєСЂС‹С‚Р°", "РќРµР»СЊР·СЏ РІРЅРµСЃС‚Рё СЂР°СЃС…РѕРґ РїРѕСЃР»Рµ Р·Р°РєСЂС‹С‚РёСЏ СЃРјРµРЅС‹.");
                    break;
                }
                if (!PromptMoney("Р’РЅРµСЃС‚Рё СЂР°СЃС…РѕРґ", "Р’РІРµРґРёС‚Рµ СЃСѓРјРјСѓ СЂР°СЃС…РѕРґР°:", "35", out var expenseAmount))
                {
                    break;
                }

                var expenseComment = PromptText("Р’РЅРµСЃС‚Рё СЂР°СЃС…РѕРґ", "РљРѕРјРјРµРЅС‚Р°СЂРёР№ Рє СЂР°СЃС…РѕРґСѓ:", "Shift expense: СЂР°СЃС…РѕРґРЅРёРєРё");
                if (string.IsNullOrWhiteSpace(expenseComment))
                {
                    break;
                }

                _shiftCash = Math.Max(0, _shiftCash - expenseAmount);
                SaveShiftExpense(expenseAmount, expenseComment);
                RefreshAdminUx();
                AddAdminLog($"Expense added: -{expenseAmount:0.##} BYN");
                ShowStatus("Р Р°СЃС…РѕРґ РІРЅРµСЃРµРЅ", $"Р’ РєР°СЃСЃСѓ РґРѕР±Р°РІР»РµРЅ СЂР°СЃС…РѕРґ: -{expenseAmount:0.##} BYN.");
                break;

            case "shift-report":
                var shiftReportPath = SaveShiftReport();
                AddAdminLog("Shift report generated");
                ShowStatus("РћС‚С‡РµС‚ СЃРјРµРЅС‹", $"РљР°СЃСЃР°: {_shiftCash:0} BYN, РѕРЅР»Р°Р№РЅ: {_shiftOnline:0} BYN. Р¤Р°Р№Р»: {shiftReportPath}");
                break;

            case "shift-incident":
                var incidentText = PromptText("Р”РѕР±Р°РІРёС‚СЊ РёРЅС†РёРґРµРЅС‚", "Р’РІРµРґРёС‚Рµ С‚РµРєСЃС‚ Р·Р°РїРёСЃРё:", "Р СѓС‡РЅР°СЏ Р·Р°РїРёСЃСЊ СЃРјРµРЅС‹ РґРѕР±Р°РІР»РµРЅР° Р°РґРјРёРЅРёСЃС‚СЂР°С‚РѕСЂРѕРј");
                if (string.IsNullOrWhiteSpace(incidentText))
                {
                    break;
                }

                AddIncident($"{DateTime.Now:HH:mm} В· {incidentText}");
                _adminSupportQueue++;
                RefreshAdminUx();
                AddAdminLog("Incident added to shift journal");
                ShowStatus("РРЅС†РёРґРµРЅС‚ РґРѕР±Р°РІР»РµРЅ", "Р—Р°РїРёСЃСЊ РїРѕСЏРІРёР»Р°СЃСЊ РІ Р¶СѓСЂРЅР°Р»Рµ, РѕС‡РµСЂРµРґСЊ РїРѕРґРґРµСЂР¶РєРё СѓРІРµР»РёС‡РµРЅР°.");
                break;

            case "owner-peak":
                _ownerDemandMode = _ownerDemandMode == "peak" ? "normal" : "peak";
                if (_ownerDemandMode == "peak")
                {
                    _vipRate = Math.Max(_vipRate, 16);
                    _royalRate = Math.Max(_royalRate, 28);
                    SaveTariffRate("VIP", _vipRate);
                    SaveTariffRate("Royal", _royalRate);
                }
                RefreshAdminUx();
                AddAdminLog($"Owner scenario applied: {_ownerDemandMode}");
                ShowStatus("Р РµР¶РёРј СЃРїСЂРѕСЃР°", $"РђРєС‚РёРІРЅС‹Р№ СЂРµР¶РёРј: {_ownerDemandMode}. РњРµС‚СЂРёРєРё РїРµСЂРµСЃС‡РёС‚Р°РЅС‹ РёР· С‚Р°СЂРёС„РѕРІ Рё Р·Р°РіСЂСѓР·РєРё.");
                break;

            case "owner-night":
                _ownerDemandMode = _ownerDemandMode == "night" ? "normal" : "night";
                if (_ownerDemandMode == "night")
                {
                    _standardRate = 7;
                    SaveTariffRate("Standard", _standardRate);
                }
                RefreshAdminUx();
                AddAdminLog($"Owner scenario applied: {_ownerDemandMode}");
                ShowStatus("Р РµР¶РёРј СЃРїСЂРѕСЃР°", $"РђРєС‚РёРІРЅС‹Р№ СЂРµР¶РёРј: {_ownerDemandMode}. РњРµС‚СЂРёРєРё РїРµСЂРµСЃС‡РёС‚Р°РЅС‹ Р±РµР· СЂСѓС‡РЅРѕРіРѕ РЅР°РєСЂСѓС‡РёРІР°РЅРёСЏ.");
                break;

            case "owner-export":
                var ownerReportPath = SaveOwnerReport();
                AddAdminLog("Owner report exported");
                ShowStatus("РћС‚С‡РµС‚ РІР»Р°РґРµР»СЊС†Р°", $"РЎРІРѕРґРєР°: РІС‹СЂСѓС‡РєР° {_ownerRevenue} BYN, Р·Р°РіСЂСѓР·РєР° {_ownerLoad}%. Р¤Р°Р№Р»: {ownerReportPath}");
                break;

            case "owner-schedule":
                var employeeName = PromptText("Р Р°СЃРїРёСЃР°РЅРёРµ СЃРјРµРЅ", "Р’РІРµРґРёС‚Рµ СЃРѕС‚СЂСѓРґРЅРёРєР° РґР»СЏ РЅРѕРІРѕР№/РѕР±РЅРѕРІР»РµРЅРЅРѕР№ СЃРјРµРЅС‹:", _currentUserFullName);
                if (string.IsNullOrWhiteSpace(employeeName))
                {
                    break;
                }

                UpsertManualShift(employeeName);
                _ownerDemandMode = "loyalty";
                LoadDatabaseState();
                RefreshAdminUx();
                AddAdminLog($"Staff schedule updated for {employeeName}");
                ShowStatus("Р Р°СЃРїРёСЃР°РЅРёРµ РѕР±РЅРѕРІР»РµРЅРѕ", $"РЎРјРµРЅР° РґР»СЏ {employeeName} СЃРѕС…СЂР°РЅРµРЅР° РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….");
                break;

            case "owner-standard":
                ChangeTariffManually("Standard", _standardRate);
                break;

            case "owner-vip":
                ChangeTariffManually("VIP", _vipRate);
                break;

            case "owner-bootcamp":
                ChangeTariffManually("Bootcamp", _bootcampRate);
                break;

            case "owner-royal":
                ChangeTariffManually("Royal", _royalRate);
                break;

            default:
                ShowStatus("РљРѕРјР°РЅРґР° РЅРµ РІС‹РїРѕР»РЅРµРЅР°", $"РљРѕРјР°РЅРґР° РёРЅС‚РµСЂС„РµР№СЃР° РЅРµ СЂР°СЃРїРѕР·РЅР°РЅР°: {action}.");
                break;
        }
    }
    private void ShiftTask_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
        {
            return;
        }

        var done = checkBox.IsChecked == true;
        ShowStatus(done ? "Р—Р°РґР°С‡Р° РІС‹РїРѕР»РЅРµРЅР°" : "Р—Р°РґР°С‡Р° РІРѕР·РІСЂР°С‰РµРЅР°", checkBox.Content?.ToString() ?? "Р—Р°РґР°С‡Р° СЃРјРµРЅС‹");
    }

    private bool HandleAdminSessionAction(string action)
    {
        var parts = action.Split('|', 2);
        if (parts.Length != 2)
        {
            return false;
        }

        switch (parts[0])
        {
            case "admin-close-session":
                CloseAdminSession(parts[1]);
                return true;

            case "admin-pay-session":
                PayAdminSession(parts[1]);
                return true;

            case "admin-extend-session":
                ExtendAdminSession(parts[1]);
                return true;

            default:
                return false;
        }
    }

    private void CloseAdminSession(string computerName)
    {
        _adminActiveSessions = Math.Max(0, _adminActiveSessions - 1);
        _adminFreePcs++;
        SaveSessionClosed(computerName);
        SetPcStatus(computerName, PcStatuses.Free);
        RefreshAdminUx();
        AddAdminLog($"{computerName} closed and released");
        ShowStatus("РЎРµСЃСЃРёСЏ Р·Р°РєСЂС‹С‚Р°", $"{computerName} РѕСЃРІРѕР±РѕР¶РґРµРЅ Рё СЃС‚Р°Р» РґРѕСЃС‚СѓРїРµРЅ РЅР° РєР°СЂС‚Рµ РєР»СѓР±Р°.");
    }

    private void PayAdminSession(string computerName)
    {
        var amount = GetOpenSessionAmount(computerName) ?? 0m;
        if (amount <= 0)
        {
            ShowStatus("РћРїР»Р°С‚Р° РЅРµ РЅР°Р№РґРµРЅР°", $"{computerName}: РЅРµС‚ РѕР¶РёРґР°СЋС‰РµР№ РѕРїР»Р°С‚С‹ РІ Р°РєС‚РёРІРЅС‹С… СЃРµСЃСЃРёСЏС….");
            return;
        }

        _adminPaymentQueue = Math.Max(0, _adminPaymentQueue - 1);
        _shiftCash += amount;
        SavePaymentConfirmation(computerName, amount);
        RefreshAdminUx();
        AddAdminLog($"{computerName} payment confirmed");
        ShowStatus("РћРїР»Р°С‚Р° РїСЂРёРЅСЏС‚Р°", $"{computerName}: РєР°СЃСЃР° +{amount:0.##} BYN.");
    }

    private void ExtendAdminSession(string computerName)
    {
        const decimal extensionPrice = 36m;
        _shiftOnline += extensionPrice;
        SaveSessionExtension(computerName, extensionPrice);
        RefreshAdminUx();
        AddAdminLog($"{computerName} extended");
        ShowStatus("РЎРµСЃСЃРёСЏ РїСЂРѕРґР»РµРЅР°", $"{computerName}: РѕРЅР»Р°Р№РЅ +{extensionPrice:0.##} BYN.");
    }

    private decimal? GetOpenSessionAmount(string computerName)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            return unitOfWork.GameSessions.GetOpenSessionAmount(computerName);
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° С‡С‚РµРЅРёСЏ СЃРµСЃСЃРёРё", ex);
            return null;
        }
    }

    private void RefreshAdminUx()
    {
        RecalculateOwnerMetrics();
        SyncAdminViewModel();
        SyncOwnerViewModel();
        RebuildAdminOperationLogList();
        RebuildAdminSessionsGrid();

        AdminActiveSessionsValue.Text = _adminActiveSessions.ToString();
        AdminPaymentQueueValue.Text = _adminPaymentQueue.ToString();
        AdminFreePcsValue.Text = _adminFreePcs.ToString();
        AdminFreePcsHintText.Text = $"РёР· {_computers.Count} СЂР°Р±РѕС‡РёС… РјРµСЃС‚";
        AdminSupportValue.Text = _adminSupportQueue.ToString();
        ShiftCashValue.Text = $"{_shiftCash:0} BYN";
        ShiftOnlineValue.Text = $"{_shiftOnline:0} BYN";
        OwnerRevenueValue.Text = $"{_ownerRevenue:N0} BYN".Replace(',', ' ');
        OwnerLoadValue.Text = $"{_ownerLoad}%";
        OwnerLoadBar.Value = _ownerLoad;
        OwnerAverageValue.Text = $"{_ownerAverageCheck} BYN";
        OwnerRepeatValue.Text = $"{_ownerRepeatRate}%";
        OwnerStandardPriceText.Text = $"{_standardRate} BYN/С‡Р°СЃ В· 14 РџРљ";
        OwnerVipPriceText.Text = $"{_vipRate} BYN/С‡Р°СЃ В· 8 РџРљ";
        OwnerBootcampPriceText.Text = $"{_bootcampRate} BYN/С‡Р°СЃ В· 5 РџРљ";
        OwnerRoyalPriceText.Text = $"{_royalRate} BYN/С‡Р°СЃ В· 5 РџРљ";
        OwnerPeakModeButton.Style = (Style)FindResource(_ownerDemandMode == "peak" ? "PrimaryButtonStyle" : "GhostButtonStyle");
        OwnerNightModeButton.Style = (Style)FindResource(_ownerDemandMode == "night" ? "PrimaryButtonStyle" : "GhostButtonStyle");
    }

    private void RebuildAdminSessionsGrid()
    {
        if (!IsLoaded || AdminSessionsGrid is null)
        {
            return;
        }

        while (AdminSessionsGrid.Children.Count > 4)
        {
            AdminSessionsGrid.Children.RemoveAt(4);
        }

        while (AdminSessionsGrid.RowDefinitions.Count > 1)
        {
            AdminSessionsGrid.RowDefinitions.RemoveAt(AdminSessionsGrid.RowDefinitions.Count - 1);
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var sessions = unitOfWork.GameSessions.GetActive(DateTime.Now);
            var computers = unitOfWork.Computers.GetDictionaryNoTracking();
            var users = unitOfWork.Users.QueryNoTracking().ToDictionary(user => user.Id);

            if (sessions.Count == 0)
            {
                AdminSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var emptyText = new TextBlock
                {
                    Text = "РќРµС‚ Р°РєС‚РёРІРЅС‹С… СЃРµСЃСЃРёР№ РІ Р±Р°Р·Рµ РґР°РЅРЅС‹С….",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    Margin = new Thickness(0, 14, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                };
                Grid.SetRow(emptyText, 1);
                Grid.SetColumnSpan(emptyText, 5);
                AdminSessionsGrid.Children.Add(emptyText);
                return;
            }

            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var row = i + 1;
                computers.TryGetValue(session.ComputerId, out var computer);
                users.TryGetValue(session.UserId, out var user);

                var computerName = computer?.Name ?? $"РџРљ-{session.ComputerId}";
                var clientName = user?.FullName ?? $"User #{session.UserId}";
                var endText = session.EndTime?.ToString("HH:mm") ?? "РѕС‚РєСЂС‹С‚Р°";
                var statusText = FormatAdminSessionStatus(session.Status);
                var statusBrush = ResolveAdminSessionStatusBrush(session.Status);

                AdminSessionsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                AddAdminSessionCell(row, 0, computerName, "TextBrush", FontWeights.Bold);
                AddAdminSessionCell(row, 1, clientName, "MutedBrush", FontWeights.Normal);
                AddAdminSessionCell(row, 2, endText, "TextBrush", FontWeights.Normal);
                AddAdminSessionCell(row, 3, statusText, statusBrush, FontWeights.Bold);
                AddAdminSessionButton(row, computerName, session.Status);
            }
        }
        catch (Exception ex)
        {
            ShowDatabaseError("РћС€РёР±РєР° Р·Р°РіСЂСѓР·РєРё СЃРµСЃСЃРёР№", ex);
        }
    }

    private void AddAdminSessionCell(int row, int column, string text, string brushKey, FontWeight weight)
    {
        var block = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource(brushKey),
            FontWeight = weight,
            Margin = new Thickness(0, 14, 0, 0),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        AdminSessionsGrid.Children.Add(block);
    }

    private void AddAdminSessionButton(int row, string computerName, string status)
    {
        var isAwaitingPayment = string.Equals(status, SessionStatuses.AwaitingPayment, StringComparison.OrdinalIgnoreCase);
        var isTeamSession = string.Equals(status, SessionStatuses.Team, StringComparison.OrdinalIgnoreCase);
        var button = new Button
        {
            Content = isAwaitingPayment ? "РћРїР»Р°С‚Р°" : isTeamSession ? "РџСЂРѕРґР»РёС‚СЊ" : "Р—Р°РєСЂС‹С‚СЊ",
            Style = (Style)FindResource(isAwaitingPayment ? "PrimaryButtonStyle" : "GhostButtonStyle"),
            Tag = isAwaitingPayment
                ? $"admin-pay-session|{computerName}"
                : isTeamSession
                    ? $"admin-extend-session|{computerName}"
                    : $"admin-close-session|{computerName}",
            MinHeight = 30,
            Padding = new Thickness(14, 0, 14, 0),
            Margin = new Thickness(10, 8, 0, 0)
        };
        button.Click += AdminAction_Click;
        Grid.SetRow(button, row);
        Grid.SetColumn(button, 4);
        AdminSessionsGrid.Children.Add(button);
    }

    private static string FormatAdminSessionStatus(string status)
    {
        return status switch
        {
            SessionStatuses.AwaitingPayment => "РћР¶РёРґР°РµС‚",
            SessionStatuses.Team => "РљРѕРјР°РЅРґР°",
            SessionStatuses.Active => "РћРїР»Р°С‡РµРЅРѕ",
            _ => status
        };
    }

    private static string ResolveAdminSessionStatusBrush(string status)
    {
        return status switch
        {
            SessionStatuses.AwaitingPayment => "WaitBrush",
            SessionStatuses.Team => "GoldLightBrush",
            SessionStatuses.Active => "OkBrush",
            _ => "MutedBrush"
        };
    }

    private void RecalculateOwnerMetrics()
    {
        var totalPcs = Math.Max(1, _computers.Count);
        var occupiedPcs = Math.Clamp(totalPcs - _adminFreePcs - _adminSupportQueue, 0, totalPcs);
        var demandMultiplier = _ownerDemandMode switch
        {
            "peak" => 1.18m,
            "night" => 0.88m,
            "loyalty" => 1.05m,
            _ => 1m
        };
        var loadBonus = _ownerDemandMode switch
        {
            "peak" => 7,
            "night" => 3,
            "loyalty" => 2,
            _ => 0
        };

        var standardRevenue = 14 * 3.2m * _standardRate;
        var vipRevenue = 8 * 2.8m * _vipRate;
        var royalRevenue = 5 * 2.4m * _royalRate;
        var bootcampRevenue = _bootcampRate * 0.75m;
        var packageRevenue = _shiftOnline * 0.35m;
        var pendingPenalty = _adminPaymentQueue * 12m;
        var servicePenalty = _adminSupportQueue * 18m;

        _ownerRevenue = (int)Math.Round((standardRevenue + vipRevenue + royalRevenue + bootcampRevenue + packageRevenue + _shiftCash - pendingPenalty - servicePenalty) * demandMultiplier);
        _ownerLoad = Math.Clamp((int)Math.Round(occupiedPcs * 100m / totalPcs) + loadBonus, 0, 100);

        var paidSessions = Math.Max(1, _adminActiveSessions - _adminPaymentQueue);
        _ownerAverageCheck = Math.Max(0, (int)Math.Round(_ownerRevenue / (decimal)paidSessions));
        _ownerRepeatRate = Math.Clamp(58 + (_ownerDemandMode == "loyalty" ? 6 : 0) + (_ownerDemandMode == "night" ? 3 : 0) - Math.Max(0, _adminSupportQueue - 3), 0, 99);
    }
    private void AddIncident(string text)
    {
        var row = new TextBlock
        {
            Text = text,
            Foreground = (Brush)FindResource("MutedBrush"),
            Margin = new Thickness(0, 0, 0, 10)
        };
        ShiftIncidentList.Children.Insert(Math.Min(1, ShiftIncidentList.Children.Count), row);
    }

    private void AddAdminLog(string text)
    {
        if (!IsLoaded)
        {
            return;
        }

        var row = new TextBlock
        {
            Text = $"{DateTime.Now:HH:mm} В· {text}",
            Foreground = (Brush)FindResource("MutedBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 6)
        };

        AdminOperationLogList.Children.Insert(0, row);
        while (AdminOperationLogList.Children.Count > 6)
        {
            AdminOperationLogList.Children.RemoveAt(AdminOperationLogList.Children.Count - 1);
        }

        SaveAdminLogEntry(text);
    }

    private void RebuildAdminOperationLogList()
    {
        if (!IsLoaded || AdminOperationLogList is null)
        {
            return;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            var logs = unitOfWork.Payments.GetAdminLogs(6);

            AdminOperationLogList.Children.Clear();
            if (logs.Count == 0)
            {
                AdminOperationLogList.Children.Add(new TextBlock
                {
                    Text = "Р–СѓСЂРЅР°Р» РѕРїРµСЂР°С†РёР№ РїСѓСЃС‚",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextWrapping = TextWrapping.Wrap
                });
                return;
            }

            foreach (var log in logs)
            {
                AdminOperationLogList.Children.Add(new TextBlock
                {
                    Text = $"{log.CreatedAt:HH:mm} В· {FormatAdminLogComment(log.Comment)}",
                    Foreground = (Brush)FindResource("MutedBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 6)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Admin log load failed: {ex}");
        }
    }

    private void SaveAdminLogEntry(string text)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var userId = ResolveCurrentOrAdminUserId(unitOfWork);
            if (userId is null)
            {
                return;
            }

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = userId.Value,
                Amount = 0,
                PaymentType = PaymentTypes.AdminLog,
                CreatedAt = DateTime.Now,
                Comment = $"Admin log: {text}"
            });
            unitOfWork.SaveChanges();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Admin log save failed: {ex}");
        }
    }

    private static string FormatAdminLogComment(string comment)
    {
        return comment.StartsWith("Admin log:", StringComparison.OrdinalIgnoreCase)
            ? comment["Admin log:".Length..].Trim()
            : comment;
    }

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T typed)
            {
                return typed;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

}
