namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Xml.Linq;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Accessibility;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>
	/// The Errors tab: the grid keeps sane column widths (its header used to be crushed to "Fi", "Li", "M" when the list was empty) and a
	/// friendly "No problems" text replaces the empty grid. The view cannot be created without the resources of the application (it refers
	/// to them while it loads), so the markup is checked here and the view model state that drives it is tested with the problem list; the
	/// look was checked on the real window in both themes.
	/// </summary>
	public sealed class OutputPanelEmptyStateTests
	{
		private static XDocument Load()
		{
			return XDocument.Load(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "OutputPanelView.xaml"));
		}

		private static IEnumerable<XElement> Grids(XDocument document)
		{
			return document.Descendants(SourceTree.Presentation + "DataGrid");
		}

		private static Double Number(XElement element, String attribute)
		{
			return Double.Parse((String?)element.Attribute(attribute) ?? "0", CultureInfo.InvariantCulture);
		}

		[Fact]
		public void Every_grid_of_the_panel_gives_its_columns_a_minimum_width_so_the_header_is_never_crushed()
		{
			List<XElement> grids = Grids(Load()).ToList();
			Assert.Equal(2, grids.Count);

			foreach (XElement grid in grids)
			{
				List<XElement> columns = grid.Descendants(SourceTree.Presentation + "DataGrid.Columns").Single().Elements().ToList();
				Assert.Equal(4, columns.Count);
				Assert.True(Number(columns[0], "MinWidth") >= 30, "type column");
				Assert.True(Number(columns[1], "MinWidth") >= 110, "file column");
				Assert.True(Number(columns[2], "MinWidth") >= 44, "line column");
				Assert.True(Number(columns[3], "MinWidth") >= 200, "message column");
				Assert.Equal("*", (String?)columns[3].Attribute("Width"));
				Assert.True(Number(columns[1], "Width") >= Number(columns[1], "MinWidth"));
				Assert.True(Number(columns[2], "Width") >= Number(columns[2], "MinWidth"));
			}
		}

		[Fact]
		public void The_errors_grid_is_replaced_by_the_localized_text_and_an_icon_when_there_is_nothing_to_list()
		{
			XDocument document = Load();
			XElement errorsGrid = Grids(document).First(grid => ((String?)grid.Attribute("ItemsSource"))?.Contains("Output.Errors", StringComparison.Ordinal) == true);
			XElement template = errorsGrid.Ancestors(SourceTree.Presentation + "DataTemplate").First();
			XElement empty = template.Descendants(SourceTree.Presentation + "StackPanel").First(panel => ((String?)panel.Attribute("Visibility"))?.Contains("Output.HasNoProblems", StringComparison.Ordinal) == true);

			Assert.Contains("BoolToVisibility", (String)empty.Attribute("Visibility")!, StringComparison.Ordinal);
			Assert.Equal("Center", (String?)empty.Attribute("HorizontalAlignment"));
			Assert.Equal("Center", (String?)empty.Attribute("VerticalAlignment"));
			XElement text = empty.Descendants(SourceTree.Presentation + "TextBlock").Single();
			Assert.Equal("{DynamicResource Str.Shell.Problems.NoProblems}", (String?)text.Attribute("Text"));
			Assert.Single(empty.Descendants(), element => element.Name.LocalName == "SymbolIcon");
			Assert.True(Loc.TryGet("Str.Shell.Problems.NoProblems", out String? message) && !String.IsNullOrWhiteSpace(message));

			XElement trigger = errorsGrid.Element(SourceTree.Presentation + "DataGrid.Style")!.Descendants(SourceTree.Presentation + "DataTrigger").Single();
			Assert.Equal("{Binding Output.HasNoProblems}", (String?)trigger.Attribute("Binding"));
			Assert.Equal("True", (String?)trigger.Attribute("Value"));
			Assert.Equal("Collapsed", (String?)trigger.Descendants(SourceTree.Presentation + "Setter").Single().Attribute("Value"));
		}

		[Fact]
		public void The_empty_state_follows_the_theme_it_uses_brushes_and_never_colors()
		{
			String xaml = File.ReadAllText(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "OutputPanelView.xaml"));

			Assert.DoesNotMatch(new Regex(@"(Foreground|Background|Fill|Stroke)=""#"), xaml);
			Assert.Contains("Foreground=\"{DynamicResource TextFillColorTertiaryBrush}\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Foreground=\"{DynamicResource TextFillColorSecondaryBrush}\"", xaml, StringComparison.Ordinal);
		}

		[Fact]
		public void The_view_model_tells_the_view_to_show_the_text_only_when_nothing_at_all_is_reported()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				Assert.True(harness.Output.HasNoProblems);
				Assert.Empty(harness.Output.Errors);

				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Warning, Source = ProblemSource.Tool, Text = "compile.exe is missing" });
				Assert.False(harness.Output.HasNoProblems);
				Assert.Single(harness.Output.Errors);

				// The filters hide the row, but the list is not empty for good: the grid (with its header) stays.
				harness.Output.ShowWarnings = false;
				Assert.Empty(harness.Output.Errors);
				Assert.False(harness.Output.HasNoProblems);

				harness.Problems.Clear();
				Assert.True(harness.Output.HasNoProblems);
				return System.Threading.Tasks.Task.CompletedTask;
			});
		}
	}
}