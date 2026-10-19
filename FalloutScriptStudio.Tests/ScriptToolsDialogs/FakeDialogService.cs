namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services;

	internal sealed class FakeDialogService : IDialogService
	{
		public List<String> Errors { get; } = [];

		public Boolean ConfirmAnswer { get; set; } = true;

		public Int32 ConfirmCount { get; private set; }

		public Task ShowInfoAsync(String title, String message)
		{
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
			return null;
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
			return null;
		}
	}
}