namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;

	internal sealed class FakeDialogService : IDialogService
	{
		public Queue<String?> FolderAnswers { get; } = new Queue<String?>();

		public Queue<String?> FileAnswers { get; } = new Queue<String?>();

		public Queue<String?> SaveAnswers { get; } = new Queue<String?>();

		public List<String> Errors { get; } = [];

		public List<String> Infos { get; } = [];

		public Boolean ConfirmAnswer { get; set; } = true;

		/// <summary>Gets how many times a confirmation was asked.</summary>
		public Int32 ConfirmCount { get; private set; }

		public String? LastFolderInitialDirectory { get; private set; }

		public Task ShowInfoAsync(String title, String message)
		{
			this.Infos.Add(message);
			return Task.CompletedTask;
		}

		public Task ShowWarningAsync(String title, String message)
		{
			return Task.CompletedTask;
		}

		public Task ShowErrorAsync(String title, String message)
		{
			this.Errors.Add(message);
			return Task.CompletedTask;
		}

		public Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.ConfirmCount++;
			return Task.FromResult(this.ConfirmAnswer);
		}

		public Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			return Task.FromResult(YesNoCancel.Yes);
		}

		public Task<String?> PromptAsync(String title, String message, String defaultValue = "")
		{
			return Task.FromResult<String?>(defaultValue);
		}

		public String? PickOpenFile(String title, String filter, String? initialDirectory = null)
		{
			return this.FileAnswers.Count > 0 ? this.FileAnswers.Dequeue() : null;
		}

		public IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null)
		{
			return Array.Empty<String>();
		}

		public String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null)
		{
			return this.SaveAnswers.Count > 0 ? this.SaveAnswers.Dequeue() : null;
		}

		public String? PickFolder(String title, String? initialDirectory = null)
		{
			this.LastFolderInitialDirectory = initialDirectory;
			return this.FolderAnswers.Count > 0 ? this.FolderAnswers.Dequeue() : null;
		}
	}
}