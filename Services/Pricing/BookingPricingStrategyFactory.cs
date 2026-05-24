namespace VictusLounge.Services.Pricing;

public static class BookingPricingStrategyFactory
{
    public static IBookingPricingStrategy Create(string? package, string tier = "Bronze")
    {
        return package switch
        {
            "night" => new NightPackPricingStrategy(),
            "morning" => new MorningPackPricingStrategy(),
            _ => new DefaultBookingPricingStrategy(tier)
        };
    }
}
