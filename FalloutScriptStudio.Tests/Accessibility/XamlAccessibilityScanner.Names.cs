namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;

	/// <content>Rule: interactive controls need an accessible name.</content>
	internal sealed partial class XamlAccessibilityScanner
	{
		/// <summary>Controls that show their own content as the accessible name, so an icon only content is not enough.</summary>
		private static readonly HashSet<String> _buttonLike = new(StringComparer.Ordinal)
		{
			"Button", "ToggleButton", "CheckBox", "RadioButton", "HyperlinkButton", "RepeatButton", "ToggleSwitch",
		};

		/// <summary>Controls that have no text of their own and need a name or a label.</summary>
		private static readonly HashSet<String> _inputLike = new(StringComparer.Ordinal)
		{
			"TextBox", "PasswordBox", "NumberBox", "ComboBox", "OverrideModeBox", "ListBox", "ListView", "TreeView", "DataGrid", "Slider",
			"AutoSuggestBox", "TextEditor", "ProgressBar", "DiagramCanvas",
		};

		/// <summary>Controls that can be labelled by the title of a settings card.</summary>
		private static readonly HashSet<String> _cardLabelled = new(StringComparer.Ordinal)
		{
			"TextBox", "PasswordBox", "NumberBox", "ComboBox", "OverrideModeBox", "ListBox", "Slider", "ToggleSwitch", "CheckBox",
		};

		/// <summary>Finds interactive controls without an accessible name, and icon only buttons.</summary>
		public IReadOnlyList<String> FindMissingNames(String file)
		{
			return this.FindMissingNames(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindMissingNames(String file, XDocument document)
		{
			List<String> findings = [];
			HashSet<String> labelTargets = LabelTargets(document);
			Dictionary<String, XElement> keyed = KeyedStyles(document);
			Dictionary<String, XElement> implicitStyles = ImplicitStyles(document);

			foreach (XElement element in document.Descendants())
			{
				if (InsideControlTemplate(element))
				{
					continue;
				}

				String name = element.Name.LocalName;
				if (_buttonLike.Contains(name))
				{
					Boolean named = this.HasName(element, keyed, implicitStyles, labelTargets) || LabelledByCard(element);
					if (!named && !HasTextContent(element))
					{
						findings.Add(Describe(file, element, IsIconOnly(element) ? "icon-only button without AutomationProperties.Name" : "button without an accessible name"));
					}
					else if (!named && IsMeaninglessText(element))
					{
						findings.Add(Describe(file, element, "button whose only text is a symbol or an abbreviation, add AutomationProperties.Name"));
					}
				}
				else if (_inputLike.Contains(name))
				{
					if (!this.HasName(element, keyed, implicitStyles, labelTargets) && !LabelledByCard(element))
					{
						findings.Add(Describe(file, element, "control without AutomationProperties.Name or LabeledBy"));
					}
				}
				else if (name == "MenuItem")
				{
					if (!HasMenuHeader(element))
					{
						findings.Add(Describe(file, element, "menu item without a Header"));
					}
				}
				else if (name == "TabItem")
				{
					if (!HasMenuHeader(element))
					{
						findings.Add(Describe(file, element, "tab without a Header"));
					}
				}
				else if (name.EndsWith("Column", StringComparison.Ordinal) && element.Parent is { Name.LocalName: "DataGrid.Columns" })
				{
					this.CheckColumnHeader(file, element, findings);
				}
			}

			return findings;
		}

		private static Boolean LabelledByCard(XElement element)
		{
			if (!_cardLabelled.Contains(element.Name.LocalName))
			{
				return false;
			}

			XElement? card = element.Ancestors().FirstOrDefault(ancestor => ancestor.Name.LocalName == "SettingCard");
			return card is not null && ((String?)card.Attribute("Title"))?.Length > 0;
		}

		private static Boolean HasMenuHeader(XElement element)
		{
			return element.Attribute("Header") is not null
				|| element.Elements().Any(child => child.Name.LocalName.EndsWith(".Header", StringComparison.Ordinal))
				|| element.Attribute("ItemsSource") is not null;
		}

		private static Boolean IsIconOnly(XElement element)
		{
			return !HasTextContent(element) && (element.Attribute("Icon") is not null || element.Elements().Any(child => child.Name.LocalName.EndsWith(".Icon", StringComparison.Ordinal) || child.Name.LocalName == "SymbolIcon"));
		}

		/// <summary>A button shows text when it has a text content, a bound content or a text block child.</summary>
		private static Boolean HasTextContent(XElement element)
		{
			String? content = (String?)element.Attribute("Content");
			if (content is not null)
			{
				return content.Length > 0;
			}

			foreach (XElement descendant in element.Descendants())
			{
				if ((descendant.Name.LocalName is "TextBlock" or "Run" or "AccessText") && descendant.Attribute("Text") is not null)
				{
					return true;
				}
			}

			return element.Elements().Any(child => child.Name.LocalName.EndsWith(".Content", StringComparison.Ordinal))
				|| (!element.HasElements && element.Value.Trim().Length > 0);
		}

		/// <summary>Symbols, glyphs and one or two letters (such as "Aa") are not a usable accessible name.</summary>
		private static Boolean IsMeaninglessText(XElement element)
		{
			String? content = (String?)element.Attribute("Content");
			if (content is null || content.StartsWith('{'))
			{
				return false;
			}

			Int32 letters = content.Count(Char.IsLetter);
			Boolean glyph = content.Any(character => character >= '' && character <= '');
			return glyph || letters < 3;
		}

		private Boolean HasName(XElement element, Dictionary<String, XElement> keyed, Dictionary<String, XElement> implicitStyles, HashSet<String> labelTargets)
		{
			if (element.Attribute("AutomationProperties.Name") is not null
				|| element.Attribute("AutomationProperties.LabeledBy") is not null
				|| element.Attributes().Any(attribute => attribute.Name.LocalName == "EditorAutomation.NameResource"))
			{
				return true;
			}

			String? elementName = (String?)element.Attribute(SourceTree.Xaml + "Name");
			if (elementName is not null && labelTargets.Contains(elementName))
			{
				return true;
			}

			XElement? style = this.ResolveStyle(element, keyed, implicitStyles);
			return style is not null && this.StyleProvidesName(style, keyed, []);
		}

		private XElement? ResolveStyle(XElement element, Dictionary<String, XElement> keyed, Dictionary<String, XElement> implicitStyles)
		{
			String? styleReference = (String?)element.Attribute("Style");
			if (styleReference is not null)
			{
				Match match = Regex.Match(styleReference, @"StaticResource\s+([A-Za-z0-9_.]+)\s*\}");
				if (match.Success)
				{
					return this.FindKeyedStyle(match.Groups[1].Value, keyed);
				}

				return null;
			}

			return implicitStyles.TryGetValue(element.Name.LocalName, out XElement? implicitStyle) ? implicitStyle : null;
		}

		private XElement? FindKeyedStyle(String key, Dictionary<String, XElement> keyed)
		{
			if (keyed.TryGetValue(key, out XElement? local))
			{
				return local;
			}

			foreach (XDocument resources in this._resourceDocuments)
			{
				XElement? style = resources.Descendants().FirstOrDefault(e => e.Name.LocalName == "Style" && (String?)e.Attribute(SourceTree.Xaml + "Key") == key);
				if (style is not null)
				{
					return style;
				}
			}

			return null;
		}

		private Boolean StyleProvidesName(XElement style, Dictionary<String, XElement> keyed, HashSet<XElement> visited)
		{
			if (!visited.Add(style))
			{
				return false;
			}

			if (style.Elements().Any(setter => setter.Name.LocalName == "Setter"
				&& ((String?)setter.Attribute("Property")) is "AutomationProperties.Name" or "AutomationProperties.LabeledBy"))
			{
				return true;
			}

			String? basedOn = (String?)style.Attribute("BasedOn");
			if (basedOn is not null)
			{
				Match match = Regex.Match(basedOn, @"StaticResource\s+([A-Za-z0-9_.]+)\s*\}");
				if (match.Success)
				{
					XElement? parent = this.FindKeyedStyle(match.Groups[1].Value, keyed);
					return parent is not null && this.StyleProvidesName(parent, keyed, visited);
				}
			}

			return false;
		}

		private void CheckColumnHeader(String file, XElement column, List<String> findings)
		{
			String columnName = column.Name.LocalName;
			XElement? headerElement = column.Elements().FirstOrDefault(child => child.Name.LocalName == columnName + ".Header");
			if (column.Attribute("Header") is null && headerElement is null)
			{
				findings.Add(Describe(file, column, "grid column without a header"));
				return;
			}

			XElement? text = headerElement?.Elements().FirstOrDefault();
			if (text is { Name.LocalName: "TextBlock" } && String.IsNullOrWhiteSpace((String?)text.Attribute("Text")) && text.Attribute("AutomationProperties.Name") is null)
			{
				findings.Add(Describe(file, column, "icon column with a blank header, set AutomationProperties.Name on the header"));
			}
		}
	}
}