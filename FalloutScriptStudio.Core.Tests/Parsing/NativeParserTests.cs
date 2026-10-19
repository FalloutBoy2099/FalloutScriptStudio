namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using Xunit;

	public class NativeParserTests
	{
		[Fact]
		public async Task ParseAsync_PathNotConfigured_IsUnavailable()
		{
			using var parser = new NativeParser();

			NativeParseResult result = await parser.ParseAsync("procedure a begin\r\nend", @"C:\s\a.ssl", new ParserOptions(), CancellationToken.None);

			Assert.Equal(NativeParseStatus.Unavailable, result.Status);
			Assert.False(result.Succeeded);
			Assert.NotNull(result.Message);
		}

		[Fact]
		public async Task ParseAsync_MissingLibrary_IsUnavailable()
		{
			using var dir = new TempDirectory();
			using var parser = new NativeParser();
			var options = new ParserOptions { NativeParserPath = Path.Combine(dir.Path, "resources", "parser.dll") };

			NativeParseResult result = await parser.ParseAsync("x", @"C:\s\a.ssl", options, CancellationToken.None);

			Assert.Equal(NativeParseStatus.Unavailable, result.Status);
			Assert.Contains("not found", result.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_LibraryThatCannotBeLoaded_IsUnavailableAndDoesNotThrow()
		{
			using var dir = new TempDirectory();
			String fake = dir.Write("parser.dll", "this is not a portable executable");
			using var parser = new NativeParser();
			var options = new ParserOptions { NativeParserPath = fake, ScratchDirectory = dir.Path };

			NativeParseResult first = await parser.ParseAsync("x", @"C:\s\a.ssl", options, CancellationToken.None);
			NativeParseResult second = await parser.ParseAsync("x", @"C:\s\a.ssl", options, CancellationToken.None);

			Assert.Equal(NativeParseStatus.Unavailable, first.Status);
			Assert.Equal(NativeParseStatus.Unavailable, second.Status);
			Assert.Equal(first.Message, second.Message);
			Assert.Contains("could not be loaded", first.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_CancelledToken_Throws()
		{
			using var parser = new NativeParser();
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => parser.ParseAsync("x", @"C:\s\a.ssl", new ParserOptions(), cts.Token));
		}

		[Fact]
		public void ParseName_ReadsLengthPrefixedNames()
		{
			Byte[] table = { 3, 0, (Byte)'a', (Byte)'b', (Byte)'c' };

			Assert.Equal("abc", NativeDataReader.ParseName(table, 6));
			Assert.Throws<InvalidDataException>(() => NativeDataReader.ParseName(table, 2));
			Assert.Throws<InvalidDataException>(() => NativeDataReader.ParseName(new Byte[] { 200, 0, 1, 2, 3 }, 6));
		}

		[Fact]
		public void FindArgumentDeclarationLine_LocatesTheArgumentInTheProcedureHeader()
		{
			String[] code =
			{
				"procedure foo",
				"  (variable who,",
				"   variable amount) begin",
				"end",
			};

			Assert.Equal(2, NativeDataReader.FindArgumentDeclarationLine(3, 0, "who", code));
			Assert.Equal(3, NativeDataReader.FindArgumentDeclarationLine(3, 0, "amount", code));
			Assert.Equal(-1, NativeDataReader.FindArgumentDeclarationLine(3, 0, "wh", code));
			Assert.Equal(-1, NativeDataReader.FindArgumentDeclarationLine(100, 0, "who", code));
		}
	}
}