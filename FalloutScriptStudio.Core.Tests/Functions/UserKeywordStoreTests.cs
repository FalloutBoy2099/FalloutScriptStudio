namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Functions;
	using Xunit;

	public class UserKeywordStoreTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		[Fact]
		public async Task AddAsync_PersistsAndReloads()
		{
			String path = this._folder.Combine("settings\\keywords.json");
			var store = new UserKeywordStore(path);
			Int32 changed = 0;
			store.Changed += (_, _) => changed++;

			Assert.True(await store.AddAsync("DEBUGMSG"));
			Assert.True(await store.AddAsync("  MyMacro "));

			Assert.Equal(new[] { "DEBUGMSG", "MyMacro" }, store.Keywords.ToArray());
			Assert.Equal(2, changed);
			var reloaded = new UserKeywordStore(path);
			await reloaded.LoadAsync();
			Assert.Equal(new[] { "DEBUGMSG", "MyMacro" }, reloaded.Keywords.ToArray());
			Assert.Contains("\"keywords\"", File.ReadAllText(path));
		}

		[Fact]
		public async Task AddAsync_RejectsDuplicatesAndInvalidWords()
		{
			var store = new UserKeywordStore(this._folder.Combine("k.json"));

			Assert.True(await store.AddAsync("ABC"));
			Assert.False(await store.AddAsync("ABC"));
			Assert.True(await store.AddAsync("abc"));
			Assert.False(await store.AddAsync(String.Empty));
			Assert.False(await store.AddAsync("two words"));
			Assert.False(await store.AddAsync("a-b"));

			Assert.Equal(new[] { "ABC", "abc" }, store.Keywords.ToArray());
		}

		[Fact]
		public async Task RemoveAsync_RemovesAndPersists()
		{
			String path = this._folder.Combine("k.json");
			var store = new UserKeywordStore(path);
			await store.AddAsync("A");
			await store.AddAsync("B");

			Assert.True(await store.RemoveAsync("A"));
			Assert.False(await store.RemoveAsync("A"));

			var reloaded = new UserKeywordStore(path);
			await reloaded.LoadAsync();
			Assert.Equal(new[] { "B" }, reloaded.Keywords.ToArray());
		}

		[Fact]
		public async Task LoadAsync_MissingOrDamagedFile_GivesAnEmptyList()
		{
			String path = this._folder.Write("bad.json", "{ not json");
			var damaged = new UserKeywordStore(path);
			var missing = new UserKeywordStore(this._folder.Combine("missing.json"));

			await damaged.LoadAsync();
			await missing.LoadAsync();

			Assert.Empty(damaged.Keywords);
			Assert.Empty(missing.Keywords);
		}

		[Fact]
		public async Task LoadAsync_FiltersInvalidEntriesOfAHandEditedFile()
		{
			String path = this._folder.Write("hand.json", "{ \"keywords\": [ \"OK\", \"\", \"bad word\", \"OK\", null, \"Fine_2\" ] }");
			var store = new UserKeywordStore(path);

			await store.LoadAsync();

			Assert.Equal(new[] { "OK", "Fine_2" }, store.Keywords.ToArray());
		}

		[Fact]
		public void ParseLegacyXml_ReadsOnlyTheUserMacrosSection()
		{
			String xml = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>\r\n"
				+ "<SyntaxDefinition name = \"UserWords\">\r\n"
				+ "  <KeyWords name = \"SpecMacros\" bold=\"true\"><Key word = \"DEBUGMSG\" /></KeyWords>\r\n"
				+ "  <KeyWords name = \"UserMacros\" bold=\"true\" color=\"Green\">\r\n"
				+ "    <Key word = \"\" /><Key word = \"MyMacro\" /><Key word = \"Other\" /><Key word = \"MyMacro\" />\r\n"
				+ "  </KeyWords>\r\n"
				+ "</SyntaxDefinition>";

			Assert.Equal(new[] { "MyMacro", "Other" }, UserKeywordStore.ParseLegacyXml(xml).ToArray());
		}

		[Fact]
		public async Task ImportLegacyXmlAsync_MergesTheKeywords()
		{
			String legacy = this._folder.Write("User_SyntaxRules.xml", "<SyntaxDefinition><KeyWords name=\"UserMacros\"><Key word=\"A\"/><Key word=\"B\"/></KeyWords></SyntaxDefinition>");
			var store = new UserKeywordStore(this._folder.Combine("k.json"));
			await store.AddAsync("B");

			Int32 added = await store.ImportLegacyXmlAsync(legacy);

			Assert.Equal(1, added);
			Assert.Equal(new[] { "B", "A" }, store.Keywords.ToArray());
			Assert.Equal(0, await store.ImportLegacyXmlAsync(this._folder.Combine("missing.xml")));
			Assert.Equal(0, await store.ImportLegacyXmlAsync(this._folder.Write("broken.xml", "<oops")));
		}
	}
}