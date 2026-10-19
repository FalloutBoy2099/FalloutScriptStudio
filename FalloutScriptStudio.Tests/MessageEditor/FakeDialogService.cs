namespace FalloutScriptStudio.Tests.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;

	/// <summary>
	/// A scripted <see cref="IDialogService"/> that records what it was asked.
	/// </summary>
	internal sealed class FakeDialogService : IDialogService
	{
		public List<String> Messages { get; } = [];

		public Boolean ConfirmAnswer { get; set; } = true;

		public YesNoCancel SaveAnswer { get; set; } = YesNoCancel.No;

		public String? PromptAnswer { get; set; }

		public String? OpenPath { get; set; }

		public String? SavePath { get; set; }

		public Int32 ConfirmCount { get; private set; }

		public Int32 AskCount { get; private set; }

		public Task ShowInfoAsync(String title, String message)
		{
			this.Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task ShowWarningAsync(String title, String message)
		{
			this.Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task ShowErrorAsync(String title, String message)
		{
			this.Messages.Add(message);
			return Task.CompletedTask;
		}

		public Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.ConfirmCount++;
			return Task.FromResult(this.ConfirmAnswer);
		}

		public Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No")
		{
			this.AskCount++;
			return Task.FromResult(this.SaveAnswer);
		}

		public Task<String?> PromptAsync(String title, String message, String defaultValue = "")
		{
			return Task.FromResult(this.PromptAnswer);
		}

		public String? PickOpenFile(String title, String filter, String? initialDirectory = null)
		{
			return this.OpenPath;
		}

		public IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null)
		{
			return Array.Empty<String>();
		}

		public String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null)
		{
			return this.SavePath;
		}

		public String? PickFolder(String title, String? initialDirectory = null)
		{
			return null;
		}
	}
}