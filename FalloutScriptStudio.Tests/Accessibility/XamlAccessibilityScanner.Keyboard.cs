namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Xml.Linq;

	/// <content>Rules: keyboard reachability, focus visuals and shortcut gestures.</content>
	internal sealed partial class XamlAccessibilityScanner
	{
		private static readonly HashSet<String> _mouseOnlyHosts = new(StringComparer.Ordinal)
		{
			"TextBlock", "Border", "Grid", "StackPanel", "Image", "Rectangle", "SymbolIcon", "Ellipse", "DockPanel", "WrapPanel",
		};

		private static readonly String[] _mouseHandlers =
		{
			"MouseLeftButtonUp", "MouseLeftButtonDown", "MouseDown", "MouseUp", "PreviewMouseLeftButtonUp", "PreviewMouseLeftButtonDown",
			"MouseDoubleClick",
		};

		/// <summary>Finds interactive controls that cannot be reached with the Tab key, and mouse only click handlers.</summary>
		public IReadOnlyList<String> FindUnreachableControls(String file)
		{
			return this.FindUnreachableControls(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public IReadOnlyList<String> FindUnreachableControls(String file, XDocument document)
		{
			List<String> findings = [];
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

		/// <summary>
		/// Finds lists and trees that can be operated with the mouse only: a tree needs a key handler, and so does a list or a grid whose items
		/// are opened by a double click (unless the window has a default button, which Enter presses).
		/// </summary>
		public IReadOnlyList<String> FindMouseOnlyLists(String file, XDocument document)
		{
			List<String> findings = [];
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
			List<String> findings = [];
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
			List<String> findings = [];
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

		/// <summary>Finds two key bindings with the same gesture in one file.</summary>
		public static IReadOnlyList<String> FindDuplicateGestures(String file)
		{
			return FindDuplicateGestures(file, SourceTree.Load(file));
		}

		/// <summary>Same rule on markup that is already loaded.</summary>
		public static IReadOnlyList<String> FindDuplicateGestures(String file, XDocument document)
		{
			List<String> findings = [];
			foreach (IGrouping<String, XElement> group in document.Descendants()
				.Where(e => e.Name.LocalName == "KeyBinding")
				.GroupBy(e => GestureOf(e) + "@" + ScopeOf(e), StringComparer.Ordinal)
				.Where(g => g.Count() > 1))
			{
				findings.Add(Describe(file, group.First(), "the gesture " + group.Key + " is bound twice"));
			}

			return findings;
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
	}
}