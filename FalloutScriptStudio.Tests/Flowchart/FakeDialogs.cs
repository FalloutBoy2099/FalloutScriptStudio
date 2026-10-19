namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;

	/// <summary>A dialog service that records the messages and answers with configured values.</summary>
	internal sealed class FakeDialogs : IDialogService
	{
		public List<String> Infos { get; } = [];

		public List<String> Warnings { get; } = [];

		public List<String> Errors { get; } = [];

		public List<String> Questions { get; } = [];

		public Boolean ConfirmAnswer { get; set; } = true;

		public YesNoCancel YesNoCancelAnswer { get; set; } = YesNoCancel.Yes;

		public String? PromptAnswer { get; set; }

		public String? OpenFileAnswer { get; set; }

		public String? SaveFileAnswer { get; set; }

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
			return Task.CompletedTask;
		}

		public Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.Questions.Add(message);
			return Task.FromResult(this.ConfirmAnswer);
		}

		public Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.Questions.Add(message);
			return Task.FromResult(this.YesNoCancelAnswer);
		}

		public Task<String?> PromptAsync(String title, String message, String defaultValue = "")
		{
			return Task.FromResult(this.PromptAnswer);
		}

		public String? PickOpenFile(String title, String filter, String? initialDirectory = null)
		{
			return this.OpenFileAnswer;
		}

		public IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null)
		{
			return Array.Empty<String>();
		}

		public String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null)
		{
			return this.SaveFileAnswer;
		}

		public String? PickFolder(String title, String? initialDirectory = null)
		{
			return null;
		}
	}
}