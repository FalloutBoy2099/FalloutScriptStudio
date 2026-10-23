namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Tests.Accessibility;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	/// <summary>The window "Compare Message Files": a thin virtualized table like the Errors tab, buttons that grow with their text, and every text in the 11 languages.</summary>
	public sealed class CompareMessagesMarkupTests
	{
		private static readonly XNamespace Presentation = SourceTree.Presentation;
		private static readonly XNamespace Ui = "http://schemas.lepo.co/wpfui/2022/xaml";
		private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

		public static IEnumerable<Object[]> Languages()
		{
			return SourceTree.LanguageCodes().Select(code => new Object[] { code });
		}

		private static XDocument Window()
		{
			return SourceTree.Load(Path.Combine(SourceTree.AppFolder, "Views", "ToolWindows", "CompareMessagesWindow.xaml"));
		}

		private static String? Attribute(XElement element, String name)
		{
			return (String?)element.Attribute(name);
		}

		[Fact]
		public void The_table_has_thin_rows_a_thin_header_small_text_and_virtualization()
		{
			XElement grid = Window().Descendants(Presentation + "DataGrid").Single();

			Assert.Equal("22", Attribute(grid, "RowHeight"));
			Assert.Equal("22", Attribute(grid, "MinRowHeight"));
			Assert.Equal("22", Attribute(grid, "ColumnHeaderHeight"));
			Assert.Equal("12", Attribute(grid, "FontSize"));
			Assert.Equal("{StaticResource ShellCompactColumnHeaderStyle}", Attribute(grid, "ColumnHeaderStyle"));
			Assert.Equal("True", Attribute(grid, "EnableRowVirtualization"));
			Assert.Equal("True", Attribute(grid, "VirtualizingPanel.IsVirtualizing"));
			Assert.Equal("Recycling", Attribute(grid, "VirtualizingPanel.VirtualizationMode"));
			Assert.Equal("True", Attribute(grid, "ScrollViewer.CanContentScroll"));
			Assert.Equal("True", Attribute(grid, "IsReadOnly"));
			Assert.Equal("Single", Attribute(grid, "SelectionMode"));
			Assert.NotNull(Attribute(grid, "AutomationProperties.Name"));
			Assert.Equal(5, grid.Descendants(Presentation + "DataGridTextColumn").Count());
		}

		[Fact]
		public void The_rows_are_named_for_a_screen_reader_and_the_window_is_resizable_and_fits_a_small_screen()
		{
			XDocument document = Window();
			XElement rowStyle = document.Descendants(Presentation + "DataGrid.RowStyle").Single().Elements(Presentation + "Style").Single();

			Assert.Contains(rowStyle.Elements(Presentation + "Setter"), setter => Attribute(setter, "Property") == "AutomationProperties.Name" && Attribute(setter, "Value") == "{Binding AccessibleName}");
			XElement root = document.Root!;
			Assert.NotNull(root.Attribute("MinWidth"));
			Assert.NotNull(root.Attribute("MinHeight"));
			Assert.Null(root.Attribute("ResizeMode"));
			Assert.Equal("True", Attribute(root, "{clr-namespace:FalloutScriptStudio.Views.Accessibility}WindowFit.FitToWorkArea"));
		}

		[Fact]
		public void No_text_button_has_a_fixed_width_and_the_close_button_is_the_cancel_button()
		{
			XDocument document = Window();
			List<XElement> buttons = document.Descendants(Ui + "Button").ToList();

			Assert.True(buttons.Count >= 8);
			Assert.All(buttons, button => Assert.Null(button.Attribute("Width")));
			XElement close = Assert.Single(buttons, button => Attribute(button, "IsCancel") == "True");
			Assert.Equal("{Binding CloseCommand}", Attribute(close, "Command"));
		}

		[Fact]
		public void The_filters_are_toggles_with_a_text_and_a_tool_tip_so_that_they_have_a_name()
		{
			XElement toggle = Window().Descendants(Presentation + "ToggleButton").Single();

			Assert.Equal("{Binding Label}", Attribute(toggle, "Content"));
			Assert.Equal("{Binding Label}", Attribute(toggle, "ToolTip"));
			Assert.Equal("{StaticResource ShellCounterToggleStyle}", Attribute(toggle, "Style"));
		}

		[Fact]
		public void The_status_line_is_a_live_region()
		{
			XDocument document = Window();

			Assert.Contains(document.Descendants(Presentation + "TextBlock"), text => Attribute(text, "{clr-namespace:FalloutScriptStudio.Views.Accessibility}LiveRegion.Text") == "{Binding StatusText}");
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void Every_status_issue_and_note_has_a_text_in_every_language(String language)
		{
			Dictionary<String, String> strings = SourceTree.LoadStrings(language);
			List<String> keys = [];
			keys.AddRange(Enum.GetValues<MessageComparisonStatus>().Select(status => "Str.ToolWindows.Compare.Status." + ComparisonTexts.StatusKey(status)));
			keys.AddRange(new[] { "PlaceholderCount", "TagCount", "VariableCount", "WhitespaceOnly", "TrailingSpaces", "SuspiciousCharacters", "PlayerTab", "Description" }.Select(name => "Str.ToolWindows.Compare.Issue." + name));
			keys.AddRange(Enum.GetValues<MessageComparisonNoteKind>().Select(kind => "Str.ToolWindows.Compare.Note." + kind));
			keys.AddRange(new[] { "CmpMissing", "CmpExtra", "CmpSame", "CmpPlaceholders", "CmpEmpty", "CmpPlayer", "CmpIssue" }.SelectMany(name => new[] { "Str.Shell.Problem." + name, "Str.Shell.Problem." + name + "Hint" }));

			List<String> missing = keys.Where(key => !strings.ContainsKey(key)).ToList();

			Assert.True(missing.Count == 0, language + ": " + String.Join(", ", missing));
		}

		[Fact]
		public void Every_issue_flag_has_a_name_and_a_row_lists_the_names_of_its_flags()
		{
			MessageComparisonIssues all = Enum.GetValues<MessageComparisonIssues>().Aggregate(MessageComparisonIssues.None, (sum, flag) => sum | flag);

			String names = ComparisonTexts.IssueNames(all);

			Assert.Equal(8, names.Split("; ").Length);
			Assert.DoesNotContain("Str.ToolWindows", names, StringComparison.Ordinal);
			Assert.Equal(String.Empty, ComparisonTexts.IssueNames(MessageComparisonIssues.None));
		}

		[Fact]
		public void Every_dialogue_finding_code_has_a_message_and_a_hint_in_every_language()
		{
			String[] names = { "DlgUnreachable", "DlgDeadEnd", "DlgUndefinedTarget", "DlgCycle", "DlgNoText", "DlgDuplicateTarget", "DlgMissingMessages", "DlgTooManyOptions", "DlgDuplicateNumber" };
			Assert.Equal(Enum.GetValues<FalloutScriptStudio.Core.Dialogue.DialogueQualityCode>().Length, names.Length);
			List<String> missing = [];
			foreach (String language in SourceTree.LanguageCodes())
			{
				Dictionary<String, String> strings = SourceTree.LoadStrings(language);
				foreach (String name in names)
				{
					foreach (String key in new[] { "Str.Shell.Problem." + name, "Str.Shell.Problem." + name + "Hint" })
					{
						if (!strings.TryGetValue(key, out String? text))
						{
							missing.Add(language + " " + key);
						}
						else if (!key.EndsWith("Hint", StringComparison.Ordinal) && !text.Contains("[DLG0", StringComparison.Ordinal))
						{
							missing.Add(language + " " + key + " lacks the code of the finding");
						}
					}
				}
			}

			Assert.True(missing.Count == 0, String.Join(", ", missing));
		}
	}
}