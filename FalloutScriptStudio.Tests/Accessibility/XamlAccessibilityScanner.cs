namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml;
	using System.Xml.Linq;

	/// <summary>
	/// Static accessibility rules applied to the markup of a view: accessible names, keyboard reachability, hard coded colors and
	/// the dialog conventions (default and cancel button, initial focus). Every rule returns readable findings
	/// ("file(line): message") so that a failing test points at the markup to fix.
	/// </summary>
	internal sealed class XamlAccessibilityScanner
	{
		/// <summary>Controls that show their own content as the accessible name, so an icon only content is not enough.</summary>
		private static readonly HashSet<String> _buttonLike = new HashSet<String>(StringComparer.Ordinal)
		{
			"Button", "ToggleButton", "CheckBox", "RadioButton", "HyperlinkButton", "RepeatButton", "ToggleSwitch",
		};

		/// <summary>Controls that have no text of their own and need a name or a label.</summary>
		private static readonly HashSet<String> _inputLike = new HashSet<String>(StringComparer.Ordinal)
		{
			"TextBox", "PasswordBox", "NumberBox", "ComboBox", "OverrideModeBox", "ListBox", "ListView", "TreeView", "DataGrid", "Slider",
			"AutoSuggestBox", "TextEditor", "ProgressBar", "DiagramCanvas",
		};

		/// <summary>Controls that can be labelled by the title of a settings card.</summary>
		private static readonly HashSet<String> _cardLabelled = new HashSet<String>(StringComparer.Ordinal)
		{
			"TextBox", "PasswordBox", "NumberBox", "ComboBox", "OverrideModeBox", "ListBox", "Slider", "ToggleSwitch", "CheckBox",
		};

		private static readonly HashSet<String> _mouseOnlyHosts = new HashSet<String>(StringComparer.Ordinal)
		{
			"TextBlock", "Border", "Grid", "StackPanel", "Image", "Rectangle", "SymbolIcon", "Ellipse", "DockPanel", "WrapPanel",
		};

		private static readonly String[] _mouseHandlers =
		{
			"MouseLeftButtonUp", "MouseLeftButtonDown", "MouseDown", "MouseUp", "PreviewMouseLeftButtonUp", "PreviewMouseLeftButtonDown",
			"MouseDoubleClick",
		};

		private static readonly HashSet<String> _colorProperties = new HashSet<String>(StringComparer.Ordinal)
		{
			"Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "CaretBrush", "SelectionBrush", "OpacityMask",
		};

		/// <summary>
		/// Windows where Escape belongs to the editor inside (it dismisses the completion list and the tips), so the window is closed with
		/// its Done button and not with Escape.
		/// </summary>
		private static readonly HashSet<String> _escapeBelongsToEditor = new HashSet<String>(StringComparer.Ordinal) { "NodeCodeEditorWindow.xaml" };

		private static readonly Regex _hexColor = new Regex(@"^#[0-9A-Fa-f]{3,8}$", RegexOptions.Compiled);
		private static readonly Regex _namedColor = new Regex(@"^[A-Za-z]+$", RegexOptions.Compiled);

		private readonly List<XDocument> _resourceDocuments = new List<XDocument>();

		/// <summary>Initializes a new instance of the <see cref="XamlAccessibilityScanner"/> class.</summary>
		public XamlAccessibilityScanner()
		{
			this._resourceDocuments.Add(SourceTree.Load(Path.Combine(SourceTree.AppFolder, "Resources", "ShellResources.xaml")));
			this._resourceDocuments.Add(SourceTree.Load(Path.Combine(SourceTree.AppFolder, "App.xaml")));
		}

		/// <summary>Finds interactive controls without an accessible name, and icon only buttons.</summary>
		public IReadOnlyList<String> FindMissingNames(String file)
		{
			return this.FindMissingNames(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindMissingNames(String file, XDocument document)
		{
			List<String> findings = new List<String>();
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

		/// <summary>Finds interactive controls that cannot be reached with the Tab key, and mouse only click handlers.</summary>
		public IReadOnlyList<String> FindUnreachableControls(String file)
		{
			return this.FindUnreachableControls(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindUnreachableControls(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (XElement element in document.Descendants())
			{
				if (InsideControlTemplate(element))
				{
					continue;
				}

				String name = element.Name.LocalName;
				if (_buttonLike.Contains(name) || _inputLike.Contains(name))
				{
					Boolean inItemTemplate = element.Ancestors().Any(ancestor => ancestor.Name.LocalName is "DataTemplate" or "HierarchicalDataTemplate");
					if (!inItemTemplate
						&& (String.Equals((String?)element.Attribute("Focusable"), "False", StringComparison.OrdinalIgnoreCase)
							|| String.Equals((String?)element.Attribute("IsTabStop"), "False", StringComparison.OrdinalIgnoreCase)))
					{
						findings.Add(Describe(file, element, "interactive control that is not a tab stop"));
					}
				}
				else if (_mouseOnlyHosts.Contains(name) && _mouseHandlers.Any(handler => element.Attribute(handler) is not null))
				{
					findings.Add(Describe(file, element, "click handler on an element that cannot take the keyboard focus, use a button"));
				}
			}

			return findings;
		}

		/// <summary>Finds hard coded colors: text, background and border colors must come from the theme resources.</summary>
		public IReadOnlyList<String> FindHardCodedColors(String file)
		{
			return this.FindHardCodedColors(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindHardCodedColors(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (XElement element in document.Descendants())
			{
				foreach (XAttribute attribute in element.Attributes())
				{
					String property = attribute.Name.LocalName;
					if (!_colorProperties.Contains(property) && !(element.Name.LocalName == "Setter" && attribute.Name.LocalName == "Value" && IsColorSetter(element)))
					{
						continue;
					}

					String value = attribute.Value.Trim();
					if (value.Length == 0 || value.StartsWith('{') || String.Equals(value, "Transparent", StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (_hexColor.IsMatch(value) || _namedColor.IsMatch(value))
					{
						findings.Add(Describe(file, element, $"hard coded color {value} in {property}, use a theme brush with DynamicResource"));
					}
				}
			}

			return findings;
		}

		/// <summary>Finds the problems of the conventions of a window: Enter, Escape, initial focus, title.</summary>
		public IReadOnlyList<String> FindWindowConventionViolations(String file)
		{
			return this.FindWindowConventionViolations(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindWindowConventionViolations(String file, XDocument document)
		{
			XElement root = document.Root!;
			List<String> findings = new List<String>();
			Boolean hasDefault = document.Descendants().Any(e => IsTrue(e, "IsDefault"));
			Boolean hasCancel = document.Descendants().Any(e => IsTrue(e, "IsCancel"));
			Boolean closesOnEscape = document.Descendants().Any(e => e.Attributes().Any(a => a.Name.LocalName.EndsWith("CloseOnEscape", StringComparison.Ordinal) && IsTrueText(a.Value)))
				|| root.Attributes().Any(a => a.Name.LocalName.EndsWith("CloseOnEscape", StringComparison.Ordinal));
			Boolean hasFocus = root.Attributes().Any(a => a.Name.LocalName == "FocusedElement" || a.Name.LocalName.EndsWith("FocusedElement", StringComparison.Ordinal))
				|| document.Descendants().Any(e => e.Attributes().Any(a => a.Name.LocalName.EndsWith("FocusedElement", StringComparison.Ordinal)));
			String codeBehind = file + ".cs";
			String code = File.Exists(codeBehind) ? File.ReadAllText(codeBehind) : String.Empty;
			Boolean focusesInCode = code.Contains(".Focus()", StringComparison.Ordinal);
			Boolean escapeInCode = code.Contains("Key.Escape", StringComparison.Ordinal);

			if (root.Attribute("Title") is null)
			{
				findings.Add(Describe(file, root, "window without a Title"));
			}

			if (!hasCancel && !closesOnEscape && !escapeInCode && !_escapeBelongsToEditor.Contains(Path.GetFileName(file)))
			{
				findings.Add(Describe(file, root, "Escape does not close the window: add a button with IsCancel or WindowKeys.CloseOnEscape"));
			}

			if (hasDefault && !hasCancel && !closesOnEscape && !escapeInCode)
			{
				findings.Add(Describe(file, root, "default button without a cancel button"));
			}

			if (!hasFocus && !focusesInCode)
			{
				findings.Add(Describe(file, root, "no initial focus: set FocusManager.FocusedElement on the window or focus the main field in code"));
			}

			return findings;
		}

		/// <summary>Finds windows that would be hard to use at a high display scale or that render blurry lines.</summary>
		public IReadOnlyList<String> FindLayoutProblems(String file, XDocument document)
		{
			XElement root = document.Root!;
			List<String> findings = new List<String>();
			if (!String.Equals((String?)root.Attribute("UseLayoutRounding"), "True", StringComparison.Ordinal) || !String.Equals((String?)root.Attribute("SnapsToDevicePixels"), "True", StringComparison.Ordinal))
			{
				findings.Add(Describe(file, root, "window without UseLayoutRounding and SnapsToDevicePixels set to True"));
			}

			if (Double.TryParse((String?)root.Attribute("Height"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Double height)
				&& height >= 480
				&& !root.Attributes().Any(attribute => attribute.Name.LocalName == "WindowFit.FitToWorkArea"))
			{
				findings.Add(Describe(file, root, "window of " + height + " units without WindowFit.FitToWorkArea: it would not fit on a small screen at 200% scale"));
			}

			foreach (XElement element in document.Descendants().Where(e => e.Attribute("UseLayoutRounding") is not null && e != root))
			{
				if (String.Equals((String?)element.Attribute("UseLayoutRounding"), "False", StringComparison.Ordinal))
				{
					findings.Add(Describe(file, element, "UseLayoutRounding turned off below the window"));
				}
			}

			return findings;
		}

		/// <summary>Finds the windows whose Tab order does not follow the reading order of their grid cells.</summary>
		public IReadOnlyList<String> FindReadingOrderProblems(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (XElement grid in document.Descendants().Where(e => e.Name.LocalName == "Grid"))
			{
				(Int32 Row, Int32 Column)? previous = null;
				foreach (XElement child in grid.Elements().Where(c => !c.Name.LocalName.Contains('.', StringComparison.Ordinal)))
				{
					// Popups and overlays that cover several cells do not take part in the order; an explicit TabIndex is the author's decision.
					if (child.Name.LocalName == "Popup"
						|| child.Attribute("Grid.RowSpan") is not null
						|| child.Attribute("Grid.ColumnSpan") is not null
						|| child.DescendantsAndSelf().Any(e => e.Attribute("TabIndex") is not null)
						|| !child.DescendantsAndSelf().Any(e => _buttonLike.Contains(e.Name.LocalName) || _inputLike.Contains(e.Name.LocalName) && e.Name.LocalName != "ProgressBar"))
					{
						continue;
					}

					(Int32 Row, Int32 Column) position = (CellOf(child, "Grid.Row"), CellOf(child, "Grid.Column"));
					if (previous is { } last && (position.Row < last.Row || (position.Row == last.Row && position.Column < last.Column)))
					{
						findings.Add(Describe(file, child, "comes after a control that sits further down or to the right in the same grid, so the Tab order differs from the reading order"));
					}

					previous = position;
				}
			}

			return findings;
		}

		private static Int32 CellOf(XElement element, String attribute)
		{
			return Int32.TryParse((String?)element.Attribute(attribute), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out Int32 value) ? value : 0;
		}

		/// <summary>
		/// Finds lists and trees that can be operated with the mouse only: a tree needs a key handler, and so does a list or a grid whose items
		/// are opened by a double click (unless the window has a default button, which Enter presses).
		/// </summary>
		public IReadOnlyList<String> FindMouseOnlyLists(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			Boolean hasDefaultButton = document.Descendants().Any(e => IsTrue(e, "IsDefault"));
			foreach (XElement element in document.Descendants().Where(e => e.Name.LocalName is "TreeView" or "DataGrid" or "ListBox" or "ListView"))
			{
				Boolean handlesKeys = element.Attribute("KeyDown") is not null || element.Attribute("PreviewKeyDown") is not null;
				if (element.Name.LocalName == "TreeView" && !handlesKeys)
				{
					findings.Add(Describe(file, element, "tree without a key handler: Enter, F2 and Delete have to work like the mouse and the context menu"));
				}
				else if (element.Attribute("MouseDoubleClick") is not null && !handlesKeys && !hasDefaultButton)
				{
					findings.Add(Describe(file, element, "opened by a double click only: handle Enter"));
				}
			}

			return findings;
		}

		/// <summary>Finds styles that remove the keyboard focus rectangle.</summary>
		public IReadOnlyList<String> FindHiddenFocusVisuals(String file)
		{
			return this.FindHiddenFocusVisuals(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindHiddenFocusVisuals(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (XElement element in document.Descendants())
			{
				XAttribute? attribute = element.Attribute("FocusVisualStyle");
				if (attribute is not null && attribute.Value.Contains("x:Null", StringComparison.Ordinal))
				{
					findings.Add(Describe(file, element, "FocusVisualStyle is removed, the keyboard focus would be invisible"));
				}

				if (element.Name.LocalName == "Setter" && (String?)element.Attribute("Property") == "FocusVisualStyle"
					&& ((String?)element.Attribute("Value"))?.Contains("x:Null", StringComparison.Ordinal) == true)
				{
					findings.Add(Describe(file, element, "FocusVisualStyle is removed, the keyboard focus would be invisible"));
				}
			}

			return findings;
		}

		/// <summary>
		/// Finds the styles that replace the template of a control without being based on the WPF-UI style: they lose its focus rectangle,
		/// and the system one is black, so they have to name the theme's focus visual.
		/// </summary>
		public IReadOnlyList<String> FindStylesWithoutFocusVisual(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (XElement style in document.Descendants().Where(e => e.Name.LocalName == "Style"))
			{
				String target = TargetTypeName((String?)style.Attribute("TargetType"));
				if (!(_buttonLike.Contains(target) || target is "ListBoxItem" or "TreeViewItem" or "ComboBox" or "TabItem" or "ListViewItem")
					|| style.Attribute("BasedOn") is not null)
				{
					continue;
				}

				IEnumerable<XElement> setters = style.Elements().Where(e => e.Name.LocalName == "Setter");
				if (setters.Any(setter => (String?)setter.Attribute("Property") == "Template") && !setters.Any(setter => (String?)setter.Attribute("Property") == "FocusVisualStyle"))
				{
					findings.Add(Describe(file, style, "style with its own template and no FocusVisualStyle: set DefaultControlFocusVisualStyle so that the focus is visible in both themes"));
				}
			}

			return findings;
		}

		/// <summary>Finds font sizes that are too small to read comfortably.</summary>
		public IReadOnlyList<String> FindSmallFonts(String file, Double minimum)
		{
			return this.FindSmallFonts(file, SourceTree.Load(file), minimum);
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindSmallFonts(String file, XDocument document, Double minimum)
		{
			List<String> findings = new List<String>();
			foreach (XElement element in document.Descendants())
			{
				String? value = element.Name.LocalName == "Setter" && (String?)element.Attribute("Property") == "FontSize"
					? (String?)element.Attribute("Value")
					: (String?)element.Attribute("FontSize");
				if (value is not null && Double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Double size) && size < minimum)
				{
					findings.Add(Describe(file, element, $"font size {value} is below {minimum}"));
				}
			}

			return findings;
		}

		/// <summary>Finds two key bindings with the same gesture in one file.</summary>
		public static IReadOnlyList<String> FindDuplicateGestures(String file)
		{
			return FindDuplicateGestures(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public static IReadOnlyList<String> FindDuplicateGestures(String file, XDocument document)
		{
			List<String> findings = new List<String>();
			foreach (IGrouping<String, XElement> group in document.Descendants()
				.Where(e => e.Name.LocalName == "KeyBinding")
				.GroupBy(e => GestureOf(e) + "@" + ScopeOf(e), StringComparer.Ordinal)
				.Where(g => g.Count() > 1))
			{
				findings.Add(Describe(file, group.First(), "the gesture " + group.Key + " is bound twice"));
			}

			return findings;
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

		/// <summary>Gets the gesture of a key binding written with <c>Key</c> and <c>Modifiers</c> or with <c>Gesture</c>.</summary>
		public static String GestureOf(XElement binding)
		{
			String? gesture = (String?)binding.Attribute("Gesture");
			if (gesture is not null)
			{
				return gesture.Replace(" ", String.Empty, StringComparison.Ordinal);
			}

			String? modifiers = (String?)binding.Attribute("Modifiers");
			String key = (String?)binding.Attribute("Key") ?? String.Empty;
			return modifiers is null ? key : modifiers + "+" + key;
		}

		private static String ScopeOf(XElement binding)
		{
			XElement? owner = binding.Parent?.Parent;
			return owner is null ? String.Empty : owner.Name.LocalName + "#" + ((String?)owner.Attribute(SourceTree.Xaml + "Name") ?? String.Empty);
		}

		private static Boolean IsTrue(XElement element, String attribute)
		{
			return IsTrueText((String?)element.Attribute(attribute));
		}

		private static Boolean IsTrueText(String? value)
		{
			return String.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
		}

		private static Boolean IsColorSetter(XElement setter)
		{
			String? property = (String?)setter.Attribute("Property");
			return property is not null && _colorProperties.Contains(property);
		}

		private static Boolean InsideControlTemplate(XElement element)
		{
			return element.Ancestors().Any(ancestor => ancestor.Name.LocalName is "ControlTemplate");
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
			return style is not null && this.StyleProvidesName(style, keyed, new HashSet<XElement>());
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
