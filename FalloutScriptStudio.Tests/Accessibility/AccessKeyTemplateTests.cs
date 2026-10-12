namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using Xunit;

	/// <summary>
	/// The controls of the dialogs show an underscore in their text as an access key only when their (WPF-UI) template presents the content as
	/// access text. These tests load the real templates and look for the access key in the visual tree.
	/// </summary>
	public sealed class AccessKeyTemplateTests
	{
		private static Boolean HasAccessKey(Func<FrameworkElement> create, Char expected)
		{
			Boolean found = false;
			WindowHost.Run(
				() => new Window { Content = create(), SizeToContent = SizeToContent.WidthAndHeight },
				window =>
				{
					found = RuntimeInspector.Descendants(window).OfType<AccessText>().Any(text => Char.ToUpperInvariant(text.AccessKey) == expected);
				});
			return found;
		}

		[Fact]
		public void TheNameReadByAScreenReaderLacksTheUnderscore()
		{
			String? name = null;
			WindowHost.Run(
				() => new Window { Content = new Wpf.Ui.Controls.Button { Content = "_Save" }, SizeToContent = SizeToContent.WidthAndHeight },
				window => name = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement((UIElement)window.Content).GetName());

			Assert.Equal("Save", name);
		}

		public static IEnumerable<Object[]> Controls()
		{
			yield return new Object[] { "Button" };
			yield return new Object[] { "CheckBox" };
			yield return new Object[] { "RadioButton" };
			yield return new Object[] { "ToggleSwitch" };
			yield return new Object[] { "HyperlinkButton" };
		}

		[Theory]
		[MemberData(nameof(Controls))]
		public void TheTemplateTurnsAnUnderscoreIntoAnAccessKey(String control)
		{
			Func<FrameworkElement> create = control switch
			{
				"Button" => () => new Wpf.Ui.Controls.Button { Content = "_Save" },
				"CheckBox" => () => new CheckBox { Content = "_Save" },
				"RadioButton" => () => new RadioButton { Content = "_Save" },
				"ToggleSwitch" => () => new Wpf.Ui.Controls.ToggleSwitch { Content = "_Save" },
				_ => () => new Wpf.Ui.Controls.HyperlinkButton { Content = "_Save" },
			};

			Assert.True(HasAccessKey(create, 'S'), control + " does not present its content as access text");
		}
	}
}
