using VictusLounge.Models;

namespace VictusLounge.Services;

public static class LoyaltyTierService
{
    public const double SilverHours = 10;
    public const double GoldHours = 30;
    public const double EliteHours = 60;

    public static double CalculatePlayedHours(IEnumerable<GameSession> sessions)
    {
        return sessions
            .Where(session => session.EndTime is not null)
            .Sum(session => Math.Max(0, (session.EndTime!.Value - session.StartTime).TotalHours));
    }

    public static string GetTier(double playedHours)
    {
        return playedHours switch
        {
            >= EliteHours => "Elite",
            >= GoldHours => "Gold",
            >= SilverHours => "Silver",
            _ => "Bronze"
        };
    }

    public static int GetRank(string tier)
    {
        return tier switch
        {
            "Elite" => 3,
            "Gold" => 2,
            "Silver" => 1,
            _ => 0
        };
    }

    public static decimal GetTopupBonusRate(string tier)
    {
        return tier switch
        {
            "Elite" => 0.15m,
            "Gold" => 0.1m,
            "Silver" => 0.05m,
            _ => 0m
        };
    }

    public static decimal GetBookingDiscountRate(string tier)
    {
        return tier switch
        {
            "Elite" => 0.15m,
            "Gold" => 0.1m,
            "Silver" => 0.05m,
            _ => 0m
        };
    }

    public static string GetBookingDiscountLabel(string tier)
    {
        var rate = GetBookingDiscountRate(tier);
        return rate > 0 ? $"{tier} -{rate * 100:0}%" : "Без скидки статуса";
    }

    public static double GetNextThreshold(string tier)
    {
        return tier switch
        {
            "Bronze" => SilverHours,
            "Silver" => GoldHours,
            "Gold" => EliteHours,
            _ => EliteHours
        };
    }

    public static string GetNextTier(string tier)
    {
        return tier switch
        {
            "Bronze" => "Silver",
            "Silver" => "Gold",
            "Gold" => "Elite",
            _ => "Elite"
        };
    }
}
