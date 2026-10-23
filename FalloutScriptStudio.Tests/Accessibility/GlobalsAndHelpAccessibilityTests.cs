namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using Xunit;

	/// <summary>
	/// The accessibility rules of <c>docs\CONVENTIONS.md</c> for the Globals tab, the Help tab and the help view of the popup, checked on their markup:
	/// named controls, localized texts, theme colors, font sizes, keyboard handlers and localized access to every command.
	/// </summary>
	public sealed class GlobalsAndHelpAccessibilityTests
	{
		private static readonly XNamespace _xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
		private static readonly Regex _hardColor = new(@"(^|[\s=""'{])#[0-9A-Fa-f]{3,8}\b", RegexOptions.CultureInvariant);

		private static XDocument Load(String relativePath)
		{
			return XDocument.Load(Path.Combine(SourceTree.AppFolder, relativePath.Replace('/', Path.DirectorySeparatorChar)));
		}

		private static String? Attr(XElement element, String name)
		{
			return name.StartsWith("x:", StringComparison.Ordinal) ? (String?)element.Attribute(_xaml + name.Substring(2)) : (String?)element.Attribute(name);
		}

		private static IEnumerable<XElement> Named(XDocument document, String localName)
		{
			return document.Descendants().Where(e => e.Name.LocalName == localName);
		}

		public static IEnumerable<Object[]> Files()
		{
			return new[] { "Views/Panels/GlobalsPanelView.xaml", "Views/Panels/HelpPanelView.xaml", "Views/Help/HelpTopicView.xaml" }.Select(f => new Object[] { f });
		}

		[Theory]
		[MemberData(nameof(Files))]
		public void The_markup_has_no_hard_coded_color_and_no_small_font(String file)
		{
			XDocument document = Load(file);

			foreach (XAttribute attribute in document.Descendants().SelectMany(e => e.Attributes()))
			{
				Assert.False(_hardColor.IsMatch(attribute.Value), file + ": " + attribute.Name.LocalName + "=" + attribute.Value);
				if (attribute.Name.LocalName == "FontSize" && Double.TryParse(attribute.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out Double size))
				{
					Assert.True(size >= 11, file + ": font size " + attribute.Value);
				}
			}

			Assert.DoesNotContain(document.Descendants(), e => e.Attributes().Any(a => a.Name.LocalName is "Focusable" or "IsTabStop" && a.Value == "False" && e.Name.LocalName is "Button" or "TreeView" or "ListBox" or "TextBox"));
			Assert.DoesNotContain(document.Descendants(), e => Attr(e, "FocusVisualStyle") == "{x:Null}");
		}

		[Theory]
		[MemberData(nameof(Files))]
		public void Every_visible_text_is_a_localized_resource_or_a_binding(String file)
		{
			XDocument document = Load(file);

			foreach (XElement element in document.Descendants().Where(e => e.Name.LocalName is "TextBlock" or "Button"))
			{
				foreach (String attribute in new[] { "Text", "Content", "ToolTip" })
				{
					String? value = Attr(element, attribute);
					if (value is not null)
					{
						Assert.True(value.StartsWith('{'), file + ": " + attribute + "=" + value);
					}
				}
			}

			Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "TextBlock" && e.Nodes().OfType<XText>().Any(t => t.Value.Trim().Length > 0 && t.Value.Trim() != "…"));
		}

		[Fact]
		public void The_globals_tree_the_filter_and_the_buttons_are_named_and_the_tree_handles_the_keyboard()
		{
			XDocument document = Load("Views/Panels/GlobalsPanelView.xaml");

			XElement tree = Named(document, "TreeView").Single();
			Assert.Equal("{DynamicResource Str.Panels.TabGlobals}", Attr(tree, "AutomationProperties.Name"));
			Assert.Equal("OnTreeKeyDown", Attr(tree, "KeyDown"));
			Assert.Equal("OnTreePreviewMouseRightButtonDown", Attr(tree, "PreviewMouseRightButtonDown"));
			XElement filter = Named(document, "TextBox").Single();
			Assert.Equal("{DynamicResource Str.Panels.GlobalsFilterPlaceholder}", Attr(filter, "AutomationProperties.Name"));
			foreach (XElement button in Named(document, "Button"))
			{
				Assert.StartsWith("{DynamicResource Str.Panels.TipGlobals", Attr(button, "ToolTip"), StringComparison.Ordinal);
				Assert.Equal("{StaticResource ShellSmallIconButtonStyle}", Attr(button, "Style"));
			}

			Assert.Contains(Named(document, "Style"), s => Attr(s, "x:Key") == "GlobalItemStyle");
		}

		[Fact]
		public void The_context_menu_of_the_globals_has_localized_headers_and_works_through_commands()
		{
			XDocument document = Load("Views/Panels/GlobalsPanelView.xaml");

			XElement[] items = Named(document, "MenuItem").ToArray();

			Assert.Equal(6, items.Length);
			Assert.All(items, static item => Assert.StartsWith("{DynamicResource Str.Panels.Menu", Attr(item, "Header"), StringComparison.Ordinal));
			Assert.All(items, static item => Assert.Contains("Command", Attr(item, "Command"), StringComparison.Ordinal));
		}

		[Fact]
		public void The_badges_carry_the_information_with_an_icon_and_a_tool_tip_and_not_only_a_color()
		{
			XDocument document = Load("Views/Panels/GlobalsPanelView.xaml");

			XElement badge = Named(document, "SymbolIcon").Single(static i => Attr(i, "x:Name") == "Badge");
			Assert.Equal("{Binding FindingText}", Attr(badge, "ToolTip"));
			Assert.Contains(document.Descendants(), static e => e.Name.LocalName == "Setter" && Attr(e, "Property") == "Symbol" && Attr(e, "Value") == "Warning24");
			Assert.Contains(document.Descendants(), static e => e.Name.LocalName == "Setter" && Attr(e, "Property") == "Symbol" && Attr(e, "Value") == "Info24");
		}

		[Fact]
		public void The_help_filter_the_list_and_the_buttons_are_named()
		{
			XDocument panel = Load("Views/Panels/HelpPanelView.xaml");
			XDocument topic = Load("Views/Help/HelpTopicView.xaml");

			Assert.Equal("{DynamicResource Str.Panels.HelpFilterPlaceholder}", Attr(Named(panel, "TextBox").Single(), "AutomationProperties.Name"));
			Assert.Equal("{DynamicResource Str.Panels.HelpResultsName}", Attr(Named(panel, "ListBox").Single(), "AutomationProperties.Name"));
			XElement[] buttons = Named(topic, "Button").ToArray();
			Assert.Equal(3, buttons.Length);
			Assert.All(buttons, static b => Assert.StartsWith("{DynamicResource Str.Panels.Help", Attr(b, "Content"), StringComparison.Ordinal));
			Assert.Equal("{Binding SignatureText}", Attr(Named(topic, "HelpSignatureText").Single(), "AutomationProperties.Name"));
		}

		[Fact]
		public void The_right_browser_has_the_two_new_tabs_with_localized_headers_and_tool_tips()
		{
			XDocument document = Load("Views/Panels/RightBrowserView.xaml");

			XElement[] tabs = Named(document, "TabItem").ToArray();

			Assert.Equal(6, tabs.Length);
			Assert.Equal("{DynamicResource Str.Panels.TabGlobals}", Attr(tabs[4], "Header"));
			Assert.Equal("{DynamicResource Str.Panels.TabTipGlobals}", Attr(tabs[4], "ToolTip"));
			Assert.Equal("{DynamicResource Str.Panels.TabHelp}", Attr(tabs[5], "Header"));
			Assert.Equal("{DynamicResource Str.Panels.TabTipHelp}", Attr(tabs[5], "ToolTip"));
		}
	}
}