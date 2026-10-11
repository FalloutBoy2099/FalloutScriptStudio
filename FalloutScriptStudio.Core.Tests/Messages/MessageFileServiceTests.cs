namespace FalloutScriptStudio.Core.Tests.Messages
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class MessageFileServiceTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();
		private readonly MessageFileService _service = new MessageFileService(NullLogger<MessageFileService>.Instance);

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private String DataScripts => Path.Combine(this._workspace.Root, "data", "scripts");

		private String DialogFolder => Path.Combine(this._workspace.Root, "data", "text", "english", "dialog");

		private MessageFileOptions CreateOptions()
		{
			return new MessageFileOptions { OutputDirectory = this.DataScripts };
		}

		[Fact]
		public async Task WriteThenRead_RoundTripsEntriesWithCommentsAndMultiLineTexts()
		{
			String path = Path.Combine(this._workspace.Root, "dialog.msg");
			MessageDocument document = new MessageDocument();
			document.Entries.Add(MessageEntry.CreateComment("# Look Name"));
			document.Entries.Add(MessageEntry.CreateMessage(100, "First line\r\nsecond line", "snd01"));
			document.Entries.Add(MessageEntry.CreateBlank());
			document.Entries.Add(new MessageEntry { Kind = MessageLineKind.Message, Id = 101, Text = "PC", IsPlayerLine = true, IndentTabs = 1, Description = " # who" });

			await this._service.WriteAsync(path, document, TextEncodings.Utf8);
			MessageDocument loaded = await this._service.ReadAsync(path, TextEncodings.Utf8);

			Assert.Equal(document.Format(), loaded.Format());
			Assert.Equal("First line\r\nsecond line", loaded.FindMessage(100)!.Text);
			Assert.True(loaded.FindMessage(101)!.IsPlayerLine);
			Assert.EndsWith("\r\n", await File.ReadAllTextAsync(path), StringComparison.Ordinal);
			Assert.Empty(Directory.GetFiles(this._workspace.Root, "*.tmp"));
		}

		[Fact]
		public async Task Oem866_FilesKeepCyrillicAndReplaceTheCyrillicHaByLatinX()
		{
			String path = Path.Combine(this._workspace.Root, "ru.msg");
			MessageDocument document = new MessageDocument();
			document.Entries.Add(MessageEntry.CreateMessage(100, "Привет, мир"));
			document.Entries.Add(MessageEntry.CreateMessage(101, "ХР"));

			await this._service.WriteAsync(path, document, TextEncodings.Oem866);

			Assert.Equal("{100}{}{Привет, мир}\r\n{101}{}{XР}\r\n", TextEncodings.Oem866.GetString(await File.ReadAllBytesAsync(path)));
			MessageDocument loaded = await this._service.ReadAsync(path, TextEncodings.Oem866);
			Assert.Equal("Привет, мир", loaded.FindMessage(100)!.Text);
		}

		[Fact]
		public async Task AnsiFiles_UseTheLegacyCodePage()
		{
			String path = Path.Combine(this._workspace.Root, "en.msg");
			MessageDocument document = new MessageDocument();
			document.Entries.Add(MessageEntry.CreateMessage(100, "café"));

			await this._service.WriteAsync(path, document, TextEncodings.Ansi);

			Byte[] bytes = await File.ReadAllBytesAsync(path);
			Assert.Equal("{100}{}{café}\r\n", TextEncodings.Ansi.GetString(bytes));
			Assert.Equal(1, TextEncodings.Ansi.GetByteCount("é"));
		}

		[Fact]
		public async Task UpdateMessageText_ChangesOnlyThatMessageOnDisk()
		{
			String path = this._workspace.CreateFile("update.msg", "# c\r\n{100}{}{old} # d\r\n{101}{}{keep}\r\n");

			Boolean updated = await this._service.UpdateMessageTextAsync(path, 100, "new", TextEncodings.Utf8);
			Boolean missing = await this._service.UpdateMessageTextAsync(path, 555, "x", TextEncodings.Utf8);

			Assert.True(updated);
			Assert.False(missing);
			Assert.Equal("# c\r\n{100}{}{new} # d\r\n{101}{}{keep}\r\n", await File.ReadAllTextAsync(path));
		}

		[Fact]
		public async Task UpdateMessageText_WithLoadedLinesKeepsThemInSync()
		{
			String path = this._workspace.CreateFile("lines.msg", "{100}{}{old}\r\n");
			List<String> lines = new List<String>(await this._service.ReadLinesAsync(path, TextEncodings.Utf8));

			Boolean updated = await this._service.UpdateMessageTextAsync(path, lines, 100, "changed", TextEncodings.Utf8);

			Assert.True(updated);
			Assert.Equal("{100}{}{changed}", Assert.Single(lines));
			Assert.Equal("{100}{}{changed}\r\n", await File.ReadAllTextAsync(path));
		}

		[Fact]
		public void LocateForScript_FindsTheFileInTheDialogFolderOfTheLanguage()
		{
			String msg = this._workspace.CreateFile(@"data\text\english\dialog\hakunin.msg");
			String script = Path.Combine(this.DataScripts, "hakunin.ssl");

			MessageFileLocation location = this._service.LocateForScript(script, null, this.CreateOptions());

			Assert.True(location.Found);
			Assert.Equal(msg, location.Path);
			Assert.Equal("hakunin.msg", location.FileName);
			Assert.Equal(this.DialogFolder, location.DefaultDirectory);
			Assert.Equal(MessageLookupIssues.None, location.Issues);
		}

		[Fact]
		public void LocateForScript_FallsBackToTheMessageFoldersThenTheScriptFolder()
		{
			String script = this._workspace.CreateFile(@"data\scripts\npc.ssl");
			MessageFileOptions options = this.CreateOptions() with
			{
				SearchDirectories = new[] { Path.Combine(this._workspace.Root, "missing-folder"), Path.Combine(this._workspace.Root, "extra") },
			};

			Assert.False(this._service.LocateForScript(script, null, options).Found);

			String inSubFolder = this._workspace.CreateFile(@"extra\sub\npc.msg");
			MessageFileLocation viaList = this._service.LocateForScript(script, null, options);
			Assert.True(viaList.Found);
			Assert.Equal(inSubFolder, viaList.Path);

			File.Delete(inSubFolder);
			String besideScript = this._workspace.CreateFile(@"data\scripts\npc.msg");
			MessageFileLocation nextToScript = this._service.LocateForScript(script, null, options);
			Assert.True(nextToScript.Found);
			Assert.Equal(besideScript, nextToScript.Path);
		}

		[Fact]
		public void LocateForScript_WithoutOutputFolderUsesTheScriptFolder()
		{
			String script = this._workspace.CreateFile(@"proj\a.ssl");
			String msg = this._workspace.CreateFile(@"proj\a.msg");

			MessageFileLocation location = this._service.LocateForScript(script, null, new MessageFileOptions());

			Assert.True(location.Found);
			Assert.Equal(msg, location.Path);
			Assert.Equal(Path.Combine(this._workspace.Root, "proj"), location.DefaultDirectory);
		}

		[Fact]
		public void LocateForScript_AssociatesByTheScriptNumberInScriptsLst()
		{
			this._workspace.CreateFile(@"data\scripts\scripts.lst", "alpha.int ; Alpha\r\n  beta.int ; Beta\r\n");
			String msg = this._workspace.CreateFile(@"data\text\english\dialog\beta.msg");
			MessageFileOptions options = this.CreateOptions() with { AssociateByScriptId = true };

			MessageFileLocation byId = this._service.LocateForScript(Path.Combine(this.DataScripts, "other.ssl"), 2, options);

			Assert.True(byId.Found);
			Assert.Equal(msg, byId.Path);
			Assert.Equal("beta.msg", byId.FileName);
		}

		[Fact]
		public void LocateForScript_ReportsScriptsLstProblems()
		{
			MessageFileOptions options = this.CreateOptions() with { AssociateByScriptId = true };
			String script = Path.Combine(this.DataScripts, "fallback.ssl");

			MessageFileLocation noList = this._service.LocateForScript(script, 1, options);
			Assert.Equal("fallback.msg", noList.FileName);
			Assert.True((noList.Issues & MessageLookupIssues.ScriptsListMissing) != 0);

			this._workspace.CreateFile(@"data\scripts\scripts.lst", "alpha.int\r\n");
			MessageFileLocation badIndex = this._service.LocateForScript(script, 9, options);
			Assert.False(badIndex.Found);
			Assert.Null(badIndex.FileName);
			Assert.True((badIndex.Issues & MessageLookupIssues.ScriptsListEntryInvalid) != 0);

			MessageFileLocation noName = this._service.LocateForScript(script, -1, options);
			Assert.Equal("fallback.msg", noName.FileName);
			Assert.True((noName.Issues & MessageLookupIssues.ScriptIdNotFound) != 0);
		}

		[Fact]
		public void LocateById_UsesScriptsLstOfTheOutputFolder()
		{
			this._workspace.CreateFile(@"data\scripts\scripts.lst", "alpha.int\r\nbeta.int\r\n");
			String msg = this._workspace.CreateFile(@"data\text\english\dialog\alpha.msg");

			MessageFileLocation location = this._service.LocateById(Path.Combine(this.DataScripts, "x.ssl"), 1, this.CreateOptions());

			Assert.True(location.Found);
			Assert.Equal(msg, location.Path);
		}

		[Fact]
		public void LocateById_WithoutOutputFolderFlagsItAndUsesTheScriptFolder()
		{
			this._workspace.CreateFile(@"proj\scripts.lst", "alpha.int\r\n");
			String script = Path.Combine(this._workspace.Root, "proj", "x.ssl");

			MessageFileLocation location = this._service.LocateById(script, 1, new MessageFileOptions());

			Assert.False(location.Found);
			Assert.Equal("alpha.msg", location.FileName);
			Assert.True((location.Issues & MessageLookupIssues.OutputDirectoryNotSet) != 0);
		}

		[Fact]
		public async Task CreateFile_WritesTheDefaultContentAtTheDefaultLocation()
		{
			String script = Path.Combine(this.DataScripts, "newnpc.ssl");
			MessageFileLocation location = this._service.LocateForScript(script, null, this.CreateOptions());
			Assert.False(location.Found);

			String created = await this._service.CreateFileAsync(location);

			Assert.Equal(Path.Combine(this.DialogFolder, "newnpc.msg"), created);
			Assert.Equal("{100}{}{}", await File.ReadAllTextAsync(created));
			Assert.True(this._service.LocateForScript(script, null, this.CreateOptions()).Found);
		}

		[Fact]
		public async Task CreateFile_DoesNotOverwriteAFileThatAppearedMeanwhile()
		{
			String script = Path.Combine(this.DataScripts, "racy.ssl");
			MessageFileLocation location = this._service.LocateForScript(script, null, this.CreateOptions());
			Assert.False(location.Found);
			Directory.CreateDirectory(this.DialogFolder);
			String existing = Path.Combine(this.DialogFolder, "racy.msg");
			await File.WriteAllTextAsync(existing, "{100}{}{Precious}");

			String created = await this._service.CreateFileAsync(location);

			Assert.Equal(existing, created);
			Assert.Equal("{100}{}{Precious}", await File.ReadAllTextAsync(existing));
		}

		[Fact]
		public async Task CreateFile_RequiresAKnownLocation()
		{
			MessageFileLocation unknown = new MessageFileLocation(false, null, null, null, MessageLookupIssues.ScriptsListEntryInvalid);

			await Assert.ThrowsAsync<InvalidOperationException>(() => this._service.CreateFileAsync(unknown));
		}

		[Fact]
		public void Options_DialogDirectoryFollowsTheLanguage()
		{
			MessageFileOptions options = new MessageFileOptions { OutputDirectory = this.DataScripts, Language = "russian" };

			Assert.Equal(Path.Combine(this._workspace.Root, "data", "text", "russian", "dialog"), options.DialogDirectory);
			Assert.Null(new MessageFileOptions().DialogDirectory);
		}
	}
}
