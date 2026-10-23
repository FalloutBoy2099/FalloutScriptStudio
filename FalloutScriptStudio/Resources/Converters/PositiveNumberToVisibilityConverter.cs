namespace FalloutScriptStudio.Resources.Converters
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>Shows an element only when the bound number is greater than a pixel (for example the scrollable width of a viewer: the arrows of a tab strip appear only when the tabs do not fit).</summary>
	public sealed class PositiveNumberToVisibilityConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value is Double number && number > 1 ? Visibility.Visible : Visibility.Collapsed;
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}