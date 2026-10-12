namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Services;
	using Xunit;
	using static FalloutScriptStudio.Tests.MessageEditor.MessageEditorFileTestData;

	[Collection("Test collection for FalloutScriptStudio.Tests.MessageEditor.MessageEditorFileTests")]
	public sealed class MessageEditorSaveTests
	{
		[Fact]
		public void SavingWritesTheFileAndClearsTheModifiedState()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				FakeLineTarget target = new FakeLineTarget();
				harness.Editor.AttachTarget(target, false);
				String path = harness.WriteFile("a.msg", _sample);
				await harness.Editor.LoadFileAsync(path);
				harness.Rows[1].Text = "Edited";
				harness.Rows[2].IsPlayerLine = true;
				Assert.True(harness.Editor.IsModified);
				Assert.True(harness.Rows[1].IsTextChanged);

				Boolean saved = await harness.Editor.SaveDocumentAsync();

				Assert.True(saved);
				Assert.False(harness.Editor.IsModified);
				Assert.False(harness.Rows[1].IsTextChanged);
				Assert.Equal(new[] { "# Intro", "{100}{}{Edited}", "\t{101}{}{Second}" }, File.ReadAllLines(path));
				Assert.Equal(2, target.Updates.Count);
				Assert.Equal("Edited", target.Updates[1].Document.FindMessage(100)!.Text);
				Assert.False(harness.Editor.HasExternalChange);
			});
		}

		[Fact]
		public void SavingAnUnsavedDocumentAsksForAName()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.Editor.NewCommand.ExecuteAsync(null);
				String path = Path.Combine(harness.Folder, "created");
				harness.Dialogs.SavePath = path;

				Boolean saved = await harness.Editor.SaveDocumentAsync();

				Assert.True(saved);
				Assert.Equal(path + ".msg", harness.Editor.FilePath);
				Assert.Equal(new[] { "# Look Name", "{100}{}{}", "# Description", "{101}{}{}" }, File.ReadAllLines(path + ".msg"));
			});
		}

		[Fact]
		public void CancellingTheSaveDialogKeepsTheDocumentModified()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.Editor.NewCommand.ExecuteAsync(null);
				harness.Rows[1].Text = "x";
				harness.Dialogs.SavePath = null;

				Assert.False(await harness.Editor.SaveDocumentAsync());

				Assert.True(harness.Editor.IsModified);
				Assert.Null(harness.Editor.FilePath);
			});
		}

		[Fact]
		public void SaveAsSwitchesToTheNewFile()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				String other = Path.Combine(harness.Folder, "other.msg");
				harness.Dialogs.SavePath = other;

				await harness.Editor.SaveAsCommand.ExecuteAsync(null);

				Assert.Equal(other, harness.Editor.FilePath);
				Assert.True(File.Exists(other));
				Assert.Equal(other, harness.Editor.RecentFiles[0]);
			});
		}

		[Fact]
		public void TheNewDocumentIsNotModifiedButCanBeSaved()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();

				await harness.Editor.NewCommand.ExecuteAsync(null);

				Assert.Equal(4, harness.Rows.Count);
				Assert.Equal("unsaved.msg", harness.Editor.DisplayName);
				Assert.False(harness.Editor.IsModified);
				Assert.True(harness.Editor.SaveCommand.CanExecute(null));
			});
		}

		[Fact]
		public void NewAsksAboutUnsavedChangesAndCanBeCancelled()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "Edited";
				harness.Dialogs.SaveAnswer = YesNoCancel.Cancel;

				await harness.Editor.NewCommand.ExecuteAsync(null);

				Assert.Equal(1, harness.Dialogs.AskCount);
				Assert.Equal("a.msg", harness.Editor.DisplayName);
				Assert.Equal("Edited", harness.Rows[1].Text);
			});
		}

		[Fact]
		public void NewDiscardsTheChangesWhenTheUserAgrees()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "Edited";
				harness.Dialogs.SaveAnswer = YesNoCancel.No;

				await harness.Editor.NewCommand.ExecuteAsync(null);

				Assert.Equal("unsaved.msg", harness.Editor.DisplayName);
				Assert.False(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void NewSavesFirstWhenTheUserAsksForIt()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = harness.WriteFile("a.msg", _sample);
				await harness.Editor.LoadFileAsync(path);
				harness.Rows[1].Text = "Edited";
				harness.Dialogs.SaveAnswer = YesNoCancel.Yes;

				await harness.Editor.NewCommand.ExecuteAsync(null);

				Assert.Contains("{100}{}{Edited}", File.ReadAllLines(path));
				Assert.Equal("unsaved.msg", harness.Editor.DisplayName);
			});
		}

		[Fact]
		public void ClosingAsksOnlyWhenThereAreChanges()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);

				Assert.True(await harness.Editor.CanCloseAsync());
				Assert.Equal(0, harness.Dialogs.AskCount);

				harness.Rows[1].Text = "Edited";
				harness.Dialogs.SaveAnswer = YesNoCancel.Cancel;
				Assert.False(await harness.Editor.CanCloseAsync());
				Assert.Equal(1, harness.Dialogs.AskCount);

				harness.Editor.SkipClosePrompt();
				Assert.True(await harness.Editor.CanCloseAsync());
			});
		}
	}
}
