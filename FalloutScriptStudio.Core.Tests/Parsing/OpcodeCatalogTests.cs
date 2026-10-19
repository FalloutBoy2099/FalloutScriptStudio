namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class OpcodeCatalogTests
	{
		private static readonly String[] _sample =
		{
			"// VANILLA PROCEDURE HANDLERS:",
			"",
			"Handler look_at_p_proc | - Called when the object is looked at. | Second line. || Note: be careful.",
			"int random (int min, int max) | - Returns a random number. | Inclusive range.",
			"void obj_open (ObjectPtr obj) | - Opens the object.",
			"int* get_array (int id) | - Returns an array.",
		};

		[Fact]
		public void Constructor_ParsesOpcodeNamesAndFormatsDescriptions()
		{
			var catalog = new OpcodeCatalog(_sample);

			Assert.Equal(4, catalog.Count);
			Assert.Equal(
				"Handler look_at_p_proc\n - Called when the object is looked at.\n Second line.\n\n Note: be careful.",
				catalog.LookupToken("look_at_p_proc"));
		}

		[Fact]
		public void LookupToken_IsCaseInsensitiveAndReturnsNullForUnknownTokens()
		{
			var catalog = new OpcodeCatalog(_sample);

			Assert.NotNull(catalog.LookupToken("RANDOM"));
			Assert.Null(catalog.LookupToken("not_an_opcode"));
		}

		[Fact]
		public void ShortDescription_KeepsOnlyTheFirstDescriptionLine()
		{
			var catalog = new OpcodeCatalog(_sample, true);

			Assert.Equal("Handler look_at_p_proc\n - Called when the object is looked at.", catalog.LookupToken("look_at_p_proc"));
		}

		[Fact]
		public void LongDescriptions_AreWrappedAtTheConfiguredWidth()
		{
			var catalog = new OpcodeCatalog(new[] { "int f (int a) | - one two three four five six seven eight nine ten" }, false, 20);

			String? description = catalog.LookupToken("f");
			Assert.NotNull(description);

			Assert.All(description.Split('\n'), line => Assert.True(line.Length <= 22, line));
			Assert.Contains("\n", description, StringComparison.Ordinal);
		}

		[Fact]
		public void Lookup_ReturnsPrefixMatchesOrderedByName()
		{
			var catalog = new OpcodeCatalog(_sample);

			IReadOnlyList<AutosuggestItem> matches = catalog.Lookup("O");

			Assert.Equal(new[] { "obj_open" }, matches.Select(m => m.Name));
			Assert.All(matches, m => Assert.Equal(AutosuggestKind.Opcode, m.Kind));
			Assert.Equal(4, catalog.Lookup(String.Empty).Count);
		}

		[Fact]
		public void LoadFromFile_ReadsUtf8AndToleratesMissingFile()
		{
			using var dir = new TempDirectory();
			String path = dir.Write("opcodes.txt", String.Join("\r\n", _sample) + "\r\nvoid msg (int x) | - Привет");

			OpcodeCatalog loaded = OpcodeCatalog.LoadFromFile(path);
			OpcodeCatalog missing = OpcodeCatalog.LoadFromFile(Path.Combine(dir.Path, "none.txt"));

			Assert.Equal(5, loaded.Count);
			Assert.Contains("Привет", loaded.LookupToken("msg"), StringComparison.Ordinal);
			Assert.Equal(0, missing.Count);
		}
	}
}