namespace FalloutScriptStudio.Services
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;

	/// <summary>The answer to a Yes / No / Cancel question.</summary>
	public enum YesNoCancel
	{
		Yes,

		No,

		Cancel,
	}

	/// <summary>
	/// Shows Fluent message boxes, prompts and the standard file / folder pickers.
	/// ViewModels use this instead of touching windows directly.
	/// </summary>
	public interface IDialogService
	{
		/// <summary>Shows an informational message.</summary>
		Task ShowInfoAsync(String title, String message);

		/// <summary>Shows a warning message.</summary>
		Task ShowWarningAsync(String title, String message);

		/// <summary>Shows an error message.</summary>
		Task ShowErrorAsync(String title, String message);

		/// <summary>Asks a Yes / No question.</summary>
		Task<Boolean> ConfirmAsync(String title, String message, String yesText = "Yes", String noText = "No");

		/// <summary>Asks a Yes / No / Cancel question (typically "save changes?").</summary>
		Task<YesNoCancel> AskYesNoCancelAsync(String title, String message, String yesText = "Yes", String noText = "No");

		/// <summary>Asks the user for a single line of text; returns <see langword="null"/> when cancelled.</summary>
		Task<String?> PromptAsync(String title, String message, String defaultValue = "");

		/// <summary>Shows an Open File dialog for one file.</summary>
		String? PickOpenFile(String title, String filter, String? initialDirectory = null);

		/// <summary>Shows an Open File dialog allowing several files.</summary>
		IReadOnlyList<String> PickOpenFiles(String title, String filter, String? initialDirectory = null);

		/// <summary>Shows a Save File dialog.</summary>
		String? PickSaveFile(String title, String filter, String? initialDirectory = null, String? fileName = null);

		/// <summary>Shows a folder picker.</summary>
		String? PickFolder(String title, String? initialDirectory = null);
	}
}