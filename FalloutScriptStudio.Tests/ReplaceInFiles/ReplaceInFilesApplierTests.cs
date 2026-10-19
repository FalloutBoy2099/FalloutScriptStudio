namespace FalloutScriptStudio.Tests.ReplaceInFiles
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>The second phase of Replace in Files: files rewritten in their own encoding, documents edited, nothing trusted from the preview.</summary>
	public sealed class ReplaceInFilesApplierTests
	{
		private static readonly Encoding Utf8WithBom = new UTF8Encoding(true);

		private static ReplacePlan Plan(String folder, String find, String replacement, Boolean regex = false, Boolean wholeWord = false)
		{
			return new ReplaceInFilesPlanner().Plan(ReplaceInFilesPlannerTests.FolderRequest(folder, find, replacement, true, wholeWord, regex), null, CancellationToken.None);
		}

		private static List<ReplaceSelection> All(ReplacePlan plan)
		{
			return plan.Files.Select(file => new ReplaceSelection(file, file.Matches)).ToList();
		}

		private static ReplaceInFilesApplier Applier(IDocumentFileWriter? writer = null)
		{
			return new ReplaceInFilesApplier(new FakeWorkspace().Interface, writer);
		}

		private static void WriteBytes(String path, Encoding encoding, String text)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
		}

		private static Byte[] Expected(Encoding encoding, String text)
		{
			return encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
		}

		[Theory]
		[InlineData("ansi")]
		[InlineData("utf8")]
		[InlineData("utf8bom")]
		[InlineData("utf16")]
		public async Task AFileIsRewrittenInItsOwnEncoding_WithItsByteOrderMark(String kind)
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			Encoding encoding = kind switch
			{
				"ansi" => TextEncodings.Ansi,
				"utf8" => new UTF8Encoding(false),
				"utf8bom" => Utf8WithBom,
				_ => Encoding.Unicode,
			};
			String path = folder.Combine("a.ssl");
			WriteBytes(path, encoding, "// café à la carte\r\nvariable old = old;\r\n");
			ReplacePlan plan = Plan(folder.Path, "old", "newer");

			ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal(Expected(encoding, "// café à la carte\r\nvariable newer = newer;\r\n"), File.ReadAllBytes(path));
			Assert.Equal((2, 1, false), (result.Replacements, result.FilesChanged, result.Cancelled));
			Assert.Empty(result.Failures);
			Assert.NotNull(result.Undo);
		}

		[Fact]
		public async Task TheLineBreaksAreKeptByteForByte_MixedOnesToo()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Combine("mixed.ssl");
			String original = "a old\nb old\r\nc old\rd old";
			WriteBytes(path, new UTF8Encoding(false), original);
			ReplacePlan plan = Plan(folder.Path, "old", "XY");

			await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal("a XY\nb XY\r\nc XY\rd XY", File.ReadAllText(path));
		}

		[Fact]
		public async Task OnlyTheTickedReplacementsAreMade()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old 1\r\nold 2\r\nold 3\r\n");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			ReplaceFilePlan file = plan.Files.Single();

			ReplaceResult result = await Applier().ApplyAsync(plan, new[] { new ReplaceSelection(file, new[] { file.Matches[0], file.Matches[2] }) }, null, CancellationToken.None);

			Assert.Equal("new 1\r\nold 2\r\nnew 3\r\n", File.ReadAllText(path));
			Assert.Equal(2, result.Replacements);
		}

		[Fact]
		public async Task ASelectionWithoutMatchesTouchesNothing()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old");
			DateTime written = File.GetLastWriteTimeUtc(path);
			ReplacePlan plan = Plan(folder.Path, "old", "new");

			ReplaceResult result = await Applier().ApplyAsync(plan, new[] { new ReplaceSelection(plan.Files.Single(), Array.Empty<ReplaceMatch>()) }, null, CancellationToken.None);

			Assert.Equal(0, result.FilesChanged);
			Assert.Null(result.Undo);
			Assert.Equal("old", File.ReadAllText(path));
			Assert.Equal(written, File.GetLastWriteTimeUtc(path));
		}

		[Fact]
		public async Task ARegexReplacementWithGroupsIsApplied()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "set(speed, 10);\r\nset(power, 20);\r\n");
			ReplacePlan plan = Plan(folder.Path, @"set\((\w+), (\d+)\)", "put($2 into $1)", regex: true);

			await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal("put(10 into speed);\r\nput(20 into power);\r\n", File.ReadAllText(path));
		}

		[Fact]
		public async Task ThousandsOfReplacementsOfDifferentLengthsKeepTheirPlaces()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			StringBuilder original = new StringBuilder();
			StringBuilder expected = new StringBuilder();
			for (Int32 i = 0; i < 3000; i++)
			{
				original.Append("x old y old\r\n");
				expected.Append("x a_much_longer_name y a_much_longer_name\r\n");
			}

			String path = folder.Write("big.ssl", original.ToString());
			ReplacePlan plan = Plan(folder.Path, "old", "a_much_longer_name");

			ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal(expected.ToString(), File.ReadAllText(path));
			Assert.Equal(6000, result.Replacements);
		}

		[Fact]
		public async Task AFileThatChangedSinceThePreviewIsLeftAlone_TheOtherFilesAreStillReplaced()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String changed = folder.Write("a.ssl", "old");
			String other = folder.Write("b.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			File.WriteAllText(changed, "// edited meanwhile\r\nold");

			ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal("// edited meanwhile\r\nold", File.ReadAllText(changed));
			Assert.Equal("new", File.ReadAllText(other));
			ReplaceFailure failure = Assert.Single(result.Failures);
			Assert.Equal((changed, ReplaceFailureReason.Changed, 1), (failure.Path, failure.Reason, failure.Matches));
			Assert.Equal(1, result.FilesChanged);
		}

		[Fact]
		public async Task AFileThatWasDeletedSinceThePreviewIsReported()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String gone = folder.Write("a.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			File.Delete(gone);

			ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal(ReplaceFailureReason.Unreadable, Assert.Single(result.Failures).Reason);
			Assert.False(File.Exists(gone));
		}

		[Fact]
		public async Task AFileThatBecameReadOnlyIsLeftAloneAndReported()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			File.SetAttributes(path, FileAttributes.ReadOnly);
			try
			{
				ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

				Assert.Equal(ReplaceFailureReason.ReadOnly, Assert.Single(result.Failures).Reason);
				Assert.Equal("old", File.ReadAllText(path));
				Assert.True((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0);
			}
			finally
			{
				File.SetAttributes(path, FileAttributes.Normal);
			}
		}

		[Fact]
		public async Task ALockedFileIsLeftAloneAndReported_NoTemporaryFileStaysBehind()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");

			ReplaceResult result;
			using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);
			}

			Assert.Equal(ReplaceFailureReason.Unreadable, Assert.Single(result.Failures).Reason);
			Assert.Equal("old", File.ReadAllText(path));
			Assert.Empty(Directory.EnumerateFiles(folder.Path, "*.tmp"));
		}

		[Fact]
		public async Task AWriteThatFailsMidWay_NeverLeavesAHalfWrittenFile()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old and more text");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			ReplaceResult result;

			// The real atomic writer cannot replace a file that another program holds open for reading: the temporary file is removed.
			using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);
			}

			ReplaceFailure failure = Assert.Single(result.Failures);
			Assert.Equal(ReplaceFailureReason.WriteFailed, failure.Reason);
			Assert.Equal("old and more text", File.ReadAllText(path));
			Assert.Empty(Directory.EnumerateFiles(folder.Path, "*.tmp"));
		}

		[Fact]
		public async Task AWriterThatThrows_LeavesTheFileAsItWas_AndTheOtherFilesAreStillDone()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String first = folder.Write("a.ssl", "old");
			String second = folder.Write("b.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			CallbackDocumentFileWriter writer = new CallbackDocumentFileWriter(async (path, content) =>
			{
				if (path.EndsWith("a.ssl", StringComparison.Ordinal))
				{
					throw new IOException("disk full");
				}

				await File.WriteAllBytesAsync(path, content);
			});

			ReplaceResult result = await Applier(writer).ApplyAsync(plan, All(plan), null, CancellationToken.None);

			ReplaceFailure failure = Assert.Single(result.Failures);
			Assert.Equal((first, ReplaceFailureReason.WriteFailed, "disk full"), (failure.Path, failure.Reason, failure.Detail));
			Assert.Equal("old", File.ReadAllText(first));
			Assert.Equal("new", File.ReadAllText(second));
		}

		[Fact]
		public async Task AReplacementTheEncodingCannotHold_LeavesTheFileUntouched_ButUtf8TakesIt()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String ansi = folder.Combine("ansi.ssl");
			String utf8 = folder.Combine("utf8.ssl");
			WriteBytes(ansi, TextEncodings.Ansi, "old café");
			WriteBytes(utf8, new UTF8Encoding(false), "old café");
			ReplacePlan plan = Plan(folder.Path, "old", "ЖЖ");

			ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal(Expected(TextEncodings.Ansi, "old café"), File.ReadAllBytes(ansi));
			Assert.Equal(Expected(new UTF8Encoding(false), "ЖЖ café"), File.ReadAllBytes(utf8));
			ReplaceFailure failure = Assert.Single(result.Failures);
			Assert.Equal((ansi, ReplaceFailureReason.NotEncodable), (failure.Path, failure.Reason));
			Assert.Equal(1, result.FilesChanged);
		}

		[Fact]
		public async Task AReplacementEqualToTheMatchWritesNothing()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String path = folder.Write("a.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "old");
			Int32 writes = 0;
			CallbackDocumentFileWriter writer = new CallbackDocumentFileWriter((_, _) =>
			{
				writes++;
				return Task.CompletedTask;
			});

			ReplaceResult result = await Applier(writer).ApplyAsync(plan, All(plan), null, CancellationToken.None);

			Assert.Equal(0, writes);
			Assert.Empty(result.Failures);
			Assert.Equal(0, result.FilesChanged);
			Assert.Equal("old", File.ReadAllText(path));
		}

		[Fact]
		public async Task ARunThatIsCancelledStopsBetweenTwoFiles_WhatWasDoneIsKeptAndCanBeUndone()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String first = folder.Write("a.ssl", "old");
			String second = folder.Write("b.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			SyncProgress progress = new SyncProgress(report =>
			{
				if (report.FilesProcessed == 1)
				{
					cancellation.Cancel();
				}
			});
			ReplaceInFilesApplier applier = Applier();

			ReplaceResult result = await applier.ApplyAsync(plan, All(plan), progress, cancellation.Token);

			Assert.True(result.Cancelled);
			Assert.Equal("new", File.ReadAllText(first));
			Assert.Equal("old", File.ReadAllText(second));
			Assert.Equal(1, result.FilesChanged);
			ReplaceUndoResult undo = await applier.UndoAsync(result.Undo!, CancellationToken.None);
			Assert.Equal(1, undo.Restored);
			Assert.Equal("old", File.ReadAllText(first));
		}

		[Theory]
		[InlineData("ansi")]
		[InlineData("utf8bom")]
		[InlineData("utf16")]
		public async Task UndoPutsTheExactBytesBack(String kind)
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			Encoding encoding = kind switch
			{
				"ansi" => TextEncodings.Ansi,
				"utf8bom" => Utf8WithBom,
				_ => Encoding.Unicode,
			};
			String path = folder.Combine("a.ssl");
			WriteBytes(path, encoding, "café old\r\nold\n");
			Byte[] before = File.ReadAllBytes(path);
			ReplacePlan plan = Plan(folder.Path, "old", "something else");
			ReplaceInFilesApplier applier = Applier();
			ReplaceResult result = await applier.ApplyAsync(plan, All(plan), null, CancellationToken.None);
			Assert.NotEqual(before, File.ReadAllBytes(path));

			ReplaceUndoResult undo = await applier.UndoAsync(result.Undo!, CancellationToken.None);

			Assert.Equal(before, File.ReadAllBytes(path));
			Assert.Equal(1, undo.Restored);
			Assert.Empty(undo.Failures);
		}

		[Fact]
		public async Task UndoLeavesAFileThatWasModifiedSinceAndSaysSo_TheOthersAreRestored()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			String touched = folder.Write("a.ssl", "old");
			String untouched = folder.Write("b.ssl", "old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");
			ReplaceInFilesApplier applier = Applier();
			ReplaceResult result = await applier.ApplyAsync(plan, All(plan), null, CancellationToken.None);
			File.AppendAllText(touched, "\r\n// my edit");

			ReplaceUndoResult undo = await applier.UndoAsync(result.Undo!, CancellationToken.None);

			Assert.Equal(1, undo.Restored);
			ReplaceFailure failure = Assert.Single(undo.Failures);
			Assert.Equal((touched, ReplaceFailureReason.Changed), (failure.Path, failure.Reason));
			Assert.Equal("new\r\n// my edit", File.ReadAllText(touched));
			Assert.Equal("old", File.ReadAllText(untouched));
		}

		[Fact]
		public async Task TheUndoRecordHoldsEditsNotTheContentOfTheFiles()
		{
			using ScriptToolsTempDirectory folder = new ScriptToolsTempDirectory();
			folder.Write("a.ssl", new String('x', 2_000_000) + " old");
			ReplacePlan plan = Plan(folder.Path, "old", "new");

			ReplaceResult result = await Applier().ApplyAsync(plan, All(plan), null, CancellationToken.None);

			ReplaceUndoFile file = Assert.Single(result.Undo!.Files);
			ReplaceReverseEdit edit = Assert.Single(file.Edits);
			Assert.Equal(("old", 3), (edit.OldText, edit.Length));
			Assert.Equal(2_000_001, edit.Offset);
			Assert.Equal(32, file.ResultHash.Length);
		}

		[Fact]
		public void AnOpenDocumentIsEditedAsOneUndoableStep_AndStaysUnsaved()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "old one\r\nold two\r\nold three\r\n");
				String diskBefore = File.ReadAllText(document.FilePath!);
				ReplaceInFilesRequest request = new ReplaceInFilesRequest
				{
					Matcher = ReplaceInFilesPlannerTests.Matcher("old"),
					Replacement = "new",
					Sources = new[] { new ReplaceTextSource(document.FilePath!, document.TextDocument.Text, document) },
				};
				ReplacePlan plan = new ReplaceInFilesPlanner().Plan(request, null, CancellationToken.None);

				ReplaceResult result = await new ReplaceInFilesApplier(harness.Workspace).ApplyAsync(plan, All(plan), null, CancellationToken.None);

				Assert.Equal("new one\r\nnew two\r\nnew three\r\n", document.TextDocument.Text);
				Assert.True(document.IsModified);
				Assert.Equal(diskBefore, File.ReadAllText(document.FilePath!));
				Assert.Equal((3, 1), (result.Replacements, result.FilesChanged));
				document.TextDocument.UndoStack.Undo();
				Assert.Equal("old one\r\nold two\r\nold three\r\n", document.TextDocument.Text);
				Assert.False(document.TextDocument.UndoStack.CanUndo && document.TextDocument.Text.Contains("new", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void ADocumentEditedSinceThePreviewIsLeftAlone()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "old\r\n");
				ReplacePlan plan = new ReplaceInFilesPlanner().Plan(DocumentRequest(document), null, CancellationToken.None);
				document.TextDocument.Insert(0, "// typed meanwhile\r\n");

				ReplaceResult result = await new ReplaceInFilesApplier(harness.Workspace).ApplyAsync(plan, All(plan), null, CancellationToken.None);

				Assert.Equal("// typed meanwhile\r\nold\r\n", document.TextDocument.Text);
				Assert.Equal(ReplaceFailureReason.Changed, Assert.Single(result.Failures).Reason);
			});
		}

		[Fact]
		public void ADocumentClosedSinceThePreviewIsReported()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "old\r\n");
				ReplacePlan plan = new ReplaceInFilesPlanner().Plan(DocumentRequest(document), null, CancellationToken.None);
				await harness.Workspace.CloseAsync(document);

				ReplaceResult result = await new ReplaceInFilesApplier(harness.Workspace).ApplyAsync(plan, All(plan), null, CancellationToken.None);

				Assert.Equal(ReplaceFailureReason.Closed, Assert.Single(result.Failures).Reason);
			});
		}

		[Fact]
		public void UndoRestoresADocumentWhileItIsUntouched_AndNotAfterAnEdit()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel first = await harness.OpenFileAsync("a.ssl", "old\r\n");
				DocumentViewModel second = await harness.OpenFileAsync("b.ssl", "old\r\n");
				ReplaceInFilesRequest request = new ReplaceInFilesRequest
				{
					Matcher = ReplaceInFilesPlannerTests.Matcher("old"),
					Replacement = "newer",
					Sources = new[]
					{
						new ReplaceTextSource(first.FilePath!, first.TextDocument.Text, first),
						new ReplaceTextSource(second.FilePath!, second.TextDocument.Text, second),
					},
				};
				ReplacePlan plan = new ReplaceInFilesPlanner().Plan(request, null, CancellationToken.None);
				ReplaceInFilesApplier applier = new ReplaceInFilesApplier(harness.Workspace);
				ReplaceResult result = await applier.ApplyAsync(plan, All(plan), null, CancellationToken.None);
				second.TextDocument.Insert(0, "// more\r\n");

				ReplaceUndoResult undo = await applier.UndoAsync(result.Undo!, CancellationToken.None);

				Assert.Equal("old\r\n", first.TextDocument.Text);
				Assert.Equal("// more\r\nnewer\r\n", second.TextDocument.Text);
				Assert.Equal(1, undo.Restored);
				Assert.Equal(ReplaceFailureReason.Changed, Assert.Single(undo.Failures).Reason);
			});
		}

		private static ReplaceInFilesRequest DocumentRequest(DocumentViewModel document)
		{
			return new ReplaceInFilesRequest
			{
				Matcher = ReplaceInFilesPlannerTests.Matcher("old"),
				Replacement = "new",
				Sources = new[] { new ReplaceTextSource(document.FilePath!, document.TextDocument.Text, document) },
			};
		}

		private sealed class SyncProgress : IProgress<SearchProgress>
		{
			private readonly Action<SearchProgress> _handler;

			public SyncProgress(Action<SearchProgress> handler)
			{
				this._handler = handler;
			}

			public void Report(SearchProgress value)
			{
				this._handler(value);
			}
		}
	}
}