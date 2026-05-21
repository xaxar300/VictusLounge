using System;

namespace VictusLounge.Models;

public class Shift
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal CashTotal { get; set; }
}
