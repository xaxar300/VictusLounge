namespace VictusLounge.Helpers;

public static class PcStatuses
{
    public const string Free = "free";
    public const string Busy = "busy";
    public const string Reserved = "reserved";
    public const string Service = "service";
}

public static class BookingStatuses
{
    public const string Confirmed = "Confirmed";
    public const string PendingPayment = "PendingPayment";
    public const string Cancelled = "Cancelled";
}

public static class SessionStatuses
{
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string AwaitingPayment = "AwaitingPayment";
    public const string Team = "Team";
}

public static class PaymentTypes
{
    public const string Bonus = "Bonus";
    public const string Card = "Card";
    public const string Cash = "Cash";
    public const string Online = "Online";
    public const string Pending = "Pending";
    public const string PendingErip = "PendingErip";
    public const string PendingCash = "PendingCash";
    public const string EventRegistration = "EventRegistration";
    public const string AdminLog = "AdminLog";
}
