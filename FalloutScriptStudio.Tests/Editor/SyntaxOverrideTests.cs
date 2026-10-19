namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Highlighting;
	using Microsoft.Extensions.Logging;
	using Xunit;

	/// <summary>The syntax definitions the user can replace with a file in <c>resources\syntax</c>.</summary>
	public sealed class SyntaxOverrideTests : IDisposable
	{
		private const String CustomMessageSyntax =
			"<SyntaxDefinition name=\"Message\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">\r\n"
			+ "\t<Color name=\"Number\" />\r\n"
			+ "\t<Color name=\"LineComment\" />\r\n"
			+ "\t<Color name=\"TrailingComment\" />\r\n"
			+ "\t<Color name=\"Emotion\" />\r\n"
			+ "\t<Color name=\"Braces\" />\r\n"
			+ "\t<RuleSet>\r\n"
			+ "\t\t<Keywords color=\"Emotion\">\r\n"
			+ "\t\t\t<Word>CUSTOMWORD</Word>\r\n"
			+ "\t\t</Keywords>\r\n"
			+ "\t</RuleSet>\r\n"
			+ "</SyntaxDefinition>\r\n";

		private readonly String _directory = TestFiles.CreateWorkFolder("syntax");

		public SyntaxOverrideTests()
		{
			Directory.CreateDirectory(this._directory);
		}

		public void Dispose()
		{
			TestFiles.DeleteWorkFolder(this._directory);
		}

		private static String? ColorOf(IHighlightingDefinition definition, String text, String word)
		{
			TextDocument document = new TextDocument(text);
			DocumentHighlighter highlighter = new DocumentHighlighter(document, definition);
			HighlightedLine line = highlighter.HighlightLine(1);
			Int32 start = text.IndexOf(word, StringComparison.Ordinal);
			return line.Sections.LastOrDefault(s => s.Offset <= start && start < s.Offset + s.Length)?.Color?.Name;
		}

		[Fact]
		public void WithoutAnOverrideFile_TheEmbeddedDefinitionIsUsed()
		{
			UiThread.Run(() =>
			{
				HighlightingProvider provider = new HighlightingProvider(this._directory, null);

				IHighlightingDefinition definition = provider.Create(HighlightingKind.Message, SyntaxThemes.Dark);

				Assert.Equal("Number", ColorOf(definition, "{100}{}{Hello}", "100"));
			});
		}

		[Fact]
		public void AnOverrideFile_ReplacesTheEmbeddedDefinition()
		{
			UiThread.Run(() =>
			{
				File.WriteAllText(Path.Combine(this._directory, "Msg.xshd"), CustomMessageSyntax);
				HighlightingProvider provider = new HighlightingProvider(this._directory, null);

				IHighlightingDefinition definition = provider.Create(HighlightingKind.Message, SyntaxThemes.Dark);

				Assert.Equal("Emotion", ColorOf(definition, "CUSTOMWORD 100", "CUSTOMWORD"));
				Assert.NotEqual("Number", ColorOf(definition, "CUSTOMWORD 100", "100"));
			});
		}

		[Fact]
		public void AnOverrideFile_IsPickedUpWithoutRestartingTheProvider()
		{
			UiThread.Run(() =>
			{
				HighlightingProvider provider = new HighlightingProvider(this._directory, null);
				Assert.Equal("Number", ColorOf(provider.Create(HighlightingKind.Message, SyntaxThemes.Dark), "100", "100"));

				File.WriteAllText(Path.Combine(this._directory, "Msg.xshd"), CustomMessageSyntax);

				Assert.Equal("Emotion", ColorOf(provider.Create(HighlightingKind.Message, SyntaxThemes.Dark), "CUSTOMWORD", "CUSTOMWORD"));
			});
		}

		[Fact]
		public void AnOverrideFileOfAnotherKind_IsIgnored()
		{
			UiThread.Run(() =>
			{
				File.WriteAllText(Path.Combine(this._directory, "Msg.xshd"), CustomMessageSyntax);
				HighlightingProvider provider = new HighlightingProvider(this._directory, null);

				IHighlightingDefinition script = provider.Create(HighlightingKind.Ssl, SyntaxThemes.Dark);

				Assert.Equal("Number", ColorOf(script, "x := 100;", "100"));
			});
		}

		[Fact]
		public void ABrokenOverrideFile_FallsBackToTheEmbeddedDefinitionAndLogsAWarning()
		{
			UiThread.Run(() =>
			{
				File.WriteAllText(Path.Combine(this._directory, "Msg.xshd"), "<SyntaxDefinition this is not xml");
				ListLogger logger = new ListLogger();
				HighlightingProvider provider = new HighlightingProvider(this._directory, logger);

				IHighlightingDefinition definition = provider.Create(HighlightingKind.Message, SyntaxThemes.Dark);

				Assert.Equal("Number", ColorOf(definition, "{100}{}{Hello}", "100"));
				Assert.Contains(LogLevel.Warning, logger.Levels);
			});
		}

		[Fact]
		public void AnXmlFileThatIsNoDefinition_FallsBackAndLogsAWarning()
		{
			UiThread.Run(() =>
			{
				File.WriteAllText(Path.Combine(this._directory, "Msg.xshd"), "<html><body>hello</body></html>");
				ListLogger logger = new ListLogger();
				HighlightingProvider provider = new HighlightingProvider(this._directory, logger);

				IHighlightingDefinition definition = provider.Create(HighlightingKind.Message, SyntaxThemes.Dark);

				Assert.Equal("Number", ColorOf(definition, "{100}{}{Hello}", "100"));
				Assert.Contains(LogLevel.Warning, logger.Levels);
			});
		}

		[Fact]
		public void AScriptDefinitionWithoutTheColorsTheEditorNeeds_FallsBackAndLogsAWarning()
		{
			UiThread.Run(() =>
			{
				const String incomplete =
					"<SyntaxDefinition name=\"SSL\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">\r\n"
					+ "\t<Color name=\"Number\" />\r\n"
					+ "\t<RuleSet><Rule color=\"Number\">\\d+</Rule></RuleSet>\r\n"
					+ "</SyntaxDefinition>\r\n";
				File.WriteAllText(Path.Combine(this._directory, "Ssl.xshd"), incomplete);
				ListLogger logger = new ListLogger();
				HighlightingProvider provider = new HighlightingProvider(this._directory, logger);

				IHighlightingDefinition definition = provider.Create(HighlightingKind.Ssl, SyntaxThemes.Dark);

				Assert.Equal("StandardProcedures", ColorOf(definition, "procedure start begin", "start"));
				Assert.Contains(LogLevel.Warning, logger.Levels);
			});
		}

		[Fact]
		public void TheEditorServices_LookInTheSyntaxFolderOfTheResources()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(baseDirectory: this._directory);
				String folder = Path.Combine(env.SettingsService.Paths.ResourcesDirectory, ScriptEditorServices.SyntaxDirectoryName);
				Directory.CreateDirectory(folder);
				File.WriteAllText(Path.Combine(folder, "Msg.xshd"), CustomMessageSyntax);

				IHighlightingDefinition definition = env.Services.Highlighting.Create(HighlightingKind.Message, SyntaxThemes.Dark);

				Assert.Equal("Emotion", ColorOf(definition, "CUSTOMWORD", "CUSTOMWORD"));
			});
		}

		private sealed class ListLogger : ILogger
		{
			public List<LogLevel> Levels { get; } = [];

			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull
			{
				return null;
			}

			public Boolean IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, String> formatter)
			{
				this.Levels.Add(logLevel);
			}
		}
	}
}