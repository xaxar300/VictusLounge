namespace VictusLounge.Services.Pricing;

public sealed class MorningPackPricingStrategy : IBookingPricingStrategy
{
    public string Label => "Morning Pack -20%";

    public string Description => "Morning Pack: 3 hours, starts at 06:00, 07:00 or 08:00, 20% discount.";

    public decimal Calculate(BookingPriceContext context)
    {
        return context.HourPrice * context.Duration * context.SeatsCount * 0.8m;
    }
}
