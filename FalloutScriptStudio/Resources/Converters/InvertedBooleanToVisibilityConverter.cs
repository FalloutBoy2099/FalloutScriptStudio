namespace FalloutScriptStudio.Resources.Converters
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>Shows an element when the bound value is <see langword="false"/>.</summary>
	public sealed class InvertedBooleanToVisibilityConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value is true ? Visibility.Collapsed : Visibility.Visible;
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value is not Visibility.Visible;
		}
	}
}
