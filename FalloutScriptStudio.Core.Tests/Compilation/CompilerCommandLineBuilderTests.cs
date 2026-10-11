namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	public class CompilerCommandLineBuilderTests
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

		[Fact]
		public void Sslc_WithMcpp_DefaultOptions()
		{
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(CreateOptions());

			String line = builder.BuildSslc(@"C:\src\test.ssl", @"C:\src\test.int", false, false);

			Assert.Equal("-I\"C:\\Editor\" -q -l -O1 -d  \"test.ssl\" -o \"C:\\src\\test.int\"", line);
		}

		[Fact]
		public void Sslc_WithBuiltinPreprocessor_AddsPreprocessSwitch()
		{
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(CreateOptions() with { Preprocessor = PreprocessorKind.Builtin });

			Assert.Equal(
				"-I\"C:\\Editor\" -p -q -l -O1 -d  \"test.ssl\" -o \"out.int\"",
				builder.BuildSslc(@"C:\src\test.ssl", "out.int", false, false));
			Assert.Equal(
				"-I\"C:\\Editor\" -P -q -l -O1 -d  \"test.ssl\" -o \"pre.ssl\"",
				builder.BuildSslc(@"C:\src\test.ssl", "pre.ssl", true, false));
		}

		[Fact]
		public void Sslc_WithAllOptions()
		{
			CompilerOptions options = CreateOptions() with
			{
				Optimization = OptimizationLevel.Full,
				BackwardCompatibility = true,
				ShowWarnings = false,
				DebugInfo = false,
				PreprocessorDefine = "DEBUG",
				SearchIncludePath = true,
				ScriptsHeaderFile = @"C:\h\scripts.h",
				HeadersDirectory = @"C:\h",
			};
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(options);

			String line = builder.BuildSslc(@"C:\src\test.ssl", @"C:\src\test.int", false, true);

			Assert.Equal("-I\"C:\\h\" -I\"C:\\Editor\" -q -l -O2 -b -n -mDEBUG -s \"test.ssl\" -o \"C:\\src\\test.int\"", line);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		public void BlankPreprocessorDefine_IsTreatedAsNoDefine(String define)
		{
			// compile.exe -m (and mcpp -D) with an empty macro fail with: Not an identifier "1".
			CompilerOptions options = CreateOptions() with { PreprocessorDefine = define, Preprocessor = PreprocessorKind.Mcpp };
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(options);

			Assert.DoesNotContain("-m", builder.BuildSslc(@"C:\src\test.ssl", @"C:\src\test.int", false, false), StringComparison.Ordinal);
			Assert.DoesNotContain("-D", builder.BuildExternalPreprocessor(@"C:\src\test.ssl", "preprocess.ssl", false), StringComparison.Ordinal);
			Assert.DoesNotContain("-d", builder.BuildExternalPreprocessor(@"C:\src\test.ssl", "preprocess.ssl", false) + builder.BuildUserCompiler(@"C:\src\test.ssl", @"C:\src", false).Replace("-d", String.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
			Assert.Contains(" 0 1 ", builder.BuildUserCompiler(@"C:\src\test.ssl", @"C:\src", false), StringComparison.Ordinal);
		}

		[Fact]
		public void Sslc_IncludeFolderIsIgnoredWithoutScriptsHeader()
		{
			CompilerOptions options = CreateOptions() with { SearchIncludePath = true, HeadersDirectory = @"C:\h", ScriptsHeaderFile = null };

			String line = new CompilerCommandLineBuilder(options).BuildSslc("a.ssl", "a.int", false, false);

			Assert.StartsWith("-I\"C:\\Editor\" -q", line, StringComparison.Ordinal);
		}

		[Fact]
		public void Mcpp_DefaultOptions()
		{
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(CreateOptions());

			String line = builder.BuildExternalPreprocessor(@"C:\src\test.ssl", "preprocess.ssl", false);

			Assert.Equal("\"C:\\src\\test.ssl\" ..\\scrTemp\\preprocess.ssl 1 ..", line);
		}

		[Fact]
		public void Mcpp_WithDefineIncludesAndPreprocessOnly()
		{
			CompilerOptions options = CreateOptions() with
			{
				ShowWarnings = false,
				PreprocessorDefine = "FOO=1",
				ScriptsHeaderFile = @"C:\h\scripts.h",
				HeadersDirectory = @"C:\h",
			};

			String line = new CompilerCommandLineBuilder(options).BuildExternalPreprocessor(@"C:\src\test.ssl", "x.tmp", true);

			Assert.Equal("\"C:\\src\\test.ssl\" ..\\scrTemp\\x.tmp 0 .. \"C:\\h\" -DFOO=1 -P", line);
		}

		[Fact]
		public void Watcom_CommandLine()
		{
			CompilerOptions options = CreateOptions() with
			{
				Preprocessor = PreprocessorKind.Watcom,
				PreprocessorDefine = "FOO",
				ScriptsHeaderFile = @"C:\h\scripts.h",
				HeadersDirectory = @"C:\h",
			};
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(options);

			Assert.Equal("\"C:\\src\\test.ssl\" ..\\scrTemp\\p.ssl l .. \"C:\\h\" -dFOO", builder.BuildExternalPreprocessor(@"C:\src\test.ssl", "p.ssl", false));
			Assert.Equal("\"C:\\src\\test.ssl\" ..\\scrTemp\\p.ssl c .. \"C:\\h\" -dFOO", builder.BuildExternalPreprocessor(@"C:\src\test.ssl", "p.ssl", true));
		}

		[Fact]
		public void UserCompiler_DefaultsToSourceFolder()
		{
			CompilerCommandLineBuilder builder = new CompilerCommandLineBuilder(CreateOptions());

			String line = builder.BuildUserCompiler(@"C:\src\test.ssl", @"C:\src", false);

			Assert.Equal("\"C:\\src\\test.ssl\" ..\\ \"C:\\src\" 0 1 \"C:\\src\"", line);
		}

		[Fact]
		public void UserCompiler_WithOutputFolderHeadersDefineAndShortCircuit()
		{
			CompilerOptions options = CreateOptions() with
			{
				CompileToSourceDirectory = false,
				OutputDirectory = @"D:\out",
				HeadersDirectory = @"C:\h",
				PreprocessorDefine = "REL",
				Optimization = OptimizationLevel.Full,
			};

			String line = new CompilerCommandLineBuilder(options).BuildUserCompiler(@"C:\src\test.ssl", @"C:\src", true);

			Assert.Equal("\"C:\\src\\test.ssl\" \"C:\\h\" \"D:\\out\" REL 2 \"C:\\src\" -s", line);
		}

		[Fact]
		public void Decompiler_CommandLine()
		{
			CompilerCommandLineBuilder defaults = new CompilerCommandLineBuilder(CreateOptions());
			CompilerCommandLineBuilder fallout1 = new CompilerCommandLineBuilder(CreateOptions() with { DecompileForFallout1 = true, TabSize = 4 });
			CompilerCommandLineBuilder noSpaces = new CompilerCommandLineBuilder(CreateOptions() with { TabsToSpaces = false });

			Assert.Equal(" -s3 \"a.int\" \"b.ssl\"", defaults.BuildDecompiler("a.int", "b.ssl"));
			Assert.Equal("-1 -s4 \"a.int\" \"b.ssl\"", fallout1.BuildDecompiler("a.int", "b.ssl"));
			Assert.Equal(" \"a.int\" \"b.ssl\"", noSpaces.BuildDecompiler("a.int", "b.ssl"));
		}
	}
}
