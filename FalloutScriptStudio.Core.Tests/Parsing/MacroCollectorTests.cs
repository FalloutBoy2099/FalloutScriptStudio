namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class MacroCollectorTests
	{
		private static MacroCollector CreateCollector()
		{
			return new MacroCollector(new IncludeResolver(null, null));
		}

		[Fact]
		public void CollectFromText_ReadsSimpleAndParameterizedDefines()
		{
			String text = SslText.Lines(
				"#define SIMPLE 5",
				"#define MAX(a, b) ((a) > (b) ? (a) : (b))",
				"#define\tTABBED   \"text\"");

			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText(text, @"C:\s\a.ssl");

			Assert.Equal("5", macros["SIMPLE"].Code);
			Assert.Equal(1, macros["SIMPLE"].DeclaredLine);

			ScriptMacro max = macros["MAX"];
			Assert.Equal("MAX(a, b)", max.DefinitionName);
			Assert.Equal("((a) > (b) ? (a) : (b))", max.Code);
			Assert.Equal(2, max.DeclaredLine);
			Assert.Equal(@"C:\s\a.ssl", max.DeclaredFile);

			Assert.Equal("\"text\"", macros["TABBED"].Code);
		}

		[Fact]
		public void CollectFromText_SkipsCommentedDefines()
		{
			String text = SslText.Lines(
				"// #define LINE 1",
				"/* #define BLOCK 1 */",
				"/*",
				"#define INSIDE 1",
				"*/",
				"#define REAL 1");

			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText(text, null);

			Assert.Equal(new[] { "REAL" }, macros.Keys);
		}

		[Fact]
		public void CollectFromText_JoinsContinuationLines()
		{
			String text = SslText.Lines(
				"#define MULTI(x) \\",
				"\tdo_something(x); \\",
				"\tmore(x)",
				"#define AFTER 1");

			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText(text, null);

			Assert.Equal("MULTI(x)", macros["MULTI"].DefinitionName);
			Assert.Equal("do_something(x);\nmore(x)", macros["MULTI"].Code);
			Assert.Equal(1, macros["MULTI"].DeclaredLine);
			Assert.Equal(4, macros["AFTER"].DeclaredLine);
		}

		[Fact]
		public void CollectFromText_UsesDocumentationCommentAsDescription()
		{
			String text = SslText.Lines(
				"#define PLAIN 1",
				"",
				"/** Maximum of two values **/",
				"#define MAX(a, b) ((a) > (b) ? (a) : (b))",
				"/** First line",
				" * second line **/",
				"#define DOCUMENTED 2");

			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText(text, null);

			Assert.Null(macros["PLAIN"].Description);
			Assert.Equal("Maximum of two values\n", macros["MAX"].Description);
			Assert.Equal("First line\n* second line\n", macros["DOCUMENTED"].Description);
			Assert.Contains("Maximum of two values", macros["MAX"].FullDescription, StringComparison.Ordinal);
		}

		[Fact]
		public void CollectFromText_IgnoresDefinesWithoutValue()
		{
			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText("#define GUARD_H\r\n#define OK 1", null);

			Assert.Equal(new[] { "OK" }, macros.Keys);
		}

		[Fact]
		public void CollectFromText_FollowsIncludesAndSurvivesCircularIncludes()
		{
			using var dir = new TempDirectory();
			dir.Write("inc.h", SslText.Lines("#include \"inc2.h\"", "#define FROM_INC 7"));
			dir.Write("inc2.h", SslText.Lines("#include \"inc.h\"", "#define FROM_INC2 8"));
			String scriptPath = dir.Write("main.ssl", "#include \"inc.h\"");
			var diagnostics = new ParseDiagnostics();

			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText(File.ReadAllText(scriptPath), scriptPath, true, diagnostics);

			Assert.Equal("7", macros["FROM_INC"].Code);
			Assert.Equal(Path.Combine(dir.Path, "inc.h"), macros["FROM_INC"].DeclaredFile);
			Assert.Equal("8", macros["FROM_INC2"].Code);
			Assert.Empty(diagnostics);
		}

		[Fact]
		public void CollectFromText_MissingInclude_IsReportedAsInfo()
		{
			using var dir = new TempDirectory();
			String scriptPath = Path.Combine(dir.Path, "main.ssl");
			var diagnostics = new ParseDiagnostics();

			CreateCollector().CollectFromText("#include \"nothing.h\"", scriptPath, true, diagnostics);

			ParseDiagnostic info = Assert.Single(diagnostics);
			Assert.Equal(ParseDiagnosticSeverity.Info, info.Severity);
		}

		[Fact]
		public void CollectFromText_CanSkipIncludes()
		{
			using var dir = new TempDirectory();
			dir.Write("inc.h", "#define FROM_INC 7");
			String scriptPath = Path.Combine(dir.Path, "main.ssl");

			IDictionary<String, ScriptMacro> macros = CreateCollector().CollectFromText("#include \"inc.h\"\r\n#define LOCAL 1", scriptPath, false);

			Assert.Equal(new[] { "LOCAL" }, macros.Keys);
		}

		[Fact]
		public void FullDescription_ContainsDefinitionDescriptionCodeAndFile()
		{
			var macro = new ScriptMacro("MAX", "MAX(a, b)", "((a) > (b) ? (a) : (b))", @"C:\h\define.h", 3, "Larger value\n");

			Assert.Equal("Define: MAX(a, b)", macro.ShortDescription);
			Assert.Equal("Define: MAX(a, b)\n - Larger value\n\n((a) > (b) ? (a) : (b))\n\nDeclare file: define.h", macro.FullDescription);
		}

		[Fact]
		public void MacroRegistry_LoadsHeadersRecursivelyAndKeepsContentWhenDirectoryIsMissing()
		{
			using var dir = new TempDirectory();
			dir.Write("define.h", "#define GLOBAL_A 1");
			dir.Write(Path.Combine("sub", "more.h"), "#define GLOBAL_B 2");
			dir.Write("ignored.txt", "#define NOT_A_HEADER 3");
			var registry = new MacroRegistry();
			var diagnostics = new ParseDiagnostics();

			Int32 count = registry.LoadFromDirectory(dir.Path, diagnostics);
			Int32 missing = registry.LoadFromDirectory(Path.Combine(dir.Path, "nope"), diagnostics);

			Assert.Equal(2, count);
			Assert.Equal(-1, missing);
			Assert.True(registry.Contains("GLOBAL_A"));
			Assert.True(registry.Contains("GLOBAL_B"));
			Assert.False(registry.Contains("global_a"));
			Assert.False(registry.Contains("NOT_A_HEADER"));
			Assert.Equal(dir.Path, registry.SourceDirectory);
			Assert.Single(diagnostics);
		}
	}
}
