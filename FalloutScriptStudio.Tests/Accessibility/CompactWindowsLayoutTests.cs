namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Media;
	using System.Xml.Linq;
	using Xunit;

	/// <summary>
	/// The layout of the two compact windows: the command palette (one thin line per command) and the Search &amp; Replace window (thin results
	/// grid, grouped buttons that grow with their text so that no language clips them). The markup is checked statically; the texts of the
	/// buttons of the search window are measured in every language.
	/// </summary>
	public sealed class CompactWindowsLayoutTests
	{
		private static readonly XNamespace Ui = "http://schemas.lepo.co/wpfui/2022/xaml";
		private static readonly XNamespace Presentation = SourceTree.Presentation;

		public static IEnumerable<Object[]> Languages()
		{
			return SourceTree.LanguageCodes().Select(code => new Object[] { code });
		}

		private static XDocument Load(params String[] relativePath)
		{
			return SourceTree.Load(Path.Combine(new[] { SourceTree.AppFolder }.Concat(relativePath).ToArray()));
		}

		private static XDocument SearchWindowMarkup()
		{
			return Load("Views", "ScriptTools", "AdvancedSearchWindow.xaml");
		}

		private static XDocument PaletteMarkup()
		{
			return Load("Views", "ToolWindows", "CommandPaletteWindow.xaml");
		}

		private static String? Attribute(XElement element, String name)
		{
			return (String?)element.Attribute(name);
		}

		private static Double Number(XElement element, String name)
		{
			return Double.Parse(Attribute(element, name)!, System.Globalization.CultureInfo.InvariantCulture);
		}

		[Theory]
		[MemberData(nameof(Languages))]
		public void TheLongestButtonTextOfTheSearchWindowFitsInTheNarrowestWindowInEveryLanguage(String language)
		{
			String[] keys =
			{
				"Str.ScriptTools.SearchFindNext", "Str.ScriptTools.SearchFindAll", "Str.ScriptTools.SearchBtnReplace", "Str.ScriptTools.SearchBtnReplaceAll",
				"Str.ScriptTools.ReplBtnReplaceInFiles", "Str.ScriptTools.SearchBtnBrowse", "Str.Common.Cancel",
			};
			XDocument document = SearchWindowMarkup();
			Double minWindowWidth = Number(document.Root!, "MinWidth");
			XElement style = document.Descendants(Presentation + "Style").Single(element => Attribute(element, "{http://schemas.microsoft.com/winfx/2006/xaml}Key") == "SearchActionButtonStyle");
			Double minButtonWidth = Number(style.Elements(Presentation + "Setter").Single(setter => Attribute(setter, "Property") == "MinWidth"), "Value");
			Double padding = Double.Parse(Attribute(style.Elements(Presentation + "Setter").Single(setter => Attribute(setter, "Property") == "Padding"), "Value")!.Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);
			Dictionary<String, String> strings = SourceTree.LoadStrings(language);
			Typeface typeface = new Typeface("Segoe UI");
			const Double iconAndGap = 28;
			const Double windowMargins = 24 + 20;
			List<String> findings = [];

			foreach (String key in keys)
			{
				String text = strings[key].Replace("_", String.Empty, StringComparison.Ordinal);
				FormattedText measured = new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 12, Brushes.Black, 1.0);
				Double needed = Math.Max(minButtonWidth, measured.WidthIncludingTrailingWhitespace + (2 * padding) + iconAndGap);

				// A button wider than the line could not be wrapped onto another line: it would be cut by the window.
				if (needed > minWindowWidth - windowMargins)
				{
					findings.Add($"{language} {key}: '{text}' needs {needed:0} units, the narrowest window has {minWindowWidth - windowMargins:0}");
				}
			}

			Assert.True(findings.Count == 0, Environment.NewLine + String.Join(Environment.NewLine, findings));
		}

		[Fact]
		public void TheSearchWindowIsAbout760By640AndCanBeResized()
		{
			XElement root = SearchWindowMarkup().Root!;

			Assert.Equal(760, Number(root, "Width"));
			Assert.Equal(640, Number(root, "Height"));
			Assert.NotNull(root.Attribute("MinWidth"));
			Assert.NotNull(root.Attribute("MinHeight"));
			Assert.Null(root.Attribute("ResizeMode"));
		}

		[Fact]
		public void NoTextButtonOfTheSearchWindowHasAFixedWidth_TheyGrowWithTheirText()
		{
			XDocument document = SearchWindowMarkup();
			List<XElement> buttons = document.Descendants(Ui + "Button").ToList();
			XElement style = document.Descendants(Presentation + "Style").Single(element => Attribute(element, "{http://schemas.microsoft.com/winfx/2006/xaml}Key") == "SearchActionButtonStyle");
			String[] setters = style.Elements(Presentation + "Setter").Select(setter => Attribute(setter, "Property")!).ToArray();

			Assert.True(buttons.Count >= 7);
			Assert.All(buttons, button =>
			{
				Assert.Null(button.Attribute("Width"));
				Assert.Equal("{StaticResource SearchActionButtonStyle}", Attribute(button, "Style"));
			});
			Assert.Contains("MinWidth", setters);
			Assert.Contains("Padding", setters);
			Assert.DoesNotContain("Width", setters);
			Assert.Equal(28, Double.Parse(style.Elements(Presentation + "Setter").Single(setter => Attribute(setter, "Property") == "MinHeight").Attribute("Value")!.Value, System.Globalization.CultureInfo.InvariantCulture));
		}

		[Fact]
		public void TheReplaceButtonsFormAGroupApartFromTheFindButtons()
		{
			XDocument document = SearchWindowMarkup();
			XElement Button(String command) => document.Descendants(Ui + "Button").Single(button => Attribute(button, "Command") == "{Binding " + command + "}");

			XElement group = Button("ReplaceCommand").Ancestors(Presentation + "Border").First();

			Assert.Contains(Button("ReplaceAllCommand"), group.Descendants());
			Assert.Contains(Button("ReplaceInFilesCommand"), group.Descendants());
			Assert.DoesNotContain(Button("SearchCommand"), group.Descendants());
			Assert.DoesNotContain(Button("FindAllCommand"), group.Descendants());
			Assert.Equal("{DynamicResource CardBackgroundFillColorDefaultBrush}", Attribute(group, "Background"));

			// The Tab order follows the markup: the find buttons come before the replace group.
			List<XElement> order = document.Descendants(Ui + "Button").ToList();
			Assert.True(order.IndexOf(Button("FindAllCommand")) < order.IndexOf(Button("ReplaceCommand")));
			Assert.True(order.IndexOf(Button("ReplaceCommand")) < order.IndexOf(Button("ReplaceAllCommand")));
			Assert.True(order.IndexOf(Button("ReplaceAllCommand")) < order.IndexOf(Button("ReplaceInFilesCommand")));
		}

		[Fact]
		public void TheResultsFillTheRemainingSpace_WithAThinHeaderThinRowsAndAnEmptyState()
		{
			XDocument document = SearchWindowMarkup();
			XElement grid = document.Descendants(Presentation + "DataGrid").Single(element => Attribute(element, "{http://schemas.microsoft.com/winfx/2006/xaml}Name") == "ResultsGrid");
			XElement host = grid.Ancestors().First(ancestor => ancestor.Attribute("Grid.Row") is not null);
			Int32 row = Int32.Parse(Attribute(host, "Grid.Row")!);
			XElement definitions = host.Parent!.Element(Presentation + "Grid.RowDefinitions")!;

			Assert.Equal("*", Attribute(definitions.Elements().ElementAt(row), "Height"));
			Assert.Equal(definitions.Elements().Count() - 1, row);
			Assert.Equal(22, Number(grid, "ColumnHeaderHeight"));
			Assert.Equal(22, Number(grid, "RowHeight"));
			Assert.Equal(22, Number(grid, "MinRowHeight"));
			Assert.Equal("{StaticResource ShellCompactColumnHeaderStyle}", Attribute(grid, "ColumnHeaderStyle"));
			Assert.Contains(document.Descendants(Presentation + "TextBlock"), text => Attribute(text, "Text") == "{DynamicResource Str.ScriptTools.SearchResultsEmpty}");
			Assert.Contains(document.Descendants(), element => Attribute(element, "Visibility") == "{Binding HasNoResults, Converter={StaticResource BooleanToVisibility}}");
		}

		[Fact]
		public void TheCompactColumnHeaderIsThinAndNotBold()
		{
			XDocument resources = Load("Resources", "ShellResources.xaml");
			XElement style = resources.Descendants(Presentation + "Style").Single(element => Attribute(element, "{http://schemas.microsoft.com/winfx/2006/xaml}Key") == "ShellCompactColumnHeaderStyle");
			String? Setter(String property) => Attribute(style.Elements(Presentation + "Setter").Single(setter => Attribute(setter, "Property") == property), "Value");

			Assert.Equal("22", Setter("Height"));
			Assert.Equal("11", Setter("FontSize"));
			Assert.Equal("Normal", Setter("FontWeight"));
		}

		[Fact]
		public void TheCommandPaletteIsAboutSixHundredTwentyByFourHundredTwentyWithAThinFilterAndThinRows()
		{
			XDocument document = PaletteMarkup();
			XElement root = document.Root!;
			XElement filter = document.Descendants(Ui + "TextBox").Single();
			XElement rowStyle = document.Descendants(Presentation + "Style").Single(element => Attribute(element, "{http://schemas.microsoft.com/winfx/2006/xaml}Key") == "PaletteRowStyle");
			Double Setter(String property) => Double.Parse(Attribute(rowStyle.Elements(Presentation + "Setter").Single(setter => Attribute(setter, "Property") == property), "Value")!, System.Globalization.CultureInfo.InvariantCulture);

			Assert.Equal(620, Number(root, "Width"));
			Assert.Equal(420, Number(root, "Height"));
			Assert.Equal(28, Number(filter, "Height"));
			Assert.Equal(26, Setter("MinHeight"));
		}

		[Fact]
		public void ACommandIsOneLine_TheReasonAMissingCommandCannotRunIsOnlyAToolTip()
		{
			XDocument document = PaletteMarkup();
			XElement list = document.Descendants(Presentation + "ListBox").Single();
			XElement template = list.Descendants(Presentation + "DataTemplate").Single();
			XElement rowStyle = document.Descendants(Presentation + "Style").Single(element => Attribute(element, "{http://schemas.microsoft.com/winfx/2006/xaml}Key") == "PaletteRowStyle");

			// The row template has one grid with the name, the menu path and the trailing text; no stacked lines, no reason text.
			Assert.Empty(template.Descendants(Presentation + "StackPanel"));
			Assert.DoesNotContain("ReasonText", template.ToString(), StringComparison.Ordinal);
			Assert.Equal(3, template.Descendants(Presentation + "TextBlock").Count());
			Assert.Contains("ReasonText", rowStyle.ToString(), StringComparison.Ordinal);
			Assert.Equal("True", Attribute(list, "VirtualizingPanel.IsVirtualizing"));
			Assert.Equal("11", Attribute(document.Descendants(Presentation + "TextBlock").Single(text => Attribute(text, "Text") == "{Binding StatusText}"), "FontSize"));
		}
	}
}