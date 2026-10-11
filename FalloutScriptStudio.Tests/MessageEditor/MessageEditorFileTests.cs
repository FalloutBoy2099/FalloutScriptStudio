namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.ViewModels.MessageEditor;
	using Xunit;

	public sealed class MessageEditorFileTests
	{
		private static readonly String[] _sample =
		{
			"# Intro",
			"{100}{}{Hello there}",
			"{101}{}{Second}",
		};

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

		[Fact]
		public void ExternalChangesAreDetectedAndReloaded()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = harness.WriteFile("a.msg", _sample);
				await harness.Editor.LoadFileAsync(path);
				harness.Editor.CheckExternalChange();
				Assert.False(harness.Editor.HasExternalChange);

				File.WriteAllLines(path, new[] { "{100}{}{Rewritten elsewhere}" });
				harness.Editor.CheckExternalChange();
				Assert.True(harness.Editor.HasExternalChange);

				await harness.Editor.ReloadCommand.ExecuteAsync(null);

				Assert.False(harness.Editor.HasExternalChange);
				Assert.Equal("Rewritten elsewhere", harness.Rows[0].Text);
			});
		}

		[Fact]
		public void KeepingMyVersionStopsTheWarning()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = harness.WriteFile("a.msg", _sample);
				await harness.Editor.LoadFileAsync(path);
				File.WriteAllLines(path, new[] { "{100}{}{Rewritten elsewhere}" });
				harness.Editor.CheckExternalChange();

				harness.Editor.IgnoreExternalChangeCommand.Execute(null);
				harness.Editor.CheckExternalChange();

				Assert.False(harness.Editor.HasExternalChange);
				Assert.Equal("# Intro", harness.Rows[0].Text);
			});
		}

		[Fact]
		public void ADeletedFileIsReported()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = harness.WriteFile("a.msg", _sample);
				await harness.Editor.LoadFileAsync(path);

				File.Delete(path);
				harness.Editor.CheckExternalChange();

				Assert.True(harness.Editor.HasExternalChange);
				Assert.Contains("no longer exists", harness.Editor.ExternalChangeMessage, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void ReloadAsksBeforeDiscardingChanges()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "Edited";
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Editor.ReloadCommand.ExecuteAsync(null);

				Assert.Equal("Edited", harness.Rows[1].Text);
				Assert.Equal(1, harness.Dialogs.ConfirmCount);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Editor.ReloadCommand.ExecuteAsync(null);

				Assert.Equal("Hello there", harness.Rows[1].Text);
				Assert.False(harness.Editor.IsModified);
			});
		}

		[Fact]
		public void TheOemEncodingReadsAndWritesCyrillic()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = Path.Combine(harness.Folder, "ru.msg");
				File.WriteAllBytes(path, TextEncodings.Oem866.GetBytes("{100}{}{Привет}\r\n"));
				harness.Editor.UseOem866 = true;

				await harness.Editor.LoadFileAsync(path);
				Assert.Equal("Привет", harness.Rows[0].Text);

				// The Cyrillic capital Ha looks like the Latin X and is written as such.
				harness.Rows[0].Text = "Х";
				await harness.Editor.SaveDocumentAsync();
				Assert.Equal("{100}{}{X}", File.ReadAllLines(path, TextEncodings.Oem866)[0]);
			});
		}

		[Fact]
		public void ChangingTheEncodingReloadsTheFile()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				String path = Path.Combine(harness.Folder, "ru.msg");
				File.WriteAllBytes(path, TextEncodings.Oem866.GetBytes("{100}{}{Привет}\r\n"));
				await harness.Editor.LoadFileAsync(path);
				String before = harness.Rows[0].Text;

				harness.Editor.UseOem866 = true;
				await harness.Editor.EncodingReloadTask;

				Assert.NotEqual(before, harness.Rows[0].Text);
				Assert.Equal("Привет", harness.Rows[0].Text);
			});
		}

		[Fact]
		public void DecliningTheReloadRestoresTheEncodingChoice()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.OpenAsync("a.msg", _sample);
				harness.Rows[1].Text = "Edited";
				harness.Dialogs.ConfirmAnswer = false;

				harness.Editor.UseOem866 = true;
				await harness.Editor.EncodingReloadTask;

				Assert.False(harness.Editor.UseOem866);
				Assert.Equal("Edited", harness.Rows[1].Text);
			});
		}

		[Fact]
		public void TheEncodingStartsFromTheSettings()
		{
			StaTestRunner.Run(() =>
			{
				using MessageEditorHarness defaults = new MessageEditorHarness();
				Assert.False(defaults.Editor.UseOem866);
				Assert.Equal("ANSI", defaults.Editor.EncodingName);

				using MessageEditorHarness dos = new MessageEditorHarness(settings => settings.Settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				Assert.True(dos.Editor.UseOem866);
				Assert.Equal("OEM 866", dos.Editor.EncodingName);
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
