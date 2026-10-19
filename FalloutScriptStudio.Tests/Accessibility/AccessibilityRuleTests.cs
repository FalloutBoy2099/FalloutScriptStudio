namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using Xunit;

	/// <summary>
	/// Each rule of the markup scan is tried on small pieces of XAML that break it and on pieces that follow it, so a rule that silently stops
	/// reporting (and lets the inventory pass for the wrong reason) is noticed.
	/// </summary>
	public sealed class AccessibilityRuleTests
	{
		private static readonly String _file = Path.Combine(SourceTree.AppFolder, "Views", "Sample", "SampleWindow.xaml");

		private readonly XamlAccessibilityScanner _scanner = new XamlAccessibilityScanner();

		private static XDocument Window(String body, String rootAttributes = "Title=\"Sample\"")
		{
			return XDocument.Parse(
				"<ui:FluentWindow xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\""
				+ " xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\""
				+ " xmlns:ui=\"http://schemas.lepo.co/wpfui/2022/xaml\""
				+ " xmlns:a11y=\"clr-namespace:FalloutScriptStudio.Views.Accessibility\""
				+ " xmlns:local=\"clr-namespace:FalloutScriptStudio.Views.ToolWindows\""
				+ " xmlns:ae=\"http://icsharpcode.net/sharpdevelop/avalonedit\" " + rootAttributes + ">"
				+ "<Grid>" + body + "</Grid></ui:FluentWindow>",
				LoadOptions.SetLineInfo);
		}

		[Fact]
		public void AnIconOnlyButtonWithoutAName_IsReported()
		{
			XDocument xaml = Window("<ui:Button Icon=\"{ui:SymbolIcon Add24}\" ToolTip=\"Add\" />");

			String finding = Assert.Single(this._scanner.FindMissingNames(_file, xaml));
			Assert.Contains("icon-only button", finding, StringComparison.Ordinal);
		}

		[Fact]
		public void AToolTipAloneIsNotAName()
		{
			XDocument xaml = Window("<Button ToolTip=\"Close\"><ui:SymbolIcon Symbol=\"Dismiss24\" /></Button>");

			Assert.Single(this._scanner.FindMissingNames(_file, xaml));
		}

		[Theory]
		[InlineData("<ui:Button AutomationProperties.Name=\"Add\" Icon=\"{ui:SymbolIcon Add24}\" />")]
		[InlineData("<ui:Button Content=\"{DynamicResource Str.Common.Add}\" Icon=\"{ui:SymbolIcon Add24}\" />")]
		[InlineData("<Button><TextBlock Text=\"{DynamicResource Str.Common.Add}\" /></Button>")]
		[InlineData("<CheckBox>Match case</CheckBox>")]
		[InlineData("<ToggleButton AutomationProperties.LabeledBy=\"{Binding ElementName=Caption}\" />")]
		public void AControlWithAName_IsAccepted(String markup)
		{
			Assert.Empty(this._scanner.FindMissingNames(_file, Window(markup)));
		}

		[Fact]
		public void AStyleThatNamesTheControlsFromTheirToolTip_IsRecognized()
		{
			XDocument xaml = Window(
				"<Grid.Resources><Style x:Key=\"IconStyle\" TargetType=\"{x:Type ui:Button}\">"
				+ "<Setter Property=\"AutomationProperties.Name\" Value=\"{Binding ToolTip, RelativeSource={RelativeSource Self}}\" /></Style>"
				+ "<Style x:Key=\"Child\" BasedOn=\"{StaticResource IconStyle}\" TargetType=\"{x:Type ui:Button}\" /></Grid.Resources>"
				+ "<ui:Button Style=\"{StaticResource Child}\" Icon=\"{ui:SymbolIcon Add24}\" ToolTip=\"Add\" />");

			Assert.Empty(this._scanner.FindMissingNames(_file, xaml));
		}

		[Fact]
		public void TheStylesOfTheApplicationResourcesAreConsulted()
		{
			XDocument xaml = Window("<ui:Button Style=\"{StaticResource ShellIconButtonStyle}\" Icon=\"{ui:SymbolIcon Add24}\" ToolTip=\"Add\" />");

			Assert.Empty(this._scanner.FindMissingNames(_file, xaml));
		}

		[Theory]
		[InlineData("Aa")]
		[InlineData("W")]
		[InlineData(".*")]
		public void AButtonWhoseOnlyTextIsASymbol_NeedsAName(String text)
		{
			XDocument xaml = Window("<ToggleButton Content=\"" + text + "\" />");

			Assert.Single(this._scanner.FindMissingNames(_file, xaml));
		}

		[Theory]
		[InlineData("<TextBox Text=\"{Binding Value}\" />")]
		[InlineData("<ui:TextBox PlaceholderText=\"Filter\" />")]
		[InlineData("<ComboBox ItemsSource=\"{Binding Items}\" />")]
		[InlineData("<ListBox ItemsSource=\"{Binding Items}\" />")]
		[InlineData("<TreeView ItemsSource=\"{Binding Nodes}\" />")]
		[InlineData("<DataGrid ItemsSource=\"{Binding Rows}\" />")]
		[InlineData("<ProgressBar Value=\"3\" />")]
		[InlineData("<ae:TextEditor />")]
		public void AnInputWithoutANameOrALabel_IsReported(String markup)
		{
			Assert.Single(this._scanner.FindMissingNames(_file, Window(markup)));
		}

		[Theory]
		[InlineData("<TextBox AutomationProperties.Name=\"Name\" />")]
		[InlineData("<TextBox AutomationProperties.LabeledBy=\"{Binding ElementName=Caption}\" />")]
		[InlineData("<ae:TextEditor a11y:EditorAutomation.NameResource=\"Str.Editor.A11y.ScriptEditor\" />")]
		[InlineData("<local:SettingCard Title=\"Theme\"><ComboBox /></local:SettingCard>")]
		[InlineData("<local:SettingCard Title=\"Folder\"><StackPanel><TextBox /><ui:Button AutomationProperties.Name=\"Browse\" Icon=\"{ui:SymbolIcon Folder24}\" /></StackPanel></local:SettingCard>")]
		public void AnInputWithANameOrALabel_IsAccepted(String markup)
		{
			Assert.Empty(this._scanner.FindMissingNames(_file, Window(markup)));
		}

		[Fact]
		public void TheTitleOfASettingCardDoesNotNameTheButtonsOfTheRow()
		{
			XDocument xaml = Window("<local:SettingCard Title=\"Folder\"><ui:Button Icon=\"{ui:SymbolIcon Folder24}\" /></local:SettingCard>");

			Assert.Single(this._scanner.FindMissingNames(_file, xaml));
		}

		[Fact]
		public void ALabelThatTargetsAControl_NamesIt()
		{
			XDocument xaml = Window("<Label Content=\"_Name\" Target=\"{Binding ElementName=NameBox}\" /><TextBox x:Name=\"NameBox\" />");

			Assert.Empty(this._scanner.FindMissingNames(_file, xaml));
		}

		[Fact]
		public void ABlankColumnHeader_IsReported_AndAnAutomationNameFixesIt()
		{
			String grid = "<DataGrid AutomationProperties.Name=\"Rows\"><DataGrid.Columns><DataGridTemplateColumn><DataGridTemplateColumn.Header>{0}</DataGridTemplateColumn.Header></DataGridTemplateColumn></DataGrid.Columns></DataGrid>";

			Assert.Single(this._scanner.FindMissingNames(_file, Window(String.Format(grid, "<TextBlock Text=\" \" />"))));
			Assert.Empty(this._scanner.FindMissingNames(_file, Window(String.Format(grid, "<TextBlock AutomationProperties.Name=\"Type\" Text=\" \" />"))));
		}

		[Fact]
		public void AColumnWithoutAHeader_IsReported()
		{
			XDocument xaml = Window("<DataGrid AutomationProperties.Name=\"Rows\"><DataGrid.Columns><DataGridTextColumn Binding=\"{Binding Name}\" /></DataGrid.Columns></DataGrid>");

			Assert.Single(this._scanner.FindMissingNames(_file, xaml));
		}

		[Fact]
		public void AMenuItemAndATabWithoutAHeader_AreReported()
		{
			Assert.Single(this._scanner.FindMissingNames(_file, Window("<Menu><MenuItem Command=\"{Binding SaveCommand}\" /></Menu>")));
			Assert.Single(this._scanner.FindMissingNames(_file, Window("<TabControl><TabItem /></TabControl>")));
			Assert.Empty(this._scanner.FindMissingNames(_file, Window("<Menu><MenuItem Header=\"{DynamicResource Str.Common.File}\" /></Menu>")));
		}

		[Fact]
		public void TheContentsOfAControlTemplate_AreNotScanned()
		{
			XDocument xaml = Window("<Grid.Resources><ControlTemplate x:Key=\"T\" TargetType=\"{x:Type ContentControl}\"><TextBox /></ControlTemplate></Grid.Resources>");

			Assert.Empty(this._scanner.FindMissingNames(_file, xaml));
		}

		[Theory]
		[InlineData("<Button Content=\"Run\" Focusable=\"False\" />")]
		[InlineData("<TextBox AutomationProperties.Name=\"Name\" IsTabStop=\"False\" />")]
		[InlineData("<TextBlock Text=\"Zoom\" MouseLeftButtonUp=\"OnClick\" />")]
		[InlineData("<Border MouseDown=\"OnClick\" />")]
		public void AControlTheKeyboardCannotReach_IsReported(String markup)
		{
			Assert.Single(this._scanner.FindUnreachableControls(_file, Window(markup)));
		}

		[Fact]
		public void AControlInsideAnItemTemplate_MayBeNonFocusable()
		{
			XDocument xaml = Window("<TreeView AutomationProperties.Name=\"Tree\"><TreeView.ItemTemplate><HierarchicalDataTemplate><CheckBox AutomationProperties.Name=\"x\" Focusable=\"False\" /></HierarchicalDataTemplate></TreeView.ItemTemplate></TreeView>");

			Assert.Empty(this._scanner.FindUnreachableControls(_file, xaml));
		}

		[Theory]
		[InlineData("<Border Background=\"#FFFFC8\" />")]
		[InlineData("<TextBlock Foreground=\"Red\" />")]
		[InlineData("<Grid.Resources><SolidColorBrush x:Key=\"B\" Color=\"#D2691E\" /></Grid.Resources>")]
		[InlineData("<Rectangle Fill=\"#66FFD54F\" />")]
		public void AHardCodedColor_IsReported(String markup)
		{
			Assert.Single(this._scanner.FindHardCodedColors(_file, Window(markup)));
		}

		[Theory]
		[InlineData("<Border Background=\"{DynamicResource CardBackgroundFillColorDefaultBrush}\" />")]
		[InlineData("<Border Background=\"Transparent\" />")]
		[InlineData("<Grid.Resources><SolidColorBrush x:Key=\"B\" Color=\"{DynamicResource SystemFillColorCaution}\" /></Grid.Resources>")]
		[InlineData("<Border BorderBrush=\"{TemplateBinding Background}\" />")]
		public void AColorFromTheTheme_IsAccepted(String markup)
		{
			Assert.Empty(this._scanner.FindHardCodedColors(_file, Window(markup)));
		}

		[Fact]
		public void ARemovedFocusRectangle_IsReported()
		{
			Assert.Single(this._scanner.FindHiddenFocusVisuals(_file, Window("<Button Content=\"Run\" FocusVisualStyle=\"{x:Null}\" />")));
			Assert.Single(this._scanner.FindHiddenFocusVisuals(_file, Window("<Grid.Resources><Style TargetType=\"Button\"><Setter Property=\"FocusVisualStyle\" Value=\"{x:Null}\" /></Style></Grid.Resources>")));
			Assert.Empty(this._scanner.FindHiddenFocusVisuals(_file, Window("<Button Content=\"Run\" />")));
		}

		[Fact]
		public void AStyleWithItsOwnTemplate_MustKeepTheFocusRectangle()
		{
			String template = "<Setter Property=\"Template\"><Setter.Value><ControlTemplate TargetType=\"ToggleButton\"><Border /></ControlTemplate></Setter.Value></Setter>";
			String Style(String attributes, String extra) => "<Grid.Resources><Style TargetType=\"ToggleButton\"" + attributes + ">" + template + extra + "</Style></Grid.Resources>";

			Assert.Single(this._scanner.FindStylesWithoutFocusVisual(_file, Window(Style(String.Empty, String.Empty))));
			Assert.Empty(this._scanner.FindStylesWithoutFocusVisual(_file, Window(Style(String.Empty, "<Setter Property=\"FocusVisualStyle\" Value=\"{DynamicResource DefaultControlFocusVisualStyle}\" />"))));
			Assert.Empty(this._scanner.FindStylesWithoutFocusVisual(_file, Window(Style(" BasedOn=\"{StaticResource {x:Type ToggleButton}}\"", String.Empty))));
		}

		[Fact]
		public void ATextSmallerThanTheMinimum_IsReported()
		{
			Assert.Single(this._scanner.FindSmallFonts(_file, Window("<TextBlock FontSize=\"10\" Text=\"x\" />"), 11));
			Assert.Single(this._scanner.FindSmallFonts(_file, Window("<Grid.Resources><Style TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"9\" /></Style></Grid.Resources>"), 11));
			Assert.Empty(this._scanner.FindSmallFonts(_file, Window("<TextBlock FontSize=\"11\" Text=\"x\" />"), 11));
		}

		[Fact]
		public void ADialogNeedsEscapeAndAnInitialFocus()
		{
			String[] findings = this._scanner.FindWindowConventionViolations(_file, Window("<Button Content=\"OK\" IsDefault=\"True\" />")).ToArray();

			Assert.Contains(findings, finding => finding.Contains("Escape", StringComparison.Ordinal));
			Assert.Contains(findings, finding => finding.Contains("default button", StringComparison.Ordinal));
			Assert.Contains(findings, finding => finding.Contains("initial focus", StringComparison.Ordinal));
		}

		[Fact]
		public void ADialogWithACancelButtonAndAFocusedElement_IsAccepted()
		{
			XDocument xaml = Window(
				"<TextBox x:Name=\"Box\" AutomationProperties.Name=\"Name\" /><Button Content=\"OK\" IsDefault=\"True\" /><Button Content=\"Cancel\" IsCancel=\"True\" />",
				"Title=\"Sample\" FocusManager.FocusedElement=\"{Binding ElementName=Box}\"");

			Assert.Empty(this._scanner.FindWindowConventionViolations(_file, xaml));
		}

		[Fact]
		public void CloseOnEscape_StandsInForACancelButton()
		{
			XDocument xaml = Window("<Button Content=\"Help\" />", "Title=\"Sample\" a11y:WindowKeys.CloseOnEscape=\"True\" FocusManager.FocusedElement=\"{Binding ElementName=Box}\"");

			Assert.Empty(this._scanner.FindWindowConventionViolations(_file, xaml));
		}

		[Fact]
		public void AWindowWithoutATitle_IsReported()
		{
			XDocument xaml = Window("<Button Content=\"Cancel\" IsCancel=\"True\" />", "a11y:WindowKeys.CloseOnEscape=\"True\" FocusManager.FocusedElement=\"{Binding ElementName=Box}\"");

			Assert.Single(this._scanner.FindWindowConventionViolations(_file, xaml));
		}

		[Fact]
		public void AControlThatComesBeforeAnEarlierCell_BreaksTheReadingOrder()
		{
			XDocument backwards = Window(
				"<Button Grid.Row=\"1\" Content=\"Second\" /><Button Grid.Row=\"0\" Content=\"First\" />");
			XDocument inOrder = Window(
				"<Button Grid.Row=\"0\" Grid.Column=\"0\" Content=\"A\" /><Button Grid.Row=\"0\" Grid.Column=\"1\" Content=\"B\" /><Button Grid.Row=\"1\" Content=\"C\" />");
			XDocument overlay = Window(
				"<Button Grid.Row=\"2\" Content=\"Last\" /><ui:TextBox Grid.Row=\"0\" Grid.RowSpan=\"3\" AutomationProperties.Name=\"Overlay\" />");
			XDocument explicitOrder = Window(
				"<Button Grid.Row=\"1\" TabIndex=\"1\" Content=\"Second\" /><Button Grid.Row=\"0\" TabIndex=\"0\" Content=\"First\" />");

			Assert.Single(this._scanner.FindReadingOrderProblems(_file, backwards));
			Assert.Empty(this._scanner.FindReadingOrderProblems(_file, inOrder));
			Assert.Empty(this._scanner.FindReadingOrderProblems(_file, overlay));
			Assert.Empty(this._scanner.FindReadingOrderProblems(_file, explicitOrder));
		}

		[Fact]
		public void ATreeAndAListOpenedByDoubleClickNeedTheKeyboard()
		{
			Assert.Single(this._scanner.FindMouseOnlyLists(_file, Window("<TreeView AutomationProperties.Name=\"Tree\" />")));
			Assert.Empty(this._scanner.FindMouseOnlyLists(_file, Window("<TreeView AutomationProperties.Name=\"Tree\" KeyDown=\"OnKey\" />")));
			Assert.Single(this._scanner.FindMouseOnlyLists(_file, Window("<DataGrid AutomationProperties.Name=\"Rows\" MouseDoubleClick=\"OnOpen\" />")));
			Assert.Empty(this._scanner.FindMouseOnlyLists(_file, Window("<DataGrid AutomationProperties.Name=\"Rows\" MouseDoubleClick=\"OnOpen\" PreviewKeyDown=\"OnKey\" />")));
			Assert.Empty(this._scanner.FindMouseOnlyLists(_file, Window("<ListBox AutomationProperties.Name=\"Items\" MouseDoubleClick=\"OnOpen\" /><Button Content=\"Open\" IsDefault=\"True\" />")));
			Assert.Empty(this._scanner.FindMouseOnlyLists(_file, Window("<ListBox AutomationProperties.Name=\"Items\" />")));
		}

		[Fact]
		public void AWindowNeedsRoundingAndAFitForItsHeight()
		{
			String[] findings = this._scanner.FindLayoutProblems(_file, Window(String.Empty, "Title=\"Sample\" Height=\"700\"")).ToArray();
			Assert.Equal(2, findings.Length);

			XDocument good = Window(String.Empty, "Title=\"Sample\" Height=\"700\" UseLayoutRounding=\"True\" SnapsToDevicePixels=\"True\" a11y:WindowFit.FitToWorkArea=\"True\"");
			Assert.Empty(this._scanner.FindLayoutProblems(_file, good));

			XDocument small = Window(String.Empty, "Title=\"Sample\" Height=\"230\" UseLayoutRounding=\"True\" SnapsToDevicePixels=\"True\"");
			Assert.Empty(this._scanner.FindLayoutProblems(_file, small));
		}

		[Fact]
		public void ATwiceBoundGesture_IsReported_WhateverTheSpelling()
		{
			XDocument keyAndModifiers = Window("<Grid.InputBindings><KeyBinding Key=\"S\" Modifiers=\"Ctrl\" Command=\"{Binding A}\" /><KeyBinding Key=\"S\" Modifiers=\"Ctrl\" Command=\"{Binding B}\" /></Grid.InputBindings>");
			XDocument gesture = Window("<Grid.InputBindings><KeyBinding Gesture=\"Ctrl+S\" Command=\"{Binding A}\" /><KeyBinding Gesture=\"Ctrl + S\" Command=\"{Binding B}\" /></Grid.InputBindings>");
			XDocument different = Window("<Grid.InputBindings><KeyBinding Key=\"S\" Modifiers=\"Ctrl\" Command=\"{Binding A}\" /><KeyBinding Key=\"S\" Modifiers=\"Ctrl+Shift\" Command=\"{Binding B}\" /></Grid.InputBindings>");

			Assert.Single(XamlAccessibilityScanner.FindDuplicateGestures(_file, keyAndModifiers));
			Assert.Single(XamlAccessibilityScanner.FindDuplicateGestures(_file, gesture));
			Assert.Empty(XamlAccessibilityScanner.FindDuplicateGestures(_file, different));
		}
	}
}