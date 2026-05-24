using VictusLounge.Services;

namespace VictusLounge.Services.Pricing;

public sealed class DefaultBookingPricingStrategy : IBookingPricingStrategy
{
    private readonly string _tier;
    private readonly decimal _discountRate;

    public DefaultBookingPricingStrategy(string tier = "Bronze")
    {
        _tier = tier;
        _discountRate = LoyaltyTierService.GetBookingDiscountRate(tier);
    }

    public string Label => LoyaltyTierService.GetBookingDiscountLabel(_tier);

    public string Description => _discountRate > 0
        ? $"Regular tariff with {_tier} {_discountRate * 100:0}% discount."
        : "Regular tariff without loyalty discount.";

    public decimal Calculate(BookingPriceContext context)
    {
        return context.HourPrice * context.Duration * context.SeatsCount * (1 - _discountRate);
    }
}
