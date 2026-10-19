namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;
	using static FalloutScriptStudio.Tests.MessageEditor.MessageEditorFileTestData;

	[Collection("Test collection for FalloutScriptStudio.Tests.MessageEditor.MessageEditorFileTests")]
	public sealed class MessageEditorFileTests
	{
		[Fact]
		public void StartsWithOneEmptyLine()
		{
			StaTestRunner.Run(() =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				Assert.Single(harness.Rows);
				Assert.True(harness.Rows[0].IsBlank);
				Assert.Null(harness.Editor.FilePath);
				Assert.Equal("Empty", harness.Editor.DisplayName);
				Assert.False(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void LoadingShowsTheFileAndRemembersItAsRecent()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				Assert.True(await harness.OpenAsync("dialog.msg", _sample));

				Assert.Equal(3, harness.Rows.Count);
				Assert.Equal("dialog.msg", harness.Editor.DisplayName);
				Assert.Equal("dialog.msg - Message Editor", harness.Editor.WindowTitle);
				Assert.False(harness.Editor.IsModified);
				Assert.Equal(new[] { Path.Combine(harness.Folder, "dialog.msg") }, harness.Editor.RecentFiles);
				Assert.Single(harness.Settings.Recent.Messages);
			});
		}

		[Fact]
		public void LoadingAnEmptyFileGivesOneEmptyLine()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				await harness.OpenAsync("empty.msg");

				Assert.Single(harness.Rows);
				Assert.True(harness.Rows[0].IsBlank);
			});
		}

		[Fact]
		public void LoadingAMissingFileReportsAnErrorAndKeepsTheDocument()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				Boolean loaded = await harness.Editor.LoadFileAsync(Path.Combine(harness.Folder, "missing.msg"));

				Assert.False(loaded);
				Assert.Equal(3, harness.Rows.Count);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void LoadingNotifiesTheTargetWithAPrivateCopy()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);

				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "Changed later";

				Assert.Single(target.Updates);
				Assert.Equal("Hello there", target.Updates[0].Document.FindMessage(100)!.Text);
			});
		}

		[Fact]
		public void OpenRecentRemovesMissingFilesOnRequest()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String missing = Path.Combine(harness.Folder, "gone.msg");
				harness.Settings.Recent.AddMessage(missing);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Editor.OpenRecentCommand.ExecuteAsync(missing);

				Assert.DoesNotContain(missing, harness.Settings.Recent.Messages);
			});
		}

		[Fact]
		public void OpenLoadsThePickedFile()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				harness.Dialogs.OpenPath = harness.WriteFile("picked.msg", _sample);

				await harness.Editor.OpenCommand.ExecuteAsync(null);

				Assert.Equal("picked.msg", harness.Editor.DisplayName);
				Assert.Equal(3, harness.Rows.Count);
			});
		}

		[Fact]
		public void OpenAsTextHandsTheFileToTheOwnerAndClosesWithoutAsking()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);
				await harness.OpenAsync("a.msg", _sample);
				Boolean closed = false;
				harness.Editor.CloseRequested += (_, _) => closed = true;

				await harness.Editor.OpenAsTextCommand.ExecuteAsync(null);

				Assert.True(closed);
				Assert.True(harness.Editor.SuppressClosePrompt);
				Assert.Equal(new[] { Path.Combine(harness.Folder, "a.msg") }, target.OpenedAsText);
			});
		}

		[Fact]
		public void OpenAsTextIsUnavailableWithoutAnOwner()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				Assert.False(harness.Editor.OpenAsTextCommand.CanExecute(null));
			});
		}

		[Fact]
		public void TheExternalEditorIsAskedForOnceAndStarted()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				String editor = harness.WriteFile("editor.exe", "MZ");
				harness.Dialogs.OpenPath = editor;

				await harness.Editor.OpenInExternalEditorCommand.ExecuteAsync(null);
				await harness.Editor.OpenInExternalEditorCommand.ExecuteAsync(null);

				Assert.Equal(editor, harness.Settings.Settings.Paths.ExternalEditorPath);
				Assert.Equal(2, harness.Platform.StartedEditors.Count);
				Assert.Equal(Path.Combine(harness.Folder, "a.msg"), harness.Platform.StartedEditors[0].File);
			});
		}

		[Fact]
		public void InitializeSelectsTheRequestedMessage()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = harness.WriteFile("a.msg", _sample);

				await harness.Editor.InitializeAsync(new FalloutScriptStudio.Services.MessageEditor.MessageEditorOpenOptions { FilePath = path, SelectMessageId = 101 });

				Assert.Same(harness.Rows[2], harness.FocusedRows[0]);
				Assert.Equal(MessageGridColumn.Text, harness.FocusRequests[^1].Column);
			});
		}

		[Fact]
		public void SettingsDecideTheInitialOptionsAndOptionChangesAreStored()
		{
			StaTestRunner.Run(() =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				harness.Settings.Settings.Messages.FontSizeIndex = 3;
				harness.Settings.Settings.Messages.ShowLipColumn = true;
				harness.Settings.Settings.Messages.HighlightComments = false;
				harness.Settings.Settings.Messages.CommentColor = CommentHighlightColor.Lavender;
				harness.Settings.Update(_ => { });

				Assert.Equal(3, harness.Editor.FontSizeIndex);
				Assert.True(harness.Editor.ShowLipColumn);
				Assert.False(harness.Editor.HighlightComments);
				Assert.Equal(CommentHighlightColor.Lavender, harness.Editor.CommentColor);

				harness.Editor.IncreaseFontSizeCommand.Execute(null);
				harness.Editor.CommentColor = CommentHighlightColor.PaleGreen;

				Assert.Equal(4, harness.Settings.Settings.Messages.FontSizeIndex);
				Assert.Equal(CommentHighlightColor.PaleGreen, harness.Settings.Settings.Messages.CommentColor);
			});
		}

		[Fact]
		public void TheFontSizeStaysInRange()
		{
			StaTestRunner.Run(() =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				for (Int32 i = 0; i < 20; i++)
				{
					harness.Editor.IncreaseFontSizeCommand.Execute(null);
				}

				Assert.Equal(harness.Editor.FontSizes.Count - 1, harness.Editor.FontSizeIndex);

				for (Int32 i = 0; i < 20; i++)
				{
					harness.Editor.DecreaseFontSizeCommand.Execute(null);
				}

				Assert.Equal(0, harness.Editor.FontSizeIndex);
				Assert.Equal(54, harness.Editor.LineColumnWidth);
				harness.Editor.FontSizeIndex = 7;
				Assert.Equal(75, harness.Editor.LineColumnWidth);
			});
		}
	}
}