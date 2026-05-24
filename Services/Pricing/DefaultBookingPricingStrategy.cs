namespace VictusLounge.Services.Pricing;

public sealed class DefaultBookingPricingStrategy : IBookingPricingStrategy
{
    public string Label => "Gold -10%";

    public string Description => "Regular tariff with Gold 10% discount.";

    public decimal Calculate(BookingPriceContext context)
    {
        return context.HourPrice * context.Duration * context.SeatsCount * 0.9m;
    }
}
