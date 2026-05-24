namespace VictusLounge.Services.Pricing;

public static class BookingPricingStrategyFactory
{
    public static IBookingPricingStrategy Create(string? package)
    {
        return package switch
        {
            "night" => new NightPackPricingStrategy(),
            "morning" => new MorningPackPricingStrategy(),
            _ => new DefaultBookingPricingStrategy()
        };
    }
}
