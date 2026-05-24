using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace VictusLounge.Converters
{
    /// <summary>
    /// Converter that translates computer status strings into brush colors.
    /// Expected format: "STD-01|Standard|free" or just "free".
    /// </summary>
    public class StatusToBrushConverter : IValueConverter
    {
        private static readonly Brush FreeBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0xAF, 0x50)); // green
        private static readonly Brush BusyBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x98, 0x00)); // amber
        private static readonly Brush ReservedBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0x44, 0x36)); // red
        private static readonly Brush ServiceBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x9E, 0x9E, 0x9E)); // gray

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Brushes.Transparent;

            string tag = value.ToString() ?? string.Empty;
            string status = tag.Split('|')[^1]; // get last part

            return status switch
            {
                "free"      => FreeBrush,
                "busy"      => BusyBrush,
                "reserved"  => ReservedBrush,
                "service"   => ServiceBrush,
                _           => Brushes.Transparent
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}