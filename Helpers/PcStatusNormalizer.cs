namespace VictusLounge.Helpers;

public static class PcStatusNormalizer
{
    public static string Normalize(string status)
    {
        return status.Trim().ToLowerInvariant() switch
        {
            "available" => PcStatuses.Free,
            "active" => PcStatuses.Busy,
            "occupied" => PcStatuses.Busy,
            "maintenance" => PcStatuses.Service,
            "" => PcStatuses.Free,
            var value => value
        };
    }
}
