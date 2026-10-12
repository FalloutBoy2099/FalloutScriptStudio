namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;

	/// <content>Lookup of the keyed and implicit styles of a document.</content>
	internal sealed partial class XamlAccessibilityScanner
	{
		private static HashSet<String> LabelTargets(XDocument document)
		{
			HashSet<String> targets = new HashSet<String>(StringComparer.Ordinal);
			foreach (XElement label in document.Descendants().Where(e => e.Name.LocalName == "Label"))
			{
				String? target = (String?)label.Attribute("Target");
				Match match = Regex.Match(target ?? String.Empty, @"ElementName=([A-Za-z0-9_]+)");
				if (match.Success)
				{
					targets.Add(match.Groups[1].Value);
				}
			}

			return targets;
		}

		private static Dictionary<String, XElement> KeyedStyles(XDocument document)
		{
			Dictionary<String, XElement> styles = new Dictionary<String, XElement>(StringComparer.Ordinal);
			foreach (XElement style in document.Descendants().Where(e => e.Name.LocalName == "Style"))
			{
				String? key = (String?)style.Attribute(SourceTree.Xaml + "Key");
				if (key is not null)
				{
					styles[key] = style;
				}
			}

			return styles;
		}

		private static Dictionary<String, XElement> ImplicitStyles(XDocument document)
		{
			Dictionary<String, XElement> styles = new Dictionary<String, XElement>(StringComparer.Ordinal);
			foreach (XElement style in document.Descendants().Where(e => e.Name.LocalName == "Style" && e.Attribute(SourceTree.Xaml + "Key") is null))
			{
				String target = TargetTypeName((String?)style.Attribute("TargetType"));
				if (target.Length > 0)
				{
					styles[target] = style;
				}
			}

			return styles;
		}

		private static String TargetTypeName(String? targetType)
		{
			if (targetType is null)
			{
				return String.Empty;
			}

			String text = targetType.Replace("{x:Type", String.Empty, StringComparison.Ordinal).Replace("}", String.Empty, StringComparison.Ordinal).Trim();
			Int32 colon = text.IndexOf(':', StringComparison.Ordinal);
			return colon >= 0 ? text[(colon + 1)..] : text;
		}
	}
}
