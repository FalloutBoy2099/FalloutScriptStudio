namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class InternalParserTests
	{
		private readonly InternalParser _parser = new InternalParser();

		[Fact]
		public void ParseProcedures_FindsDeclarationBeginAndEndLines()
		{
			IReadOnlyList<ScriptProcedure> procedures = this._parser.ParseProcedures(SslText.BasicScript(), @"C:\scripts\test.ssl");

			Assert.Equal(new[] { "start", "Talk_p_proc" }, procedures.Select(p => p.Name));

			ScriptProcedure start = procedures[0];
			Assert.Equal(5, start.DeclaredLine);
			Assert.Equal(8, start.StartLine);
			Assert.Equal(8, start.DefinedLine);
			Assert.Equal(10, start.EndLine);
			Assert.True(start.HasForwardDeclaration);
			Assert.True(start.IsStandard);
			Assert.Equal("test.ssl", start.FileName);
			Assert.Equal(Path.GetFullPath(@"C:\scripts\test.ssl"), start.DeclaredFile);

			ScriptProcedure talk = procedures[1];
			Assert.Equal(6, talk.DeclaredLine);
			Assert.Equal(12, talk.StartLine);
			Assert.Equal(14, talk.EndLine);
			Assert.Equal("talk_p_proc", talk.NormalizedName);
		}

		[Fact]
		public void ParseProcedures_WithoutForwardDeclaration_UsesBodyLineAsDeclaration()
		{
			String text = SslText.Lines("procedure alone begin", "end");

			ScriptProcedure procedure = Assert.Single(this._parser.ParseProcedures(text, "a.ssl"));

			Assert.Equal(1, procedure.StartLine);
			Assert.Equal(1, procedure.DeclaredLine);
			Assert.False(procedure.HasForwardDeclaration);
			Assert.Equal(2, procedure.EndLine);
		}

		[Fact]
		public void ParseProcedures_IgnoresCommentedCode()
		{
			String text = SslText.Lines(
				"// procedure fake1 begin",                      // 1
				"/* procedure fake2 begin",                      // 2
				"end",                                           // 3
				"procedure fake3 begin */",                      // 4
				"procedure real begin /* trailing */",           // 5
				"\tx := 1; // end",                              // 6
				"end // closes real");                           // 7

			ScriptProcedure procedure = Assert.Single(this._parser.ParseProcedures(text, "a.ssl"));

			Assert.Equal("real", procedure.Name);
			Assert.Equal(5, procedure.StartLine);
			Assert.Equal(7, procedure.EndLine);
		}

		[Fact]
		public void ParseProcedures_ReadsModifiersAndArguments()
		{
			String text = SslText.Lines(
				"import procedure Imported;",                            // 1
				"export procedure Exported begin",                       // 2
				"end",                                                   // 3
				"critical procedure Crit(variable a, variable b) begin", // 4
				"end",                                                   // 5
				"pure inline procedure Both begin",                      // 6
				"end");                                                  // 7

			IReadOnlyList<ScriptProcedure> procedures = this._parser.ParseProcedures(text, "a.ssl");

			Assert.Equal(new[] { "Imported", "Exported", "Crit", "Both" }, procedures.Select(p => p.Name));
			Assert.True(procedures[0].IsImported);
			Assert.Equal(-1, procedures[0].StartLine);
			Assert.True(procedures[1].IsExported);
			Assert.Equal(ProcedureFlags.Critical, procedures[2].Flags);
			Assert.Equal(4, procedures[2].StartLine);
			Assert.Equal(ProcedureFlags.Pure | ProcedureFlags.Inline, procedures[3].Flags);
			Assert.Equal(7, procedures[3].EndLine);
		}

		[Fact]
		public void ParseProcedures_ModifiersAreCaseInsensitive()
		{
			String text = SslText.Lines("Export Procedure Mixed_Case Begin", "End");

			ScriptProcedure procedure = Assert.Single(this._parser.ParseProcedures(text, "a.ssl"));

			Assert.Equal("Mixed_Case", procedure.Name);
			Assert.True(procedure.IsExported);
			Assert.Equal(2, procedure.EndLine);
		}

		[Fact]
		public void ParseProcedures_BeginOnNextLine_StartsAtProcedureLine()
		{
			String text = SslText.Lines("procedure split", "begin", "\tx := 1;", "end");

			ScriptProcedure procedure = Assert.Single(this._parser.ParseProcedures(text, "a.ssl"));

			Assert.Equal(1, procedure.StartLine);
			Assert.Equal(4, procedure.EndLine);
		}

		[Fact]
		public void ParseProcedures_MissingEnd_ReportsError()
		{
			String text = SslText.Lines("procedure first begin", "procedure second begin", "end");
			var diagnostics = new ParseDiagnostics();

			IReadOnlyList<ScriptProcedure> procedures = this._parser.ParseProcedures(text, "a.ssl", diagnostics);

			Assert.Equal(-1, procedures[0].EndLine);
			Assert.Equal(3, procedures[1].EndLine);
			ParseDiagnostic error = Assert.Single(diagnostics);
			Assert.Equal(ParseDiagnosticSeverity.Error, error.Severity);
			Assert.Equal(1, error.Line);
			Assert.Contains("'first'", error.Message, StringComparison.Ordinal);
			Assert.Equal("[Error] <Internal Parser> Line: 1 : " + error.Message, error.ToString());
		}

		[Fact]
		public void ParseProcedures_DeclarationWithoutBody_ReportsWarningUnlessImported()
		{
			String text = SslText.Lines("procedure lonely;", "import procedure fine;");
			var diagnostics = new ParseDiagnostics();

			this._parser.ParseProcedures(text, "a.ssl", diagnostics);

			ParseDiagnostic warning = Assert.Single(diagnostics);
			Assert.Equal(ParseDiagnosticSeverity.Warning, warning.Severity);
			Assert.Contains("'lonely'", warning.Message, StringComparison.Ordinal);
		}

		[Fact]
		public void Parse_ReturnsParsedInfoWithoutNativeData()
		{
			ProgramInfo info = this._parser.Parse(SslText.BasicScript(), @"C:\scripts\test.ssl");

			Assert.True(info.IsParsed);
			Assert.False(info.HasNativeData);
			Assert.False(info.HasParseError);
			Assert.Equal(2, info.Procedures.Count);
			Assert.Equal(Path.GetFullPath(@"C:\scripts\test.ssl"), info.ScriptFilePath);
		}

		[Fact]
		public void MergeProcedures_UpdatesLinesAddsAndRemovesProcedures()
		{
			ProgramInfo previous = this._parser.Parse(SslText.BasicScript(), "test.ssl");
			ScriptVariable argument = new ScriptVariable("x", VariableScope.Local);
			ScriptProcedure withData = new ScriptProcedure(previous.Procedures[0].Name)
			{
				DeclaredFile = previous.Procedures[0].DeclaredFile,
				StartFile = previous.Procedures[0].StartFile,
				DeclaredLine = 5,
				StartLine = 8,
				EndLine = 10,
				References = new[] { new SourceReference("test.ssl", 9) },
				Variables = new[] { argument },
			};
			previous = previous.WithProcedures(new[] { withData, previous.Procedures[1] });

			// Two lines are inserted above, Talk_p_proc is renamed (so it disappears), "added" is new.
			String edited = SslText.Lines(
				"// Test script",
				"// extra",
				"// extra",
				"procedure start;",
				"procedure start begin",
				"end",
				"procedure added begin",
				"end");

			ProgramInfo merged = this._parser.MergeProcedures(previous, edited, "test.ssl");

			Assert.Equal(new[] { "start", "added" }, merged.Procedures.Select(p => p.Name));
			ScriptProcedure start = merged.Procedures[0];
			Assert.Equal(4, start.DeclaredLine);
			Assert.Equal(5, start.StartLine);
			Assert.Equal(6, start.EndLine);
			Assert.Same(argument, start.Variables[0]);
			Assert.Equal(9, start.References[0].Line);
			Assert.Equal(7, merged.Procedures[1].StartLine);
			Assert.NotSame(previous, merged);
			Assert.Equal(2, previous.Procedures.Count);
		}

		[Fact]
		public void RefreshProcedureLocations_UpdatesLinesOnly()
		{
			ProgramInfo info = this._parser.Parse(SslText.BasicScript(), "test.ssl");
			String edited = "\r\n\r\n" + SslText.BasicScript();

			ProgramInfo refreshed = this._parser.RefreshProcedureLocations(info, edited, "test.ssl");

			Assert.Equal(info.Procedures.Count, refreshed.Procedures.Count);
			Assert.Equal(10, refreshed.Procedures[0].StartLine);
			Assert.Equal(12, refreshed.Procedures[0].EndLine);
			Assert.Equal(8, info.Procedures[0].StartLine);
		}

		[Fact]
		public void RebindToFile_MovesOnlyEntitiesOfTheOldFile()
		{
			ProgramInfo info = this._parser.Parse(SslText.BasicScript(), @"C:\scripts\old.ssl");
			ScriptProcedure foreign = new ScriptProcedure("ext")
			{
				DeclaredFile = @"C:\headers\other.h",
				StartFile = @"C:\headers\other.h",
			};
			info = info.WithProcedures(info.Procedures.Concat(new[] { foreign })).WithFlags(needsDataRefresh: true);

			ProgramInfo moved = this._parser.RebindToFile(info, @"C:\scripts\new.ssl");

			Assert.All(moved.Procedures.Take(2), p => Assert.Equal(Path.GetFullPath(@"C:\scripts\new.ssl"), p.DeclaredFile));
			Assert.Equal(@"C:\headers\other.h", moved.Procedures[2].DeclaredFile);
			Assert.False(moved.NeedsDataRefresh);
			Assert.Equal(Path.GetFullPath(@"C:\scripts\new.ssl"), moved.ScriptFilePath);
		}

		[Fact]
		public void ProcedureBeginFixer_JoinsLoneBeginLines()
		{
			String text = SslText.Lines("procedure a", "begin", "end", "critical procedure b", "begin", "end", "procedure c begin", "end");

			String fixedText = new ProcedureBeginFixer().FixText(text);

			Assert.Equal(
				SslText.Lines("procedure a begin", "end", "critical procedure b begin", "end", "procedure c begin", "end"),
				fixedText);
		}

		[Fact]
		public void ProcedureBeginFixer_FixFile_RewritesTheFile()
		{
			using var dir = new TempDirectory();
			String path = dir.Write("a.ssl", SslText.Lines("procedure a", "begin", "end"));

			Boolean changed = new ProcedureBeginFixer().FixFile(path);

			Assert.True(changed);
			Assert.Equal(SslText.Lines("procedure a begin", "end"), File.ReadAllText(path));
			Assert.False(new ProcedureBeginFixer().FixFile(path));
		}
	}
}
