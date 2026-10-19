namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;

	/// <summary>A dialog service with scripted answers that records what was shown.</summary>
	internal sealed class FakeDialogService : IDialogService
	{
		public Queue<YesNoCancel> YesNoCancelAnswers { get; } = new Queue<YesNoCancel>();

		public Boolean ConfirmAnswer { get; set; } = true;

		public String? SaveFileAnswer { get; set; }

		public String? OpenFileAnswer { get; set; }

		public String? FolderAnswer { get; set; }

		public String? PromptAnswer { get; set; }

		public List<String> Messages { get; } = [];

		public List<String> Confirmations { get; } = [];

		public Task ShowInfoAsync(String title, String message)
		{
			this.Messages.Add(title + ": " + message);
			return Task.CompletedTask;
		}

		public Task ShowWarningAsync(String title, String message)
		{
			this.Messages.Add(title + ": " + message);
			return Task.CompletedTask;
		}

		public Task ShowErrorAsync(String title, String message)
		{
			this.Messages.Add(title + ": " + message);
			return Task.CompletedTask;
		}

		public Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.Confirmations.Add(title + ": " + message);
			return Task.FromResult(this.ConfirmAnswer);
		}

		public Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.Confirmations.Add(title + ": " + message);
			return Task.FromResult(this.YesNoCancelAnswers.Count > 0 ? this.YesNoCancelAnswers.Dequeue() : YesNoCancel.Cancel);
		}

		/// <summary>Gets or sets an action that runs when a prompt is shown (to change the world while the user "types").</summary>
		public Action? OnPrompt { get; set; }

		public Task<String?> PromptAsync(String title, String message, String defaultValue = "")
		{
			this.OnPrompt?.Invoke();
			return Task.FromResult(this.PromptAnswer);
		}

		public String? PickOpenFile(String title, String filter, String? initialDirectory = null)
		{
			return this.OpenFileAnswer;
		}

		public IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null)
		{
			return this.OpenFileAnswer is null ? Array.Empty<String>() : new[] { this.OpenFileAnswer };
		}

		public String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null)
		{
			return this.SaveFileAnswer;
		}

		public String? PickFolder(String title, String? initialDirectory = null)
		{
			return this.FolderAnswer;
		}
	}
}