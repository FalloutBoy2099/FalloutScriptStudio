namespace FalloutScriptStudio.Tests.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;

	internal sealed class FakeDialogService : IDialogService
	{
		public Queue<String?> FolderAnswers { get; } = new Queue<String?>();

		public Queue<String?> FileAnswers { get; } = new Queue<String?>();

		public List<String> Errors { get; } = [];

		public List<String> Warnings { get; } = [];

		public List<String> Infos { get; } = [];

		public Boolean ConfirmAnswer { get; set; } = true;

		public YesNoCancel YesNoCancelAnswer { get; set; } = YesNoCancel.Yes;

		public Int32 ConfirmCount { get; private set; }

		/// <summary>Gets or sets a task that <see cref="ShowErrorAsync"/> waits for, to keep an error box open.</summary>
		public Task? ErrorGate { get; set; }

		public Task ShowInfoAsync(String title, String message)
		{
			this.Infos.Add(message);
			return Task.CompletedTask;
		}

		public Task ShowWarningAsync(String title, String message)
		{
			this.Warnings.Add(message);
			return Task.CompletedTask;
		}

		public Task ShowErrorAsync(String title, String message)
		{
			this.Errors.Add(message);
			return this.ErrorGate ?? Task.CompletedTask;
		}

		public Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.ConfirmCount++;
			return Task.FromResult(this.ConfirmAnswer);
		}

		public Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			return Task.FromResult(this.YesNoCancelAnswer);
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
			return null;
		}

		public String? PickFolder(String title, String? initialDirectory = null)
		{
			return this.FolderAnswers.Count > 0 ? this.FolderAnswers.Dequeue() : null;
		}
	}
}