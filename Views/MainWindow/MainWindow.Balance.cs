using VictusLounge.Helpers;
using VictusLounge.Services;

namespace VictusLounge;

public partial class MainWindow
{
    private static bool TryParseMoney(string raw, out decimal amount)
    {
        return MoneyFormatter.TryParsePositive(raw, out amount);
    }

    private static decimal GetTierTopupBonusRate(string tier)
    {
        return LoyaltyTierService.GetTopupBonusRate(tier);
    }
}
