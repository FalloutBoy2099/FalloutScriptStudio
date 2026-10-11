namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;

	/// <summary>Creates, deletes and moves the procedures of a script (the text edits are computed by <see cref="IProcedureTextService"/>).</summary>
	public sealed class ProcedureOperations
	{
		private readonly ISettingsService _settings;
		private readonly ParseCoordinator _parser;
		private readonly IDialogService _dialogs;
		private readonly IProcedureTextService _procedureText;
		private readonly IScriptToolService _scriptTools;

		/// <summary>Initializes the operations.</summary>
		public ProcedureOperations(
			ISettingsService settings,
			ParseCoordinator parser,
			IDialogService dialogs,
			IProcedureTextService procedureText,
			IScriptToolService scriptTools)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(procedureText);
			ArgumentNullException.ThrowIfNull(scriptTools);
			this._settings = settings;
			this._parser = parser;
			this._dialogs = dialogs;
			this._procedureText = procedureText;
			this._scriptTools = scriptTools;
		}

		/// <summary>Asks for the name and placement of a new procedure and inserts it into a script.</summary>
		/// <param name="workspace">The workspace that activates the script.</param>
		/// <param name="document">The script.</param>
		/// <param name="suggestedName">The name to start with (the selected procedure or text; its trailing number is incremented), or <see langword="null"/>.</param>
		/// <param name="anchorProcedure">The procedure "after the procedure" and "copy the body" refer to; the procedure under the caret when <see langword="null"/>.</param>
		public async Task CreateAsync(IWorkspace workspace, DocumentViewModel document, String? suggestedName, String? anchorProcedure = null)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);
			if (document.Kind != DocumentKind.Script)
			{
				return;
			}

			ScriptProcedure? anchor = ResolveAnchor(document, anchorProcedure);
			ProcedureNameResult? result = await this.PromptAsync(document, suggestedName ?? String.Empty, ProcedureNameDialogMode.Create, false, anchor);
			if (result is not null)
			{
				await this.InsertAsync(workspace, document, result, anchor);
			}
		}

		/// <summary>Asks how to create one of the standard handlers (placement, copying) and inserts it.</summary>
		/// <param name="workspace">The workspace that activates the script.</param>
		/// <param name="document">The script.</param>
		/// <param name="handlerName">The name of the handler.</param>
		/// <param name="anchorProcedure">The procedure "after the procedure" and "copy the body" refer to; the procedure under the caret when <see langword="null"/>.</param>
		public async Task CreateHandlerAsync(IWorkspace workspace, DocumentViewModel document, String handlerName, String? anchorProcedure = null)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);
			ArgumentException.ThrowIfNullOrEmpty(handlerName);

			if (document.Kind != DocumentKind.Script)
			{
				return;
			}

			if (document.NeedsParse)
			{
				await this._parser.ParseAsync(document, true);
			}

			if (document.ProgramInfo is { } info && info.ContainsName(handlerName, false, true))
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Shell.Dlg.CreateProcedureTitle"), Loc.Get("Str.Shell.Dlg.ProcedureExists"));
				return;
			}

			ScriptProcedure? anchor = ResolveAnchor(document, anchorProcedure);
			ProcedureNameResult? result = await this.PromptAsync(document, handlerName, ProcedureNameDialogMode.Create, true, anchor);
			if (result is not null)
			{
				await this.InsertAsync(workspace, document, result, anchor);
			}
		}

		/// <summary>Deletes a procedure (declaration and body) from a script.</summary>
		/// <returns><see langword="false"/> when the procedure was not found.</returns>
		public async Task<Boolean> DeleteAsync(DocumentViewModel document, String name)
		{
			ArgumentNullException.ThrowIfNull(document);
			ProcedureEditResult? result = this._procedureText.DeleteProcedure(document.TextDocument.Text, name);
			if (result is null)
			{
				return false;
			}

			document.TextDocument.ApplyEdits(result.Edits);
			await this._parser.ParseAsync(document, true);
			return true;
		}

		/// <summary>Moves a procedure in front of another procedure of the same script (or to the end of the script).</summary>
		/// <returns><see langword="false"/> when the move was not possible.</returns>
		public async Task<Boolean> MoveAsync(DocumentViewModel document, String name, String? beforeProcedure)
		{
			ArgumentNullException.ThrowIfNull(document);
			ProcedureEditResult? result = this._procedureText.MoveProcedure(document.TextDocument.Text, name, beforeProcedure);
			if (result is null)
			{
				return false;
			}

			document.TextDocument.ApplyEdits(result.Edits);
			await this._parser.ParseAsync(document, true);
			return true;
		}

		private static ScriptProcedure? ResolveAnchor(DocumentViewModel document, String? anchorProcedure)
		{
			ScriptProcedure? anchor = String.IsNullOrEmpty(anchorProcedure) ? null : document.ProgramInfo?.GetProcedure(anchorProcedure);
			return anchor ?? document.Editor?.CurrentProcedure;
		}

		private async Task InsertAsync(IWorkspace workspace, DocumentViewModel document, ProcedureNameResult answer, ScriptProcedure? current)
		{
			ProcedureInsertRequest request = new ProcedureInsertRequest(answer.Declaration)
			{
				Placement = answer.Placement switch
				{
					ProcedurePlacement.AfterCurrent => InsertPlacement.After,
					ProcedurePlacement.AtCaret => InsertPlacement.Caret,
					_ => InsertPlacement.End,
				},
				AnchorProcedure = current?.Name,
				CaretLine = document.Editor?.CaretLine ?? 1,
				CopyAnchorBody = answer.CopyBody && current is not null,
				UseScriptOverrides = ProcedureHandlerCatalog.RequiresScriptOverrides(answer.Name),
				Indentation = this.GetIndent(),
			};

			ProcedureEditResult? result = this._procedureText.InsertProcedure(document.TextDocument.Text, request);
			if (result is null)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Shell.Dlg.CreateProcedureTitle"), Loc.Get("Str.Shell.Dlg.ProcedureExists"));
				return;
			}

			document.TextDocument.ApplyEdits(result.Edits);
			workspace.ActiveDocument = document;
			DocumentNavigation.Navigate(document, new OpenDocumentOptions { Line = result.FocusLine, Column = 1 });
			await this._parser.ParseAsync(document, true);
		}

		private Task<ProcedureNameResult?> PromptAsync(DocumentViewModel document, String initialName, ProcedureNameDialogMode mode, Boolean nameReadOnly, ScriptProcedure? current)
		{
			return this._scriptTools.PromptProcedureAsync(new ProcedureNameRequest
			{
				InitialName = initialName,
				Mode = mode,
				ExistingNames = document.ProgramInfo?.Procedures.Select(p => p.Name).ToList() ?? new List<String>(),
				NameReadOnly = nameReadOnly,
				CanChoosePlacement = true,
				CanCopyBody = current is not null,
				PreferInsertAfter = current is not null,
			});
		}

		private String GetIndent()
		{
			EditorSettings editor = this._settings.Settings.Editor;
			return editor.TabsToSpaces ? new String(' ', editor.TabSize) : "\t";
		}
	}
}
