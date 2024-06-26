﻿using System.Globalization;
using System.Windows.Data;

namespace Leosac.KeyManager.Library.UI.Domain
{
    public class OffByOneToBoolConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length != 2 || values[0] is not int value1 || values[1] is not int value2)
            {
                return Binding.DoNothing;
            }

            return Math.Abs(value1 - value2) == 1;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new InvalidOperationException();
        }
    }
}
