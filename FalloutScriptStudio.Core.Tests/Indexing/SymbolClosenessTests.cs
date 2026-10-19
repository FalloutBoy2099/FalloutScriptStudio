namespace FalloutScriptStudio.Core.Tests.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Indexing;
	using Xunit;

	public class SymbolClosenessTests
	{
		private static IndexedSymbol Symbol(String file, Int32 line = 1, Boolean hasBody = true)
		{
			return new IndexedSymbol("x", IndexedSymbolKind.Macro, file, line) { HasBody = hasBody };
		}

		[Fact]
		public void Order_PutsTheCurrentFileFirst_ThenTheIncludedFiles_ThenTheOthers()
		{
			IndexedSymbol current = Symbol(@"C:\mod\scripts\a.ssl");
			IndexedSymbol included = Symbol(@"C:\mod\headers\h.h");
			IndexedSymbol sibling = Symbol(@"C:\mod\scripts\b.ssl");
			IndexedSymbol far = Symbol(@"C:\other\z.h");
			HashSet<String> includes = new(StringComparer.OrdinalIgnoreCase) { included.File };

			IReadOnlyList<IndexedSymbol> ordered = SymbolCloseness.Order(new[] { far, sibling, included, current }, @"C:\mod\scripts\a.ssl", includes);

			Assert.Equal(new[] { current, included, sibling, far }, ordered.ToArray());
		}

		[Fact]
		public void Order_AmongTheOthersTheNearestFolderComesFirst()
		{
			IndexedSymbol same = Symbol(@"C:\mod\scripts\b.ssl");
			IndexedSymbol parent = Symbol(@"C:\mod\c.h");
			IndexedSymbol cousin = Symbol(@"C:\mod\other\d.h");
			IndexedSymbol far = Symbol(@"D:\x\y\z\e.h");

			IReadOnlyList<IndexedSymbol> ordered = SymbolCloseness.Order(new[] { far, cousin, parent, same }, @"C:\mod\scripts\a.ssl");

			Assert.Equal(new[] { same, parent, cousin, far }, ordered.ToArray());
		}

		[Fact]
		public void Order_ADefinitionWithABodyComesBeforeADeclaration_InTheSameGroup()
		{
			IndexedSymbol declaration = Symbol(@"C:\mod\scripts\a.ssl", 1, false);
			IndexedSymbol body = Symbol(@"C:\mod\scripts\b.ssl", 1, true);

			IReadOnlyList<IndexedSymbol> ordered = SymbolCloseness.Order(new[] { declaration, body }, @"C:\mod\scripts\c.ssl");

			Assert.Equal(new[] { body, declaration }, ordered.ToArray());
		}

		[Fact]
		public void Order_TiesAreBrokenByPathAndLine_AndAnUnknownCurrentFileIsAccepted()
		{
			IndexedSymbol late = Symbol(@"C:\mod\b.ssl", 9);
			IndexedSymbol early = Symbol(@"C:\mod\b.ssl", 2);
			IndexedSymbol first = Symbol(@"C:\mod\a.ssl", 50);

			IReadOnlyList<IndexedSymbol> ordered = SymbolCloseness.Order(new[] { late, early, first }, null);

			Assert.Equal(new[] { first, early, late }, ordered.ToArray());
		}

		[Fact]
		public void GetDistance_CountsTheStepsBetweenFolders()
		{
			Assert.Equal(0, SymbolCloseness.GetDistance(@"C:\a\b", @"C:\a\b"));
			Assert.Equal(1, SymbolCloseness.GetDistance(@"C:\a\b", @"C:\a"));
			Assert.Equal(2, SymbolCloseness.GetDistance(@"C:\a\b", @"C:\a\c"));
			Assert.Equal(6, SymbolCloseness.GetDistance(@"C:\a\b", @"D:\x\y"));
			Assert.True(SymbolCloseness.GetDistance(null, @"C:\a") > 1000);
		}
	}
}