namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using Xunit;
	using static FalloutScriptStudio.Tests.MessageEditor.MessageEditorFileTestData;

	[Collection("Test collection for FalloutScriptStudio.Tests.MessageEditor.MessageEditorFileTests")]
	public sealed class MessageEditorReloadTests
	{
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
	}
}
