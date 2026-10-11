namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	/// <summary>The C runtime of the external tools reads a backslash before a quote as an escape; spaces split unquoted arguments.</summary>
	public class CompilerCommandLineQuotingTests
	{
		private static CompilerOptions CreateOptions()
		{
			return new CompilerOptions
			{
				ProgramDirectory = @"C:\Editor",
				ResourcesDirectory = @"C:\Editor\resources",
				ScratchDirectory = @"C:\Editor\scrTemp",
			};
		}

		[Theory]
		[InlineData(@"C:\headers", "\"C:\\headers\"")]
		[InlineData(@"C:\headers\", "\"C:\\headers\"")]
		[InlineData(@"C:\my headers\\", "\"C:\\my headers\"")]
		[InlineData(@"D:\", "\"D:\\\\\"")]
		[InlineData(@"D:", "\"D:\"")]
		[InlineData(@"\\server\share\", "\"\\\\server\\share\"")]
		public void Quote_NeverEndsWithAnEscapedQuote(String path, String expected)
		{
			Assert.Equal(expected, CompilerCommandLineBuilder.Quote(path));
		}

		[Fact]
		public void Sslc_IncludeFoldersWithATrailingBackslash_DoNotSwallowTheLine()
		{
			CompilerOptions options = CreateOptions() with { ProgramDirectory = @"C:\Editor\", SearchIncludePath = true, ScriptsHeaderFile = @"C:\h\scripts.h", HeadersDirectory = @"C:\h\" };
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(options);

			String line = builder.BuildSslc(@"C:\src\test.ssl", @"C:\src\test.int", false, false);

			Assert.StartsWith("-I\"C:\\h\" -I\"C:\\Editor\" -q", line, StringComparison.Ordinal);
			Assert.DoesNotContain("\\\"", line, StringComparison.Ordinal);
		}

		[Fact]
		public void Sslc_DriveRootAsIncludeFolder_KeepsItsBackslashDoubled()
		{
			CompilerOptions options = CreateOptions() with { ProgramDirectory = @"D:\" };

			Assert.StartsWith("-I\"D:\\\\\" -q", new CompilerCommandLineBuilder(options).BuildSslc(@"C:\src\test.ssl", @"C:\src\test.int", false, false), StringComparison.Ordinal);
		}

		[Fact]
		public void UserCompiler_FoldersWithATrailingBackslash_AreCleaned()
		{
			CompilerOptions options = CreateOptions() with
			{
				CompileToSourceDirectory = false,
				OutputDirectory = @"D:\Fallout\data\scripts\",
				SearchIncludePath = true,
				ScriptsHeaderFile = @"C:\h\scripts.h",
				HeadersDirectory = @"C:\h\",
			};

			String line = new CompilerCommandLineBuilder(options).BuildUserCompiler(@"C:\src\test.ssl", @"C:\src\", false);

			Assert.Equal("\"C:\\src\\test.ssl\" \"C:\\h\" \"D:\\Fallout\\data\\scripts\" 0 1 \"C:\\src\"", line);
		}

		[Fact]
		public void ExternalPreprocessor_HeadersFolderWithATrailingBackslash_IsCleaned()
		{
			CompilerOptions options = CreateOptions() with { SearchIncludePath = true, ScriptsHeaderFile = @"C:\h\scripts.h", HeadersDirectory = @"C:\h\" };

			String line = new CompilerCommandLineBuilder(options).BuildExternalPreprocessor(@"C:\src\test.ssl", "p.ssl", false);

			Assert.Equal("\"C:\\src\\test.ssl\" \"C:\\Editor\\scrTemp\\p.ssl\" 1 .. \"C:\\h\"", line);
		}

		[Fact]
		public void ExternalPreprocessor_ScratchFolderWithSpaces_IsAnAbsoluteQuotedPath()
		{
			// The user-profile layout: the program is in Program Files, the scratch folder in a profile with a space in its name.
			CompilerOptions options = new CompilerOptions
			{
				ProgramDirectory = @"C:\Program Files\Fallout Script Studio",
				ResourcesDirectory = @"C:\Program Files\Fallout Script Studio\resources",
				ScratchDirectory = @"C:\Users\John Smith\AppData\Local\FalloutScriptStudio\scrTemp",
			};

			String mcpp = new CompilerCommandLineBuilder(options).BuildExternalPreprocessor(@"C:\src\test.ssl", "preprocess.ssl", false);
			String wcc = new CompilerCommandLineBuilder(options with { Preprocessor = PreprocessorKind.Watcom }).BuildExternalPreprocessor(@"C:\src\test.ssl", "preprocess.ssl", false);

			Assert.StartsWith("\"C:\\src\\test.ssl\" \"C:\\Users\\John Smith\\AppData\\Local\\FalloutScriptStudio\\scrTemp\\preprocess.ssl\" 1 ..", mcpp, StringComparison.Ordinal);
			Assert.StartsWith("\"C:\\src\\test.ssl\" \"C:\\Users\\John Smith\\AppData\\Local\\FalloutScriptStudio\\scrTemp\\preprocess.ssl\" l ..", wcc, StringComparison.Ordinal);
		}
	}
}
