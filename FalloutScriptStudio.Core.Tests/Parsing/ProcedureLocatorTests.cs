namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class ProcedureLocatorTests
	{
		private readonly ProcedureLocator _locator = new ProcedureLocator();

		private static String Script()
		{
			return SslText.Lines(
				"// header",                    // 1
				"#include \"a.h\"",             // 2
				"#define X 1",                  // 3
				"",                             // 4
				"procedure start;",             // 5
				"",                             // 6
				"procedure start begin",        // 7
				"end",                          // 8
				"",                             // 9
				"procedure other",              // 10
				"begin",                        // 11
				"\tcall start;",                // 12
				"end");                         // 13
		}

		[Fact]
		public void GetProcedureBlock_FindsBodyOfProcedure()
		{
			ProcedureBlock block = this._locator.GetProcedureBlock(Script(), "START");

			Assert.Equal(7, block.BeginLine);
			Assert.Equal(8, block.EndLine);
			Assert.Equal(7, block.DeclarationLine);
		}

		[Fact]
		public void GetProcedureBlock_BeginOnNextLine_CanReturnProcedureLine()
		{
			ProcedureBlock begin = this._locator.GetProcedureBlock(Script(), "other");
			ProcedureBlock header = this._locator.GetProcedureBlock(Script(), "other", 1, true);

			Assert.Equal(11, begin.BeginLine);
			Assert.Equal(10, header.BeginLine);
			Assert.Equal(13, begin.EndLine);
		}

		[Fact]
		public void GetProcedureBlock_UnknownProcedure_ReturnsNotFoundAndWarns()
		{
			var diagnostics = new ParseDiagnostics();

			ProcedureBlock block = this._locator.GetProcedureBlock(Script(), "missing", 1, false, diagnostics);

			Assert.Equal(-1, block.BeginLine);
			Assert.Equal(-1, block.EndLine);
			Assert.Single(diagnostics);
		}

		[Fact]
		public void GetProcedureBlock_DoesNotConfusePrefixedNames()
		{
			String text = SslText.Lines("procedure start_two begin", "end", "procedure start begin", "end");

			ProcedureBlock block = this._locator.GetProcedureBlock(text, "start");

			Assert.Equal(3, block.BeginLine);
			Assert.Equal(4, block.EndLine);
		}

		[Fact]
		public void GetDeclarationLine_FindsForwardDeclarationOnly()
		{
			Assert.Equal(5, this._locator.GetDeclarationLine(Script(), "start"));
			Assert.Equal(-1, this._locator.GetDeclarationLine(Script(), "other"));
		}

		[Fact]
		public void GetDeclarationInsertLine_ReturnsLineAfterLastDeclaration()
		{
			Assert.Equal(6, this._locator.GetDeclarationInsertLine(Script()));
			Assert.Equal(-1, this._locator.GetDeclarationInsertLine("// nothing here"));
		}

		[Fact]
		public void GetDeclarationRegion_SpansIncludesDefinesAndDeclarations()
		{
			LineRange region = this._locator.GetDeclarationRegion(Script(), 7);

			Assert.Equal(2, region.StartLine);
			Assert.Equal(5, region.EndLine);
			Assert.True(region.IsValid);
		}
	}
}
