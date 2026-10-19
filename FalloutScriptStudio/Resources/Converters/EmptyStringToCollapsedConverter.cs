namespace FalloutScriptStudio.Resources.Converters
{
	using System;
	using System.Globalization;
	using System.Windows;
	using System.Windows.Data;

	/// <summary>Collapses an element when the bound text is null or empty.</summary>
	public sealed class EmptyStringToCollapsedConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value is String { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}