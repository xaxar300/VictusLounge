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
        private static readonly Brush FreeBrush = new SolidColorBrush(Color.FromRgb(0x68, 0xD8, 0x89));
        private static readonly Brush BusyBrush = new SolidColorBrush(Color.FromRgb(0xE6, 0x6B, 0x6B));
        private static readonly Brush ReservedBrush = new SolidColorBrush(Color.FromRgb(0xED, 0xC8, 0x6B));
        private static readonly Brush ServiceBrush = new SolidColorBrush(Color.FromRgb(0xA9, 0xA0, 0x92));

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
            Binding.DoNothing;
    }
}
