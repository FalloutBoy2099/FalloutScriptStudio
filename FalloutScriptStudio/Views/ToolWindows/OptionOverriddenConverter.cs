namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Windows.Data;

	/// <summary>
	/// Tells whether an option is in the list of the options the open project overrides: the values are the list (the identifiers) and the
	/// identifier of the option of a card. The result is <see langword="false"/> while either is missing.
	/// </summary>
	public sealed class OptionOverriddenConverter : IMultiValueConverter
	{
		/// <inheritdoc/>
		public Object Convert(Object[] values, Type targetType, Object parameter, CultureInfo culture)
		{
			if (values is { Length: >= 2 } && values[0] is IEnumerable<String> options && values[1] is String { Length: > 0 } id)
			{
				return options.Contains(id, StringComparer.Ordinal);
			}

			return false;
		}

		/// <inheritdoc/>
		public Object[] ConvertBack(Object value, Type[] targetTypes, Object parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
