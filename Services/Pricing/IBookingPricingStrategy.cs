namespace VictusLounge.Services.Pricing;

public interface IBookingPricingStrategy
{
    string Label { get; }
    string Description { get; }
    decimal Calculate(BookingPriceContext context);
}
