namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Xunit;

	public class NodeCodeSnippetStoreTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		[Fact]
		public async Task Changed_IsRaisedOnTheCallersContext()
		{
			using PumpSynchronizationContext pump = new PumpSynchronizationContext();
			using NodeCodeSnippetStore store = new NodeCodeSnippetStore(this._folder.Combine("CustomCode.ini"));
			List<SynchronizationContext?> seen = [];
			store.Changed += (_, _) => seen.Add(SynchronizationContext.Current);

			Boolean added = await pump.RunAsync(() => store.AddAsync("greeting", "Reply(100);"));
			Boolean removed = await pump.RunAsync(() => store.RemoveAsync("greeting"));

			Assert.True(added);
			Assert.True(removed);
			Assert.Equal(2, seen.Count);
			Assert.All(seen, context => Assert.Same(pump, context));
		}

		[Fact]
		public void Parse_ReadsTheLegacyFormat()
		{
			var snippets = NodeCodeSnippetStore.Parse(new[] { "no bar before any snippet", "Greeting|Reply(100);", "Two|a|b", "|empty name", "Multi|a;<cr>b;" });

			Assert.Equal(new[] { "Greeting", "Two", "Multi" }, snippets.Select(s => s.Name).ToArray());
			Assert.Equal("a|b", snippets[1].Code);
			Assert.Equal("a;" + Environment.NewLine + "b;", snippets[2].Code);
		}

		[Fact]
		public async Task AddAsync_PersistsMultiLineCode_AndReplacesSameName()
		{
			String path = this._folder.Combine("settings\\CustomCode.ini");
			var store = new NodeCodeSnippetStore(path);
			Int32 changed = 0;
			store.Changed += (_, _) => changed++;

			Assert.True(await store.AddAsync("Say", "Reply(1);\r\nNOption(2, Node001, 4);"));
			Assert.True(await store.AddAsync("Bye", "NMessage(3);"));
			Assert.True(await store.AddAsync("Say", "Reply(9);"));
			Assert.False(await store.AddAsync("  ", "code"));
			Assert.False(await store.AddAsync("name", String.Empty));

			Assert.Equal(new[] { "Bye", "Say" }, store.Snippets.Select(s => s.Name).ToArray());
			Assert.Equal(3, changed);
			Assert.Equal("Bye|NMessage(3);\r\nSay|Reply(9);\r\n", File.ReadAllText(path));
			var reloaded = new NodeCodeSnippetStore(path);
			await reloaded.LoadAsync();
			Assert.Equal(store.Snippets, reloaded.Snippets);
		}

		[Fact]
		public async Task AddAsync_SanitizesTheName()
		{
			var store = new NodeCodeSnippetStore(this._folder.Combine("c.ini"));

			await store.AddAsync("a|b\r\nc", "x");

			Assert.Equal("a/b c", store.Snippets.Single().Name);
		}

		[Fact]
		public async Task RemoveAsync_RemovesAndPersists()
		{
			String path = this._folder.Combine("c.ini");
			var store = new NodeCodeSnippetStore(path);
			await store.AddAsync("A", "1");
			await store.AddAsync("B", "2");

			Assert.True(await store.RemoveAsync("A"));
			Assert.False(await store.RemoveAsync("A"));

			Assert.Equal("B|2\r\n", File.ReadAllText(path));
		}

		[Fact]
		public async Task LoadAsync_MissingFile_IsEmpty()
		{
			var store = new NodeCodeSnippetStore(this._folder.Combine("nothing.ini"));

			await store.LoadAsync();

			Assert.Empty(store.Snippets);
		}

		[Fact]
		public async Task AddAsync_WithoutPriorLoad_KeepsTheSnippetsAlreadyInTheFile()
		{
			String path = this._folder.Combine("settings\\CustomCode.ini");
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllText(path, "Old|Reply(1);\r\nOther|NMessage(2);\r\n");
			var store = new NodeCodeSnippetStore(path);

			Assert.True(await store.AddAsync("New", "Reply(3);"));

			Assert.Equal(new[] { "Old", "Other", "New" }, store.Snippets.Select(s => s.Name).ToArray());
			Assert.Equal("Old|Reply(1);\r\nOther|NMessage(2);\r\nNew|Reply(3);\r\n", File.ReadAllText(path));
		}

		[Fact]
		public async Task RemoveAsync_WithoutPriorLoad_KeepsTheOtherSnippets()
		{
			String path = this._folder.Combine("c.ini");
			File.WriteAllText(path, "A|1\r\nB|2\r\n");
			var store = new NodeCodeSnippetStore(path);

			Assert.True(await store.RemoveAsync("A"));

			Assert.Equal("B|2\r\n", File.ReadAllText(path));
		}

		[Fact]
		public async Task AddAsync_UnreadableFile_IsNotOverwritten()
		{
			String path = this._folder.Combine("c.ini");
			File.WriteAllText(path, "Old|1\r\n");
			var store = new NodeCodeSnippetStore(path);

			using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
			{
				Assert.False(await store.AddAsync("New", "2"));
			}

			Assert.Equal("Old|1\r\n", File.ReadAllText(path));
		}

		[Fact]
		public void Parse_IgnoresBlankLinesAfterTheLastSnippet_ButKeepsThoseInsideACode()
		{
			var snippets = NodeCodeSnippetStore.Parse(new[] { "A|first;", "", "second;", "", "", "B|x", "", "  " });

			Assert.Equal("first;" + Environment.NewLine + Environment.NewLine + "second;", snippets[0].Code);
			Assert.Equal("x", snippets[1].Code);
		}

		[Fact]
		public void Parse_JoinsTheRawLinesOfAMultiLineLegacySnippet()
		{
			var snippets = NodeCodeSnippetStore.Parse(new[] { "Multi|Reply(1);", "NOption(2);", "Next|x" });

			Assert.Equal(new[] { "Multi", "Next" }, snippets.Select(s => s.Name).ToArray());
			Assert.Equal("Reply(1);" + Environment.NewLine + "NOption(2);", snippets[0].Code);
		}
	}
}