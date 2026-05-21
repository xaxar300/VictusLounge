using System;

namespace VictusLounge.Models;

public class Payment
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Comment { get; set; } = string.Empty;
}
