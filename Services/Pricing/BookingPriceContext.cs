namespace VictusLounge.Services.Pricing;

public sealed record BookingPriceContext(decimal HourPrice, int Duration, int SeatsCount);
