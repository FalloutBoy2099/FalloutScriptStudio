namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml;
	using System.Xml.Linq;

	/// <summary>
	/// Static accessibility rules applied to the markup of a view: accessible names, keyboard reachability, hard coded colors and
	/// the dialog conventions (default and cancel button, initial focus). Every rule returns readable findings
	/// ("file(line): message") so that a failing test points at the markup to fix.
	/// </summary>
	internal sealed partial class XamlAccessibilityScanner
	{
		private readonly List<XDocument> _resourceDocuments = new List<XDocument>();

		/// <summary>Initializes a new instance of the <see cref="XamlAccessibilityScanner"/> class.</summary>
		public XamlAccessibilityScanner()
		{
			this._resourceDocuments.Add(SourceTree.Load(Path.Combine(SourceTree.AppFolder, "Resources", "ShellResources.xaml")));
			this._resourceDocuments.Add(SourceTree.Load(Path.Combine(SourceTree.AppFolder, "App.xaml")));
		}

		/// <summary>Gets the text of a header or content attribute of an element, or <see langword="null"/>.</summary>
		public static String? HeaderText(XElement element)
		{
			return (String?)element.Attribute("Header") ?? (String?)element.Attribute("Content");
		}

		/// <summary>Formats a finding.</summary>
		public static String Describe(String file, XElement element, String message)
		{
			String position = element is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture) : "?";
			String hint = (String?)element.Attribute(SourceTree.Xaml + "Name")
				?? (String?)element.Attribute("Content")
				?? (String?)element.Attribute("Header")
				?? (String?)element.Attribute("ToolTip")
				?? (String?)element.Attribute("Text")
				?? (String?)element.Attribute("ItemsSource")
				?? String.Empty;
			return $"{SourceTree.Relative(file)}({position}): <{element.Name.LocalName}> {message} {hint}".TrimEnd();
		}

		private static Boolean IsTrue(XElement element, String attribute)
		{
			return IsTrueText((String?)element.Attribute(attribute));
		}

		private static Boolean IsTrueText(String? value)
		{
			return String.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
		}

		private static Boolean InsideControlTemplate(XElement element)
		{
			return element.Ancestors().Any(ancestor => ancestor.Name.LocalName is "ControlTemplate");
		}
	}
}
