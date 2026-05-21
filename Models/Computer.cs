namespace VictusLounge.Models;

public class Computer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string Specs { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal HourPrice { get; set; }
}
