using System;
using System.Globalization;
using System.Linq;
using VictusLounge.Helpers;
using VictusLounge.Models;
using VictusLounge.Repositories;
using VictusLounge.Services;

namespace VictusLounge.Services.Facades;

public sealed class TopupFacade
{
    public TopupSummary BuildSummary(TopupSummaryRequest request)
    {
        if (!TryReadAmount(request.AmountText, out var amount))
        {
            return new TopupSummary("0 BYN", "Введите сумму больше 0", 0, 0, GetClientTier(request.UserId));
        }

        var user = TryGetUser(request.UserId);
        var tier = user is null ? "Bronze" : GetClientTier(user);
        var promoCode = TryGetPromoCode(request.AppliedPromoCode);
        var bonus = CalculateTopupBonus(amount, tier, promoCode);
        var bonusText = bonus > 0
            ? promoCode is not null
                ? $"+{bonus:0.##} бонусов по промокоду"
                : $"+{bonus:0.##} бонусов по статусу {tier}"
            : "Бонусы начисляются от 50 BYN";

        return new TopupSummary($"{amount:0.##} BYN", bonusText, amount, bonus, tier);
    }

    public TopupFacadeResult ConfirmTopup(TopupFacadeRequest request)
    {
        if (!TryReadAmount(request.AmountText, out var amount))
        {
            return TopupFacadeResult.Fail("Введите корректную сумму пополнения.");
        }

        if (request.UserId <= 0)
        {
            return TopupFacadeResult.Fail("Войдите в систему перед пополнением баланса.");
        }

        if (request.Method != "card")
        {
            return SavePendingTopupRequest(request.UserId, amount, request.Method);
        }

        if (!IsValidPaymentCardNumber(request.CardNumber))
        {
            return TopupFacadeResult.Fail("Введите корректный номер карты.");
        }

        return SaveBalanceTopup(request.UserId, amount, request.AppliedPromoCode);
    }

    private static TopupFacadeResult SaveBalanceTopup(int userId, decimal amount, string? appliedPromoCode)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var user = unitOfWork.Users.FirstOrDefault(item => item.Id == userId);
            if (user is null)
            {
                return TopupFacadeResult.Fail("Пользователь не найден.");
            }

            var promoCode = TryGetPromoCode(appliedPromoCode);
            var tier = GetClientTier(user);
            var bonus = CalculateTopupBonus(amount, tier, promoCode);
            var bonusSource = promoCode is not null ? $"promo {promoCode.Code}" : $"tier {tier}";
            user.Balance += amount + bonus;
            user.LoyaltyTier = tier;

            var nextPaymentId = unitOfWork.Payments.GetNextId(payment => payment.Id);
            unitOfWork.Payments.Add(new Payment
            {
                Id = nextPaymentId++,
                UserId = user.Id,
                Amount = amount,
                PaymentType = PaymentTypes.Card,
                CreatedAt = DateTime.Now,
                Comment = bonus > 0
                    ? $"Balance top-up. Bonus added to balance: {bonus:0.##} BYN via {bonusSource}"
                    : "Balance top-up"
            });

            if (bonus > 0)
            {
                unitOfWork.Payments.Add(new Payment
                {
                    Id = nextPaymentId,
                    UserId = user.Id,
                    Amount = bonus,
                    PaymentType = PaymentTypes.Bonus,
                    CreatedAt = DateTime.Now,
                    Comment = $"Top-up bonus from {bonusSource}: {amount:0.##} BYN"
                });
            }

            unitOfWork.SaveChanges();
            return TopupFacadeResult.Ok(amount, bonus, user.Balance, TopupOperation.Card);
        }
        catch (Exception ex)
        {
            return TopupFacadeResult.Fail("Не удалось обновить баланс в базе данных.", ex);
        }
    }

    private static TopupFacadeResult SavePendingTopupRequest(int userId, decimal amount, string method)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            if (!unitOfWork.Users.Any(user => user.Id == userId))
            {
                return TopupFacadeResult.Fail("Пользователь не найден.");
            }

            unitOfWork.Payments.Add(new Payment
            {
                Id = unitOfWork.Payments.GetNextId(payment => payment.Id),
                UserId = userId,
                Amount = amount,
                PaymentType = method == "erip" ? PaymentTypes.PendingErip : PaymentTypes.PendingCash,
                CreatedAt = DateTime.Now,
                Comment = "Pending balance top-up request"
            });

            unitOfWork.SaveChanges();
            return TopupFacadeResult.Ok(amount, 0, null, method == "erip" ? TopupOperation.Erip : TopupOperation.Cash);
        }
        catch (Exception ex)
        {
            return TopupFacadeResult.Fail("Не удалось сохранить заявку в базе данных.", ex);
        }
    }

    private static bool TryReadAmount(string amountText, out decimal amount)
    {
        return decimal.TryParse(
            amountText.Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }

    private static decimal CalculateTopupBonus(decimal amount, string tier, PromoCode? promoCode)
    {
        if (amount < 50)
        {
            return 0;
        }

        if (promoCode is not null)
        {
            return amount >= promoCode.MinTopupAmount ? Math.Round(amount * promoCode.TopupBonusRate, 2) : 0;
        }

        return Math.Round(amount * GetTierTopupBonusRate(tier), 2);
    }

    private static PromoCode? TryGetPromoCode(string? appliedPromoCode)
    {
        if (string.IsNullOrWhiteSpace(appliedPromoCode))
        {
            return null;
        }

        try
        {
            using var unitOfWork = new UnitOfWork();
            return unitOfWork.PromoCodes.GetActiveByCode(appliedPromoCode);
        }
        catch
        {
            return null;
        }
    }

    private static User? TryGetUser(int userId)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            return unitOfWork.Users.GetByIdNoTracking(userId);
        }
        catch
        {
            return null;
        }
    }

    private static string GetClientTier(int userId)
    {
        try
        {
            using var unitOfWork = new UnitOfWork();
            var sessions = unitOfWork.GameSessions
                .QueryNoTracking()
                .Where(session => session.UserId == userId)
                .ToList();
            return LoyaltyTierService.GetTier(LoyaltyTierService.CalculatePlayedHours(sessions));
        }
        catch
        {
            return "Bronze";
        }
    }

    private static string GetClientTier(User user)
    {
        var tier = GetClientTier(user.Id);
        return string.IsNullOrWhiteSpace(tier) ? "Bronze" : tier;
    }

    private static decimal GetTierTopupBonusRate(string tier)
    {
        return LoyaltyTierService.GetTopupBonusRate(tier);
    }

    private static bool IsValidPaymentCardNumber(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 12)
        {
            return false;
        }

        var sum = 0;
        var doubleDigit = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var value = digits[i] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}

public sealed record TopupSummaryRequest(
    int UserId,
    decimal CurrentBalance,
    string AmountText,
    string? AppliedPromoCode);

public sealed record TopupSummary(
    string SummaryText,
    string BonusText,
    decimal Amount,
    decimal Bonus,
    string Tier);

public sealed record TopupFacadeRequest(
    int UserId,
    string AmountText,
    string Method,
    string CardNumber,
    string? AppliedPromoCode);

public sealed record TopupFacadeResult(
    bool Success,
    string? ErrorMessage,
    Exception? Exception,
    decimal Amount,
    decimal Bonus,
    decimal? NewBalance,
    TopupOperation Operation)
{
    public static TopupFacadeResult Ok(decimal amount, decimal bonus, decimal? newBalance, TopupOperation operation)
    {
        return new TopupFacadeResult(true, null, null, amount, bonus, newBalance, operation);
    }

    public static TopupFacadeResult Fail(string errorMessage, Exception? exception = null)
    {
        return new TopupFacadeResult(false, errorMessage, exception, 0, 0, null, TopupOperation.None);
    }
}

public enum TopupOperation
{
    None,
    Card,
    Erip,
    Cash
}
