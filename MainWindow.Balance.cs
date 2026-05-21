using System;
using System.Globalization;
using System.Linq;

namespace VictusLounge;

public partial class MainWindow
{
    private static bool TryParseMoney(string raw, out decimal amount)
    {
        var digits = new string(raw.Where(ch => char.IsDigit(ch) || ch is '.' or ',').ToArray());
        return decimal.TryParse(
            digits.Replace(',', '.'),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out amount) && amount > 0;
    }

    private static decimal CalculateTopupBonus(decimal amount)
    {
        return amount >= 50 ? Math.Round(amount * 0.1m, 2) : 0;
    }

    private static bool IsValidDemoCardNumber(string raw)
    {
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.Length < 12)
        {
            return false;
        }

        var sum = 0;
        var doubleDigit = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var value = digits[i] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9)
                {
                    value -= 9;
                }
            }

            sum += value;
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }
}
