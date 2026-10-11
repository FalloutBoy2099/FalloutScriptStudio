namespace FalloutScriptStudio.Resources.Converters
{
	using System;
	using System.Globalization;
	using System.Windows.Data;
	using FalloutScriptStudio.Core.Compilation;
	using Wpf.Ui.Controls;

	/// <summary>Maps the kind of an error list entry to its glyph.</summary>
	public sealed class ErrorTypeToSymbolConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value switch
			{
				BuildErrorType.Error => SymbolRegular.ErrorCircle24,
				BuildErrorType.Warning => SymbolRegular.Warning24,
				BuildErrorType.Search => SymbolRegular.Search24,
				BuildErrorType.Parser => SymbolRegular.Code24,
				_ => SymbolRegular.Info24,
			};
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
