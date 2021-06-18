using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace MarketInfo.Viewer
{
    public static class EnumHelper
    {
        public static string Description(this Enum e)
        {
            return (e.GetType()
                .GetField(e.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false)
                .FirstOrDefault() as DescriptionAttribute)?.Description ?? e.ToString();
        }
    }

    [ValueConversion(typeof(Enum), typeof(IEnumerable<Tuple<Enum, string>>))]
    public class EnumToCollectionConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Enum.GetValues(value.GetType())
                       .Cast<Enum>()
                       .Select(e => new Tuple<Enum, string>(e, e.Description()))
                       .ToList();
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return null;
        }
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
