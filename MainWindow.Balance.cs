using System;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using VictusLounge.Data;

namespace VictusLounge;

public partial class MainWindow
{
    private static bool TryParseMoney(string raw, out decimal amount)
    {
        var digits = new string(raw.Where(ch => char.IsDigit(ch) || ch is '.' or ',').ToArray());
        return decimal.TryParse(
            digits.Replace(',', '.'),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }

    private decimal CalculateTopupBonus(decimal amount)
    {
        if (amount < 50)
        {
            return 0;
        }

        var promoCode = GetAppliedPromoCode();
        if (promoCode is not null)
        {
            return amount >= promoCode.MinTopupAmount ? Math.Round(amount * promoCode.TopupBonusRate, 2) : 0;
        }

        var rate = GetTierTopupBonusRate(GetCurrentClientTier());
        return Math.Round(amount * rate, 2);
    }

    private string GetCurrentClientTier()
    {
        try
        {
            using var dbContext = new AppDbContext();
            var user = dbContext.Users.AsNoTracking().FirstOrDefault(item => item.Id == _currentUserId);
            return user is null ? GetClientTier(_balanceAmount) : GetClientTier(user);
        }
        catch
        {
            return GetClientTier(_balanceAmount);
        }
    }

    private static decimal GetTierTopupBonusRate(string tier)
    {
        return tier switch
        {
            "Elite" => 0.15m,
            "Gold" => 0.1m,
            "Silver" => 0.05m,
            _ => 0m
        };
    }

    private static bool IsValidDemoCardNumber(string raw)
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
