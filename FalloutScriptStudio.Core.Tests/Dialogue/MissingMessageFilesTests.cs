namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Tests.Functions;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public class MissingMessageFilesTests : IDisposable
	{
		private readonly TempFolder _folder = new TempFolder();

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			this._folder.Dispose();
		}

		private static FlowchartModel CreateModel(params FlowchartContentLine[] lines)
		{
			FlowchartNodeData node = new FlowchartNodeData("Node001", DialogueNodeKind.Default, Array.Empty<FlowchartLink>(), Array.Empty<String>(), lines);
			return new FlowchartModel(new[] { node }, new Dictionary<String, IReadOnlyList<DialogueLine>>());
		}

		private async Task<DialogueMessageSource> CreateSourceAsync(params Int32[] fileNumbers)
		{
			this._folder.Write("data\\scripts\\scripts.lst", "start.int ; 1\r\nother.int ; 2\r\nthird.int ; 3\r\n");
			this._folder.Write("data\\text\\english\\dialog\\dcjohn.msg", "{100}{}{Own hello}\r\n");
			this._folder.Write("data\\text\\english\\dialog\\third.msg", "{300}{}{Third text}\r\n");
			MessageFileOptions options = new MessageFileOptions { OutputDirectory = this._folder.Combine("data\\scripts"), Encoding = TextEncodings.Utf8 };
			DialogueMessageSource source = await DialogueMessageSource.CreateAsync(
				new MessageFileService(NullLogger<MessageFileService>.Instance),
				this._folder.Combine("data\\scripts\\dcjohn.ssl"),
				null,
				options);
			await source.EnsureLoadedAsync(fileNumbers);
			return source;
		}

		[Fact]
		public async Task Find_ListsEachMissingFileOnceWithItsExpectedPath()
		{
			DialogueMessageSource source = await this.CreateSourceAsync(2, 3);
			FlowchartModel model = CreateModel(
				new FlowchartContentLine("Reply(110)", null, DialogueOpcode.Reply, 110, 2, 0),
				new FlowchartContentLine("NOption(111)", null, DialogueOpcode.Option, 111, 2, 1),
				new FlowchartContentLine("Reply(300)", null, DialogueOpcode.Reply, 300, 3, 2));

			IReadOnlyList<String> missing = MissingMessageFiles.Find(model, source);

			String path = Assert.Single(missing);
			Assert.EndsWith("other.msg", path, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task Find_IgnoresTheOwnFileAndLinesWithoutAMessage()
		{
			DialogueMessageSource source = await this.CreateSourceAsync(2);
			FlowchartModel model = CreateModel(
				new FlowchartContentLine("Reply(999)", null, DialogueOpcode.Reply, 999, -1, 0),
				new FlowchartContentLine("call Node002", null, DialogueOpcode.Call, 0, 2, 1),
				new FlowchartContentLine("Reply(0)", null, DialogueOpcode.Reply, 0, 2, 2));

			Assert.Empty(MissingMessageFiles.Find(model, source));
		}
	}
}