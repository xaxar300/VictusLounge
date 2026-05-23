namespace VictusLounge.Models;

public class PromoCode
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal BookingDiscountRate { get; set; }
    public decimal TopupBonusRate { get; set; }
    public decimal MinTopupAmount { get; set; }
    public bool IsActive { get; set; } = true;
}
