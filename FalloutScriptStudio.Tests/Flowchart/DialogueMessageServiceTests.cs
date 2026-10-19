namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using ICSharpCode.AvalonEdit.Document;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public class DialogueMessageServiceTests : IDisposable
	{
		private readonly ScriptToolsTempDirectory _folder = new ScriptToolsTempDirectory();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		private DialogueMessageService CreateService()
		{
			return new DialogueMessageService(new MessageFileService(NullLogger<MessageFileService>.Instance), new FakeSettingsService(this._folder.Path));
		}

		private DocumentViewModel CreateScript(String? path)
		{
			return new DocumentViewModel(path, DocumentKind.Script, new TextDocument());
		}

		[Fact]
		public async Task CreateSource_FindsTheMessageFileNextToTheScript()
		{
			String script = this._folder.Write(Path.Combine("scripts", "dcjohn.ssl"), String.Empty);
			this._folder.Write(Path.Combine("scripts", "dcjohn.msg"), "{100}{}{Hello there.}\r\n{101}{}{Bye.}\r\n");

			IDialogueMessageSource source = await this.CreateService().CreateSourceAsync(this.CreateScript(script));

			Assert.Equal("Hello there.", source.GetMessage(-1, 100).Text);
			Assert.Equal(Path.Combine(Path.GetDirectoryName(script)!, "dcjohn.msg"), source.GetMessageFilePath(-1));
			Assert.Equal(DialogueMessageStatus.MessageNotFound, source.GetMessage(-1, 555).Status);
		}

		[Fact]
		public async Task CreateSource_WithoutAMessageFile_ReportsAMissingFile()
		{
			String script = this._folder.Write(Path.Combine("scripts", "nomsg.ssl"), String.Empty);

			IDialogueMessageSource source = await this.CreateService().CreateSourceAsync(this.CreateScript(script));

			Assert.Equal(DialogueMessageStatus.FileNotFound, source.GetMessage(-1, 100).Status);
			Assert.Null(source.GetMessageFilePath(-1));
		}

		[Fact]
		public async Task CreateSource_ForAScriptThatWasNeverSaved_ReportsAMissingFile()
		{
			IDialogueMessageSource source = await this.CreateService().CreateSourceAsync(this.CreateScript(null));

			Assert.Equal(DialogueMessageStatus.FileNotFound, source.GetMessage(-1, 100).Status);
		}

		[Fact]
		public async Task UpdateMessageText_ChangesOnlyThatMessageInTheFile()
		{
			String file = this._folder.Write(Path.Combine("scripts", "dcjohn.msg"), "{100}{}{Hello there.}\r\n{101}{}{Bye.}\r\n");

			Boolean changed = await this.CreateService().UpdateMessageTextAsync(file, 101, "See you.");

			Assert.True(changed);
			Assert.Equal("{100}{}{Hello there.}\r\n{101}{}{See you.}\r\n", File.ReadAllText(file));
		}

		[Fact]
		public async Task UpdateMessageText_OfAMissingMessageOrFile_ReturnsFalse()
		{
			String file = this._folder.Write(Path.Combine("scripts", "dcjohn.msg"), "{100}{}{Hello there.}\r\n");
			DialogueMessageService service = this.CreateService();

			Assert.False(await service.UpdateMessageTextAsync(file, 555, "x"));
			Assert.False(await service.UpdateMessageTextAsync(this._folder.Combine("scripts", "gone.msg"), 100, "x"));
		}
	}
}