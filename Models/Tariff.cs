namespace VictusLounge.Models;

public class Tariff
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PricePerHour { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
