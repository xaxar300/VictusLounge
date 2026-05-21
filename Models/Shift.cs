using System;

namespace VictusLounge.Models;

public class Shift
{
    public int Id { get; set; }
    // Demo simplification: shifts store employee display names instead of a User FK.
    // This keeps the coursework schema simple while preserving readable shift history.
    public string EmployeeName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal CashTotal { get; set; }
}
