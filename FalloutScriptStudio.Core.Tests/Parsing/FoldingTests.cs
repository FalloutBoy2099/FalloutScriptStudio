namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class FoldingTests
	{
		private readonly FoldingGenerator _generator = new FoldingGenerator();
		private readonly InternalParser _parser = new InternalParser();

		[Fact]
		public void Generate_ProducesDeclarationRegionAndProcedureRegions()
		{
			String text = SslText.Lines(
				"// header",                // 1
				"#include \"a.h\"",         // 2
				"#define X 1",              // 3
				"",                         // 4
				"procedure start;",         // 5
				"",                         // 6
				"procedure start begin",    // 7
				"\tx := 1;",                // 8
				"end");                     // 9
			IReadOnlyList<ScriptProcedure> procedures = this._parser.ParseProcedures(text, @"C:\s\test.ssl");

			IReadOnlyList<FoldingRegion> regions = this._generator.Generate(text, "test.ssl", procedures);

			Assert.Equal(2, regions.Count);
			Assert.Equal(new FoldingRegion(2, 5, FoldingKind.Region, " - Declaration Region - "), regions[0]);
			Assert.Equal(new FoldingRegion(7, 9, FoldingKind.Procedure, " START "), regions[1]);
		}

		[Fact]
		public void Generate_SkipsProceduresOfOtherFiles()
		{
			String text = SslText.Lines("procedure a begin", "end");
			IReadOnlyList<ScriptProcedure> procedures = this._parser.ParseProcedures(text, @"C:\s\other.h");

			IReadOnlyList<FoldingRegion> regions = this._generator.Generate(text, "test.ssl", procedures);

			Assert.Empty(regions);
		}

		[Fact]
		public void Generate_FindsVariableAndPreprocessorBlocks()
		{
			String text = SslText.Lines(
				"#if 1",                    // 1
				"#define A 1",              // 2
				"#endif",                   // 3
				"variable begin",           // 4
				"\tx;",                     // 5
				"end",                      // 6
				"variable y;");             // 7

			IReadOnlyList<FoldingRegion> regions = this._generator.Generate(text, "a.h", Array.Empty<ScriptProcedure>());

			Assert.Equal(
				new[]
				{
					new FoldingRegion(1, 3, FoldingKind.PreprocessorIf, "#if 1 "),
					new FoldingRegion(4, 6, FoldingKind.Variables, " - Variables - "),
				},
				regions);
		}

		[Fact]
		public void Generate_VariableBlockInterruptedByProcedure_IsNotFolded()
		{
			String text = SslText.Lines("variable begin", "procedure p begin", "end");

			IReadOnlyList<FoldingRegion> regions = this._generator.Generate(text, "a.h", Array.Empty<ScriptProcedure>());

			Assert.DoesNotContain(regions, r => r.Kind == FoldingKind.Variables);
		}

		[Fact]
		public void CollapsedProcedureStore_RemembersCollapsedProceduresPerScript()
		{
			var store = new InMemoryCollapsedProcedureStore();
			var regions = new[]
			{
				new FoldingRegion(1, 3, FoldingKind.Procedure, " START "),
				new FoldingRegion(5, 8, FoldingKind.Procedure, " TALK "),
				new FoldingRegion(10, 12, FoldingKind.Variables, " - Variables - "),
			};

			store.Remember("test.ssl", regions, r => r.Title.Contains("TALK", StringComparison.Ordinal));

			Assert.True(store.IsCollapsed("TEST.SSL", "talk"));
			Assert.False(store.IsCollapsed("test.ssl", "start"));
			Assert.False(store.IsCollapsed("other.ssl", "talk"));
			Assert.Equal(new[] { regions[1] }, store.GetCollapsedRegions("test.ssl", regions));

			store.Remember("test.ssl", regions, r => false);
			Assert.Empty(store.GetCollapsedRegions("test.ssl", regions));
		}

		[Fact]
		public void CollapsedProcedureStore_ExportAndImportRoundTrip()
		{
			var store = new InMemoryCollapsedProcedureStore();
			store.SetCollapsed("a.ssl", " START ", true);
			store.SetCollapsed("b.ssl", "x", true);
			store.SetCollapsed("b.ssl", "x", false);

			var copy = new InMemoryCollapsedProcedureStore();
			copy.Import(store.Export());

			Assert.True(copy.IsCollapsed("a.ssl", "start"));
			Assert.Single(store.Export());
		}
	}
}