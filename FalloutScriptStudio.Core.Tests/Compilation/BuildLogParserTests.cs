namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	public class BuildLogParserTests
	{
		private const String Source = @"D:\scripts\test.ssl";

		private readonly BuildLogParser _parser = new BuildLogParser();

		[Fact]
		public void Parse_SslcErrorsWithAndWithoutColumn()
		{
			String output = "****** 12:00:00 ******\r\n----------------------\r\n"
				+ "[Error] <D:\\scripts\\test.ssl>:12:5: undeclared identifier 'foo'\r\n"
				+ "[Error] <D:\\scripts\\test.ssl>:20: syntax error\r\n"
				+ "Compilation Failed\r\n";

			IReadOnlyList<BuildError> errors = this._parser.Parse(output, Source, PreprocessorKind.Builtin);

			Assert.Equal(2, errors.Count);
			Assert.Equal(new BuildError(BuildErrorType.Error, "undeclared identifier 'foo'", Source, 12, 5), errors[0]);
			Assert.Equal(new BuildError(BuildErrorType.Error, "syntax error", Source, 20), errors[1]);
		}

		[Fact]
		public void Parse_WarningAppendsTheFollowingLine()
		{
			String output = "[Warning] <D:\\scripts\\test.ssl>:7: variable 'tmp' is never used\r\n"
				+ "    variable tmp;\r\n"
				+ "[Message] <D:\\scripts\\test.ssl>:9: just so you know\r\n";

			IReadOnlyList<BuildError> errors = this._parser.Parse(output, Source, PreprocessorKind.Builtin);

			Assert.Equal(2, errors.Count);
			Assert.Equal(BuildErrorType.Warning, errors[0].Type);
			Assert.Equal("variable 'tmp' is never used: variable tmp;", errors[0].Message);
			Assert.Equal(7, errors[0].Line);
			Assert.Equal(BuildErrorType.Message, errors[1].Type);
			Assert.Equal(9, errors[1].Line);
		}

		[Fact]
		public void Parse_McppWarningWithoutBracketTag()
		{
			String output = "D:\\scripts\\test.ssl:5: warning: Undefined macro \"FOO\"\r\n"
				+ "    FOO\r\n";

			IReadOnlyList<BuildError> errors = this._parser.Parse(output, Source, PreprocessorKind.Mcpp);

			BuildError warning = Assert.Single(errors);
			Assert.Equal(BuildErrorType.Warning, warning.Type);
			Assert.Equal(Source, warning.FileName);
			Assert.Equal(5, warning.Line);
			Assert.Equal("Undefined macro \"FOO\": FOO", warning.Message);
		}

		[Fact]
		public void Parse_ExternalPreprocessorMapsCopyBackToTheSourceScript()
		{
			String output = "[Error] <D:\\editor\\scrTemp\\test_[pre].ssl\\test.ssl>:3: boom\r\n";

			IReadOnlyList<BuildError> errors = this._parser.Parse(output, Source, PreprocessorKind.Mcpp);

			Assert.Equal(Source, Assert.Single(errors).FileName);
		}

		[Fact]
		public void Parse_RelativeFileNamesAreResolvedAgainstTheScriptFolder()
		{
			String output = "[Error] <inc/defs.h>:3: bad define\r\n";

			IReadOnlyList<BuildError> errors = this._parser.Parse(output, Source, PreprocessorKind.Builtin);

			BuildError error = Assert.Single(errors);
			Assert.Equal(Path.GetFullPath(@"D:\scripts\inc\defs.h"), error.FileName);
			Assert.Equal(3, error.Line);
		}

		[Fact]
		public void Parse_NoneFileIsKeptAsIs()
		{
			String output = "[Error] <none>:0: could not open file\r\n";

			BuildError error = Assert.Single(this._parser.Parse(output, Source, PreprocessorKind.Mcpp));

			Assert.Equal("none", error.FileName);
			Assert.Equal("could not open file", error.Message);
		}

		[Fact]
		public void Parse_UnrecognizedErrorLineBecomesAnEntryWithoutFile()
		{
			String output = "[Error] something unexpected happened\r\n";

			BuildError error = Assert.Single(this._parser.Parse(output, Source, PreprocessorKind.Builtin));

			Assert.Equal(BuildErrorType.Error, error.Type);
			Assert.Equal("none", error.FileName);
			Assert.Equal("[Error] something unexpected happened", error.Message);
		}

		[Fact]
		public void Parse_AcceptsLfOnlyLineBreaksAndEmptyInput()
		{
			Assert.Empty(this._parser.Parse(String.Empty, Source, PreprocessorKind.Builtin));

			IReadOnlyList<BuildError> errors = this._parser.Parse("[Error] <D:\\scripts\\test.ssl>:1: a\n[Error] <D:\\scripts\\test.ssl>:2: b\n", Source, PreprocessorKind.Builtin);

			Assert.Equal(2, errors.Count);
		}

		[Fact]
		public void ParserLog_SplitsErrorsWarningsAndIncludeNotes()
		{
			String log = "[Error] <D:\\scripts\\test.ssl>:12:4: syntax error\r\n"
				+ "  from D:\\headers\\defs.h:30:\r\n"
				+ "[Warning] <D:\\scripts\\test.ssl>:3: unused variable\r\n";

			ParserLogResult result = new ParserLogParser().Parse(log, "test.ssl", true, new DateTime(2026, 1, 1, 8, 5, 9));

			Assert.StartsWith("------ Script: test.ssl < Parse Time: 08:05:09 > ------\r\n", result.Text, StringComparison.Ordinal);
			Assert.Equal(2, result.Errors.Count);
			Assert.Equal(BuildErrorType.Error, result.Errors[0].Type);
			Assert.Equal(12, result.Errors[0].Line);
			Assert.Equal(4, result.Errors[0].Column);
			Assert.Equal(BuildErrorType.Warning, result.Errors[1].Type);
			BuildError note = Assert.Single(result.IncludeNotes);
			Assert.Equal(@"D:\headers\defs.h", note.FileName);
			Assert.Equal(30, note.Line);
		}

		[Fact]
		public void Parse_SslcWarningsEndWithAnEmptyLine_AndAreNotGluedToTheNextEntry()
		{
			// Real compile.exe output: every [Warning] is followed by an empty line, then the next entry.
			String output = "Compiling test.ssl\r\n"
				+ "[Warning] <test.ssl>:6:23: Variable foreach 'p' already exists.\r\n"
				+ "\r\n"
				+ "[Warning] <test.ssl>:6:26: Variable foreach 'd' already exists.\r\n"
				+ "\r\n"
				+ "*** THERE WERE ERRORS (1 of them) ***\r\n";

			IReadOnlyList<BuildError> errors = this._parser.Parse(output, Source, PreprocessorKind.Builtin);

			Assert.Equal(
				new[]
				{
					new BuildError(BuildErrorType.Warning, "Variable foreach 'p' already exists.", Path.Combine(@"D:\scripts", "test.ssl"), 6, 23),
					new BuildError(BuildErrorType.Warning, "Variable foreach 'd' already exists.", Path.Combine(@"D:\scripts", "test.ssl"), 6, 26),
				},
				errors);
		}

		[Fact]
		public void Parse_SslcSemanticErrorWithCategoryTag_YieldsFileLineColumnAndMessage()
		{
			String output = "[Error] <Semantic> <test.ssl>:7:26: Undefined symbol call_it in factor\r\n\r\n";

			BuildError error = Assert.Single(this._parser.Parse(output, Source, PreprocessorKind.Builtin));

			Assert.Equal(new BuildError(BuildErrorType.Error, "Undefined symbol call_it in factor", Path.Combine(@"D:\scripts", "test.ssl"), 7, 26), error);
		}

		[Fact]
		public void Parse_SslcMessageWithoutFile_KeepsTheNoneFile()
		{
			String output = "[Message] <none>:-1: Eliminating procedure unusedp\r\n";

			BuildError error = Assert.Single(this._parser.Parse(output, Source, PreprocessorKind.Builtin));

			Assert.Equal(BuildErrorType.Message, error.Type);
			Assert.Equal(BuildError.NoFile, error.FileName);
			Assert.Equal(-1, error.Line);
			Assert.Equal("Eliminating procedure unusedp", error.Message);
		}

		[Fact]
		public void ParserLog_CanDropWarnings()
		{
			String log = "[Warning] <D:\\scripts\\test.ssl>:3: unused variable\r\n"
				+ "[Error] <D:\\scripts\\test.ssl>:5: broken\r\n";

			ParserLogResult result = new ParserLogParser().Parse(log, "test.ssl", false, DateTime.Now);

			BuildError error = Assert.Single(result.Errors);
			Assert.Equal(BuildErrorType.Error, error.Type);
			Assert.DoesNotContain("unused variable", result.Text, StringComparison.Ordinal);
		}
	}
}