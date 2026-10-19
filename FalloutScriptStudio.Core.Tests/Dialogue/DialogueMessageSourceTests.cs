namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public class DialogueMessageSourceTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();
		private readonly MessageFileService _service = new MessageFileService(NullLogger<MessageFileService>.Instance);

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		private MessageFileOptions CreateOptions()
		{
			return new MessageFileOptions { OutputDirectory = this._folder.Combine("data\\scripts"), Encoding = TextEncodings.Utf8 };
		}

		private String ScriptPath => this._folder.Combine("data\\scripts\\dcjohn.ssl");

		private void CreateFiles()
		{
			this._folder.Write("data\\scripts\\scripts.lst", "start.int ; 1\r\nother.int ; 2\r\n");
			this._folder.Write("data\\text\\english\\dialog\\dcjohn.msg", "{100}{}{Own hello}\r\n{101}{}{Own help}\r\n");
			this._folder.Write("data\\text\\english\\dialog\\other.msg", "{110}{}{Other text}\r\n");
		}

		[Fact]
		public async Task CreateAsync_LoadsTheOwnMessageFile()
		{
			this.CreateFiles();

			DialogueMessageSource source = await DialogueMessageSource.CreateAsync(this._service, this.ScriptPath, null, this.CreateOptions());

			Assert.True(source.HasOwnFile);
			DialogueMessageLookup found = source.GetMessage(-1, 100);
			Assert.Equal(DialogueMessageStatus.Found, found.Status);
			Assert.Equal("Own hello", found.Text);
			Assert.EndsWith("dcjohn.msg", found.FilePath);
			Assert.Equal(found.FilePath, source.GetMessageFilePath(-1));
		}

		[Fact]
		public async Task EnsureLoadedAsync_LoadsFilesByNumberThroughScriptsLst()
		{
			this.CreateFiles();
			DialogueMessageSource source = await DialogueMessageSource.CreateAsync(this._service, this.ScriptPath, null, this.CreateOptions());

			Assert.Equal(DialogueMessageStatus.FileNotFound, source.GetMessage(2, 110).Status);
			await source.EnsureLoadedAsync(new[] { 2, 2, -1 });

			DialogueMessageLookup other = source.GetMessage(2, 110);
			Assert.Equal((DialogueMessageStatus.Found, "Other text"), (other.Status, other.Text));
			Assert.EndsWith("other.msg", other.FilePath);
		}

		[Fact]
		public async Task Lookup_ReportsMissingMessagesFilesAndNumbers()
		{
			this.CreateFiles();
			DialogueMessageSource source = await DialogueMessageSource.CreateAsync(this._service, this.ScriptPath, null, this.CreateOptions());
			await source.EnsureLoadedAsync(new[] { 9 });

			DialogueMessageLookup noMessage = source.GetMessage(-1, 555);
			DialogueMessageLookup noFile = source.GetMessage(9, 1);
			DialogueMessageLookup noNumber = source.GetMessage(-1, 0);

			Assert.Equal((DialogueMessageStatus.MessageNotFound, MessageFileTexts.TextNotFound), (noMessage.Status, noMessage.Text));
			Assert.True(noMessage.IsError);
			Assert.Equal(DialogueMessageStatus.FileNotFound, noFile.Status);
			Assert.Equal(String.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFileTexts.FileNotFoundFormat, 9), noFile.Text);
			Assert.True(noFile.IsError);
			Assert.Equal(DialogueMessageStatus.NoMessageNumber, noNumber.Status);
			Assert.Null(noNumber.Text);
			Assert.False(noNumber.IsError);
		}

		[Fact]
		public async Task CreateAsync_WithoutAMessageFile_ReportsFileNotFound()
		{
			this._folder.Write("data\\scripts\\scripts.lst", "start.int\r\n");

			DialogueMessageSource source = await DialogueMessageSource.CreateAsync(this._service, this.ScriptPath, null, this.CreateOptions());

			Assert.False(source.HasOwnFile);
			Assert.Equal(DialogueMessageStatus.FileNotFound, source.GetMessage(-1, 100).Status);
		}

		[Fact]
		public void RegisteredDocuments_AreUsedWithoutAService()
		{
			var source = new DialogueMessageSource(DialogueSamples.OwnMessages(), "own.msg");
			source.SetFile(123, DialogueSamples.OtherMessages(), "other.msg");

			Assert.Equal("Hello there, stranger.", source.GetMessage(-1, 100).Text);
			Assert.Equal("Reply from gsay_reply.", source.GetMessage(123, 112).Text);
			Assert.Equal("other.msg", source.GetMessageFilePath(123));
			Assert.True(source.EnsureLoadedAsync(new[] { 5 }).IsCompletedSuccessfully);
			Assert.Equal(DialogueMessageStatus.FileNotFound, source.GetMessage(5, 1).Status);
		}
	}
}