namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.IO;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Xunit;

	/// <summary>The batch files shipped in <c>resources</c> get quoted paths (spaces in the profile, a trailing backslash) and must be CRLF files for cmd.exe.</summary>
	public class ShippedBatchFilesTests
	{
		private static String Read(String name)
		{
			return File.ReadAllText(Path.Combine(RepositoryFiles.Root, "FalloutScriptStudio", "Content", "resources", name));
		}

		[Theory]
		[InlineData("mcpp.bat")]
		[InlineData("wcc.bat")]
		[InlineData("usercomp.bat")]
		public void BatchFile_UsesCrLfLineEndings(String name)
		{
			String text = Read(name);

			Assert.DoesNotMatch(new Regex("(?<!\r)\n"), text);
		}

		[Theory]
		[InlineData("mcpp.bat")]
		[InlineData("wcc.bat")]
		public void Preprocessor_ReadsThePathsWithoutTheirQuotesAndQuotesThemAgain(String name)
		{
			String text = Read(name);

			Assert.Contains("set \"pfile=%~2\"", text, StringComparison.Ordinal);
			Assert.Contains("set \"fscript=%~1\"", text, StringComparison.Ordinal);
			Assert.Contains("if not exist \"%pfile%\" goto FAILED", text, StringComparison.Ordinal);
			Assert.Contains("\"%fscript%\"", text, StringComparison.Ordinal);
			Assert.DoesNotContain("set pfile=%2", text, StringComparison.Ordinal);
		}

		[Fact]
		public void UserCompiler_TakesTheScratchFolderFromTheEnvironment()
		{
			String text = Read("usercomp.bat");

			Assert.Contains("%FSS_SCRATCH%", text, StringComparison.Ordinal);
			Assert.Contains("if exist \"%prefile%\" del \"%prefile%\"", text, StringComparison.Ordinal);
			Assert.Contains("if not exist \"%~1\" goto NOFILE", text, StringComparison.Ordinal);
			Assert.DoesNotContain("set prefile=..\\scrTemp", text, StringComparison.Ordinal);
		}
	}
}