namespace FalloutScriptStudio.Tests.Accessibility
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The markup of the lists: the details of a row that only the real window shows.</summary>
	public sealed class ListRowMarkupTests
	{
		[Fact]
		public void TheSecondaryTextOfAListRowIsReadableOnTheSelectedRow()
		{
			// The caption style is muted grey: on the accent color of the selected row it is nearly invisible (1.8:1 in the light theme). A row of a
			// list shows its secondary text with ShellListSecondaryTextStyle, which switches to the color meant for text on the accent color.
			String views = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Views");
			List<String> offenders = [];
			foreach (String file in Directory.GetFiles(views, "*.xaml", SearchOption.AllDirectories))
			{
				XDocument document = XDocument.Load(file);
				foreach (XElement list in document.Descendants().Where(element => element.Name.LocalName == "ListBox"))
				{
					foreach (XElement text in list.Descendants().Where(element => element.Name.LocalName == "TextBlock" && ((String?)element.Attribute("Style"))?.Contains("ShellCaptionTextStyle", StringComparison.Ordinal) == true))
					{
						offenders.Add(Path.GetRelativePath(views, file) + ": " + (String?)text.Attribute("Text"));
					}
				}
			}

			Assert.True(offenders.Count == 0, "These texts of list rows use the caption style, which is unreadable on the selected row: " + String.Join("; ", offenders));
		}
	}
}