namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Dialogs;

	/// <summary>The dialogue dialogs with a scripted answer for the function editor and an accept-all (or cancel) macro rename preview.</summary>
	internal sealed class FakeDialogueDialogsService : IDialogueDialogsService
	{
		/// <summary>Gets or sets the answer of the function editor; <see langword="null"/> cancels it.</summary>
		public FunctionEditResult? Answer { get; set; }

		/// <summary>Gets the function the editor was last opened with (<see langword="null"/> when a function was being added).</summary>
		public FunctionDefinition? LastInitial { get; private set; }

		/// <summary>Gets a value indicating whether the editor was last opened to add a function.</summary>
		public Boolean? LastIsNew { get; private set; }

		/// <summary>Gets or sets a value indicating whether the macro rename preview is cancelled.</summary>
		public Boolean Cancel { get; set; }

		/// <summary>Gets the last macro rename preview that was shown.</summary>
		public MacroRenamePreview? LastPreview { get; private set; }

		public Task ShowFunctionRulesAsync()
		{
			return Task.CompletedTask;
		}

		public async Task<FunctionDefinition?> EditFunctionAsync(FunctionDefinition? existing)
		{
			FunctionEditResult? result = await this.ShowFunctionEditorAsync(existing);
			return result?.Definition;
		}

		public Task<FunctionEditResult?> ShowFunctionEditorAsync(FunctionDefinition? existing)
		{
			this.LastInitial = existing;
			this.LastIsNew = existing is null;
			return Task.FromResult(this.Answer);
		}

		public Task<IReadOnlyList<PreviewMatch>?> ConfirmMacroRenameAsync(MacroRenamePreview preview)
		{
			this.LastPreview = preview;
			IReadOnlyList<PreviewMatch>? chosen = this.Cancel ? null : preview.Matches.ToList();
			return Task.FromResult(chosen);
		}
	}
}
