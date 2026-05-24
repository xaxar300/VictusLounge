using VictusLounge.Helpers;

namespace VictusLounge;

public partial class MainWindow
{
    private static bool TryParseMoney(string raw, out decimal amount)
    {
        return MoneyFormatter.TryParsePositive(raw, out amount);
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
}
