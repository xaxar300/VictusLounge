using System;

namespace VictusLounge.Models;

public class Booking
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int ComputerId { get; set; }
    public Computer Computer { get; set; } = null!;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Package { get; set; } = "regular";
    public DateTime CreatedAt { get; set; }
}
