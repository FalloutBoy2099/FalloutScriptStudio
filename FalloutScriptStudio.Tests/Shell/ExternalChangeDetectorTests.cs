namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class ExternalChangeDetectorTests
	{
		private static DocumentViewModel CreateDocument(String path)
		{
			DocumentViewModel document = new DocumentViewModel(path, DocumentKind.Script, new TextDocument("// text"));
			document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
			return document;
		}

		[Fact]
		public void Nothing_is_asked_when_the_time_stamp_is_unchanged()
		{
			ShellStaRunner.Run(async () =>
			{
				using TempFolder folder = new TempFolder();
				FakeDialogService dialogs = new FakeDialogService();
				DocumentViewModel document = CreateDocument(folder.Write("a.ssl", "// text"));
				List<DocumentViewModel> reloaded = [];

				await new ExternalChangeDetector(dialogs).CheckAsync(new[] { document }, d => { reloaded.Add(d); return Task.FromResult(true); });

				Assert.Empty(dialogs.Confirmations);
				Assert.Empty(reloaded);
			});
		}

		[Fact]
		public void A_changed_file_is_reloaded_only_when_the_user_agrees()
		{
			ShellStaRunner.Run(async () =>
			{
				using TempFolder folder = new TempFolder();
				FakeDialogService dialogs = new FakeDialogService();
				String path = folder.Write("a.ssl", "// text");
				DocumentViewModel document = CreateDocument(path);
				File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
				List<DocumentViewModel> reloaded = [];
				ExternalChangeDetector detector = new ExternalChangeDetector(dialogs);

				dialogs.ConfirmAnswer = false;
				await detector.CheckAsync(new[] { document }, d => { reloaded.Add(d); return Task.FromResult(true); });
				Assert.Single(dialogs.Confirmations);
				Assert.Empty(reloaded);

				// The new time stamp was remembered, so the same change is not offered twice.
				await detector.CheckAsync(new[] { document }, d => { reloaded.Add(d); return Task.FromResult(true); });
				Assert.Single(dialogs.Confirmations);

				File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(10));
				dialogs.ConfirmAnswer = true;
				await detector.CheckAsync(new[] { document }, d => { reloaded.Add(d); return Task.FromResult(true); });
				Assert.Equal(new[] { document }, reloaded);
			});
		}

		[Fact]
		public void A_file_that_vanished_is_skipped()
		{
			ShellStaRunner.Run(async () =>
			{
				using TempFolder folder = new TempFolder();
				FakeDialogService dialogs = new FakeDialogService();
				String path = folder.Write("a.ssl", "// text");
				DocumentViewModel document = CreateDocument(path);
				File.Delete(path);

				await new ExternalChangeDetector(dialogs).CheckAsync(new[] { document }, _ => Task.FromResult(true));

				Assert.Empty(dialogs.Confirmations);
			});
		}
	}
}