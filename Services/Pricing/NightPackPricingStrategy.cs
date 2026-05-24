namespace VictusLounge.Services.Pricing;

public sealed class NightPackPricingStrategy : IBookingPricingStrategy
{
    public string Label => "Night Pack -25%";

    public string Description => "Night Pack: 8 hours, starts at 22:00, 23:00 or 00:00, 25% discount.";

    public decimal Calculate(BookingPriceContext context)
    {
        return context.HourPrice * context.Duration * context.SeatsCount * 0.75m;
    }
}
