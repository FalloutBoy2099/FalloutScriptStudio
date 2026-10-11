namespace FalloutScriptStudio.Resources.Converters
{
	using System;
	using System.Globalization;
	using System.Windows.Data;
	using FalloutScriptStudio.Documents;
	using Wpf.Ui.Controls;

	/// <summary>Maps the kind of a document to the glyph shown on its tab and in the project explorer.</summary>
	public sealed class DocumentKindToSymbolConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			return value switch
			{
				DocumentKind.Script => SymbolRegular.DocumentText24,
				DocumentKind.Header => SymbolRegular.DocumentHeader24,
				DocumentKind.Message => SymbolRegular.Chat24,
				_ => SymbolRegular.Document24,
			};
		}

		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}
