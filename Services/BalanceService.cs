using VictusLounge.Models;
using VictusLounge.Repositories;

namespace VictusLounge.Services;

public sealed class BalanceService
{
    public PromoCode? GetActivePromoCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        using var unitOfWork = new UnitOfWork();
        return unitOfWork.PromoCodes.GetActiveByCode(code.Trim());
    }
}
