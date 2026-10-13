namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using Xunit;

	public class SymbolOccurrenceScannerTests
	{
		private static IReadOnlyList<SymbolOccurrence> Scan(String text, String name, Boolean caseSensitive = false, Boolean strings = false)
		{
			return SymbolOccurrenceScanner.Scan(text, "a.ssl", name, caseSensitive, strings);
		}

		[Fact]
		public void Scan_SeparatesDeclarationsFromReferences()
		{
			String text = Ssl.Crlf(
				"procedure helper;",
				"procedure helper begin",
				"end",
				"procedure start begin",
				"\thelper();",
				"\tcall Helper;",
				"end");

			IReadOnlyList<SymbolOccurrence> result = Scan(text, "helper");

			Assert.Equal(
				new[] { SymbolOccurrenceKind.Declaration, SymbolOccurrenceKind.Declaration, SymbolOccurrenceKind.Reference, SymbolOccurrenceKind.Reference },
				result.Select(o => o.Kind).ToArray());
			Assert.Equal(new[] { 1, 2, 5, 6 }, result.Select(o => o.Line).ToArray());
			Assert.Equal(2, result[2].Column);
		}

		[Fact]
		public void Scan_IgnoresCommentsAndStrings()
		{
			String text = Ssl.Crlf("// helper", "/* helper */", "x = \"call helper now\";", "helper();");

			IReadOnlyList<SymbolOccurrence> result = Scan(text, "helper");

			Assert.Equal(4, Assert.Single(result).Line);
		}

		[Fact]
		public void Scan_ReportsAStringThatIsExactlyTheNameOnRequest()
		{
			String text = Ssl.Crlf("x = \"helper\";", "y = \"helper2\";", "#include \"helper\"");

			Assert.Empty(Scan(text, "helper"));
			SymbolOccurrence occurrence = Assert.Single(Scan(text, "helper", false, true));
			Assert.Equal(SymbolOccurrenceKind.StringLiteral, occurrence.Kind);
			Assert.Equal(1, occurrence.Line);
			Assert.Equal(6, occurrence.Column);
			Assert.Equal("helper", text.Substring(occurrence.Offset, occurrence.Length));
		}

		[Fact]
		public void Scan_AMacroNameIsCaseSensitive_AndItsDefinitionIsADeclaration()
		{
			String text = Ssl.Crlf("#define MAX 5", "x = MAX + max;");

			IReadOnlyList<SymbolOccurrence> result = Scan(text, "MAX", true);

			Assert.Equal(new[] { SymbolOccurrenceKind.Declaration, SymbolOccurrenceKind.Reference }, result.Select(o => o.Kind).ToArray());
			Assert.Equal(3, Scan(text, "MAX", false).Count);
		}

		[Fact]
		public void Scan_FindsTheNameInTheBodyOfAMacro()
		{
			String text = Ssl.Crlf("#define RUN helper();", "procedure start begin", "\tRUN", "end");

			SymbolOccurrence occurrence = Assert.Single(Scan(text, "helper"));
			Assert.Equal(1, occurrence.Line);
			Assert.Equal(SymbolOccurrenceKind.Reference, occurrence.Kind);
		}

		[Fact]
		public void Scan_OnlyWholeWordsMatch()
		{
			Assert.Empty(Scan("my_helper(); helper2(); helperx();", "helper"));
		}

		[Fact]
		public void Scan_NoMatchIsCheap()
		{
			Assert.Empty(Scan("procedure start begin end", "missing"));
		}
	}
}
