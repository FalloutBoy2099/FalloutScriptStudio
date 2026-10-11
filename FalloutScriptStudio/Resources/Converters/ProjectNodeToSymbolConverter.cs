namespace FalloutScriptStudio.Resources.Converters
{
	using System;
	using System.Globalization;
	using System.Windows.Data;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.ViewModels.Panels;
	using Wpf.Ui.Controls;

	/// <summary>Maps a node of the project explorer to its glyph.</summary>
	public sealed class ProjectNodeToSymbolConverter : IValueConverter
	{
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			if (value is not ProjectNodeViewModel node)
			{
				return SymbolRegular.Document24;
			}

			if (node.IsDirectory)
			{
				return node.IsExpanded ? SymbolRegular.FolderOpen24 : SymbolRegular.Folder24;
			}

			if (node.IsCompiled)
			{
				return SymbolRegular.Box24;
			}

			if (node.IsFlowchart)
			{
				return SymbolRegular.Organization24;
			}

			return node.Kind switch
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
