using System;

namespace VictusLounge.Models;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ComputerId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
