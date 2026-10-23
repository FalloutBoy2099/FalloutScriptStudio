namespace FalloutScriptStudio.Tests.DialoguePreview
{
	using System;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The markup of the dialogue preview window: the details that only show on the real window.</summary>
	public sealed class DialoguePreviewWindowMarkupTests
	{
		private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

		[Fact]
		public void TheLineButtonOfARowWithoutAMessageNumberIsHidden()
		{
			String path = System.IO.Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Views", "DialoguePreview", "DialoguePreviewWindow.xaml");
			XDocument window = XDocument.Load(path);

			// The button shows the message number: for a call or an exit the number is empty and the button would be an empty rounded rectangle.
			XElement button = window.Descendants().Single(element => element.Name.LocalName == "Button" && (String?)element.Attribute(Xaml + "Name") == "EditLineButton");
			Assert.Equal("{Binding LineText}", (String?)button.Attribute("Content"));
			XElement trigger = window.Descendants().Single(element => element.Name.LocalName == "DataTrigger" && (String?)element.Attribute("Binding") == "{Binding LineText}");
			Assert.Equal(String.Empty, (String?)trigger.Attribute("Value"));
			XElement setter = trigger.Elements().Single();
			Assert.Equal("EditLineButton", (String?)setter.Attribute("TargetName"));
			Assert.Equal("Visibility", (String?)setter.Attribute("Property"));
			Assert.Equal("Collapsed", (String?)setter.Attribute("Value"));
		}
	}
}