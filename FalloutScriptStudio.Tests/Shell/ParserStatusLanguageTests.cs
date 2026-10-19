namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.ComponentModel;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class ParserStatusLanguageTests
	{
		[Fact]
		public void The_parser_status_text_is_recomputed_when_the_language_changes()
		{
			LanguageCollection.CollectGarbage();
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\nend\r\n");
				document.ParserStatus = new ParserStatus(ParserStatusKind.Running);
				Boolean raised = false;
				((INotifyPropertyChanged)harness.Main).PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(harness.Main.ParserStatusText);

				try
				{
					Loc.SetLanguage("fr");

					Assert.True(raised);
					Assert.Equal(document.ParserStatus.ToText(), harness.Main.ParserStatusText);
				}
				finally
				{
					Loc.SetLanguage(Loc.DefaultLanguage);
				}
			});
		}

		[Fact]
		public void A_status_is_translated_when_it_is_read_and_not_when_it_is_stored()
		{
			ParserStatus waiting = new ParserStatus(ParserStatusKind.Waiting);

			Assert.Equal("Parser: Waiting...", waiting.ToText());
			Assert.Equal(String.Empty, ParserStatus.None.ToText());
			Assert.Equal(ParserStatusKind.Waiting, waiting.Kind);
		}
	}
}