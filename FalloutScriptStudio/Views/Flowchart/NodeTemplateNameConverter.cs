namespace FalloutScriptStudio.Views.Flowchart
{
	using System;
	using System.Globalization;
	using System.Windows.Data;
	using Localization;

	/// <summary>
	/// Turns the name of a code template of the new node dialog (which comes from the core library, in English) into the text in the
	/// language of the interface.
	/// </summary>
	public sealed class NodeTemplateNameConverter : IValueConverter
	{
		private const String EmptyName = "<Empty>";
		private const String CodeNamePrefix = "Template Code ";

		/// <inheritdoc />
		public Object Convert(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			String name = value as String ?? String.Empty;
			if (String.Equals(name, EmptyName, StringComparison.Ordinal))
			{
				return Loc.Get("Str.Flowchart.Template.Empty");
			}

			if (name.StartsWith(CodeNamePrefix, StringComparison.Ordinal))
			{
				return Loc.Format("Str.Flowchart.Template.Code", name.Substring(CodeNamePrefix.Length));
			}

			return name;
		}

		/// <inheritdoc />
		public Object ConvertBack(Object? value, Type targetType, Object? parameter, CultureInfo culture)
		{
			throw new NotSupportedException();
		}
	}
}