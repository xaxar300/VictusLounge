using System.Globalization;
using System.Linq;

namespace VictusLounge.Helpers;

public static class MoneyFormatter
{
    public static string FormatByn(decimal amount)
    {
        return $"{amount:0.##} BYN";
    }

    public static bool TryParsePositive(string raw, out decimal amount)
    {
        var digits = new string(raw.Where(ch => char.IsDigit(ch) || ch is '.' or ',').ToArray());
        return decimal.TryParse(
            digits.Replace(',', '.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }
}
