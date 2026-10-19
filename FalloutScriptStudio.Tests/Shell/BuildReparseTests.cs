namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class BuildReparseTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void A_parse_that_was_waiting_for_the_typing_pause_runs_after_the_compilation()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Compiler.CompileToSourceDirectory = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Assert.True(await Eventually.TrueAsync(() => harness.Parser.Completed >= 1 && !document.NeedsParse));
				harness.Coordinator.Debounce = TimeSpan.FromSeconds(30);
				document.TextDocument.Insert(0, "// edit\r\n");
				Assert.True(harness.Coordinator.IsPending(document));

				Boolean compiled = await harness.Build.CompileAsync(document);

				Assert.True(compiled);
				Assert.True(await Eventually.TrueAsync(() => !document.NeedsParse && harness.Parser.Texts.Any(text => text.StartsWith("// edit", StringComparison.Ordinal))));
				Assert.False(harness.Coordinator.IsPending(document));
			});
		}
	}
}