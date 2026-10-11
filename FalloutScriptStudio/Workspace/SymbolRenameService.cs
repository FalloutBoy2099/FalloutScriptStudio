namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;

	/// <summary>
	/// The rename refactoring of the main window: asks for the new name, lets the Core refactoring service compute the edits
	/// (honoring the unsaved text of open tabs), shows the preview of a macro rename and applies the edits to the open tabs
	/// (undoable) and to the other files on disk.
	/// </summary>
	public sealed class SymbolRenameService : ISymbolRenameService
	{
		private const String UnsavedPath = "<unsaved>";

		private readonly IRefactoringService _refactoring;
		private readonly IDialogService _dialogs;
		private readonly IScriptToolService _scriptTools;
		private readonly IDialogueDialogsService _dialogsService;
		private readonly ISettingsService _settings;
		private readonly IDocumentFileWriter _writer;

		/// <summary>Initializes the service.</summary>
		public SymbolRenameService(
			IRefactoringService refactoring,
			IDialogService dialogs,
			IScriptToolService scriptTools,
			IDialogueDialogsService dialogsService,
			ISettingsService settings,
			IDocumentFileWriter? writer = null)
		{
			ArgumentNullException.ThrowIfNull(refactoring);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(scriptTools);
			ArgumentNullException.ThrowIfNull(dialogsService);
			ArgumentNullException.ThrowIfNull(settings);
			this._refactoring = refactoring;
			this._dialogs = dialogs;
			this._scriptTools = scriptTools;
			this._dialogsService = dialogsService;
			this._settings = settings;
			this._writer = writer ?? new AtomicDocumentFileWriter();
		}

		public async Task<Boolean> RenameAsync(IWorkspaceManager workspace, DocumentViewModel document, IScriptSymbol symbol)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);
			ArgumentNullException.ThrowIfNull(symbol);

			if (symbol.IsImported)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Common.Rename"), Loc.Get("Str.Shell.Dlg.ImportedRename"));
				return false;
			}

			ProgramInfo? previousInfo = document.ProgramInfo;
			if (previousInfo is null || document.NeedsParse)
			{
				await workspace.RequestParseAsync(document, true);
			}

			ProgramInfo? info = document.ProgramInfo;
			if (info is null)
			{
				return false;
			}

			if (!ReferenceEquals(info, previousInfo))
			{
				// The symbol (for example the one of the procedures tree) belongs to the parse before the text changed: its lines and
				// references are out of date, so the symbol of the new parse takes its place.
				IScriptSymbol? current = FindSymbol(info, symbol);
				if (current is not null)
				{
					symbol = current;
				}
				else if (previousInfo is not null)
				{
					await this._dialogs.ShowWarningAsync(Loc.Get("Str.Common.Rename"), Loc.Get("Str.Shell.Dlg.CannotRename"));
					return false;
				}
			}

			Boolean isMacro = symbol is ScriptMacro;
			Boolean isGlobalMacro = symbol is ScriptMacro global && IsGlobalMacro(info, global);
			List<RefactorDocument> documents = this.CollectDocuments(workspace, document, isGlobalMacro);

			String? newName = await this.AskNewNameAsync(symbol, info, document, documents);
			if (String.IsNullOrEmpty(newName))
			{
				return false;
			}

			if (symbol.IsExported && !await this._dialogs.ConfirmAsync(
				Loc.Get("Str.Common.Rename"),
				Loc.Format("Str.Shell.Dlg.RenameExported", symbol.Name)))
			{
				return false;
			}

			Boolean renameInDefiningFile = false;
			if (symbol is ScriptProcedure procedure && IsDefinedElsewhere(procedure, document))
			{
				// Like the legacy editor: a procedure defined in another file can be renamed there too.
				YesNoCancel answer = await this._dialogs.AskYesNoCancelAsync(
					Loc.Get("Str.Common.Rename"),
					Loc.Format("Str.Shell.Dlg.RenameInDefiningFile", Path.GetFileName(procedure.StartFile)));
				if (answer == YesNoCancel.Cancel)
				{
					return false;
				}

				renameInDefiningFile = answer == YesNoCancel.Yes;
				if (renameInDefiningFile)
				{
					this.AddDefiningFile(documents, procedure.StartFile);
				}
			}

			RenameResult result;
			if (symbol is ScriptMacro scriptMacro)
			{
				MacroRenamePreview preview = this._refactoring.PreviewMacroRename(scriptMacro, newName, documents);
				if (preview.Matches.Count == 0)
				{
					await this._dialogs.ShowInfoAsync(Loc.Get("Str.Common.Rename"), Loc.Get("Str.Shell.Dlg.NoPlaces"));
					return false;
				}

				IReadOnlyList<PreviewMatch>? chosen = isGlobalMacro ? await this._dialogsService.ConfirmMacroRenameAsync(preview) : preview.Matches;
				if (chosen is null)
				{
					return false;
				}

				result = this._refactoring.ApplyMacroRename(preview, chosen);
			}
			else
			{
				result = this._refactoring.Rename(new RenameRequest(info, symbol, newName, document.FilePath ?? UnsavedPath, documents) { RenameInDefiningFile = renameInDefiningFile });
			}

			if (!result.Succeeded)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Common.Rename"), result.Validation.Message ?? Loc.Get("Str.Shell.Dlg.CannotRename"));
				return false;
			}

			Int32 changed = await this.ApplyAsync(workspace, document, result, documents);
			String status = Loc.Format("Str.Shell.Output.Renamed", symbol.Name, newName, result.EditCount, result.Files.Count);
			if (result.UnresolvedReferences.Count > 0)
			{
				status += " " + Loc.Format("Str.Shell.Output.UnresolvedReferences", result.UnresolvedReferences.Count);
			}

			workspace.ReportStatus(status);
			if (isGlobalMacro)
			{
				// The macros of the headers folder changed on disk or in open tabs: the registry must follow (legacy GetGlobalMacros).
				await workspace.ReloadGlobalMacrosAsync();
			}

			return changed > 0 || isMacro;
		}

		private static Boolean IsDefinedElsewhere(ScriptProcedure procedure, DocumentViewModel document)
		{
			return procedure.StartFile.Length > 0
				&& !String.Equals(GetFullPath(procedure.StartFile), GetFullPath(document.FilePath ?? UnsavedPath), StringComparison.OrdinalIgnoreCase);
		}

		private void AddDefiningFile(List<RefactorDocument> documents, String path)
		{
			String full = GetFullPath(path);
			if (documents.Any(d => String.Equals(GetFullPath(d.Path), full, StringComparison.OrdinalIgnoreCase)) || !File.Exists(full))
			{
				return;
			}

			try
			{
				documents.Add(new RefactorDocument(path, DocumentTextCodec.Decode(File.ReadAllBytes(full), this._settings.Settings.Editor.FileEncoding.ToEncoding()).Text, false));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The file stays out of the rename; the Core service reports that the defining file was not supplied.
			}
		}

		/// <summary>Finds the symbol of a new parse that stands for a symbol of an older parse of the same script.</summary>
		private static IScriptSymbol? FindSymbol(ProgramInfo info, IScriptSymbol symbol)
		{
			switch (symbol)
			{
				case ScriptProcedure procedure:
					return info.Procedures.FirstOrDefault(p => String.Equals(p.Name, procedure.Name, StringComparison.OrdinalIgnoreCase));
				case ScriptMacro macro:
					if (info.ScriptMacros.TryGetValue(macro.Token, out ScriptMacro? local))
					{
						return local;
					}

					return info.GlobalMacros.TryGetValue(macro.Token, out ScriptMacro? shared) ? shared : null;
				case ScriptVariable variable:
					return info.Variables.Concat(info.Procedures.SelectMany(p => p.Variables))
						.Where(v => v.Scope == variable.Scope && String.Equals(v.Name, variable.Name, StringComparison.OrdinalIgnoreCase))
						.OrderBy(v => String.Equals(v.DeclaredFile, variable.DeclaredFile, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
						.ThenBy(v => Math.Abs(v.DeclaredLine - variable.DeclaredLine))
						.FirstOrDefault();
				default:
					return null;
			}
		}

		private static Boolean IsGlobalMacro(ProgramInfo info, ScriptMacro macro)
		{
			return info.GlobalMacros.TryGetValue(macro.Token, out ScriptMacro? registered) && ReferenceEquals(registered, macro);
		}

		private static String GetFullPath(String path)
		{
			try
			{
				return Path.GetFullPath(path);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path;
			}
		}

		private List<RefactorDocument> CollectDocuments(IWorkspaceManager workspace, DocumentViewModel current, Boolean includeProject)
		{
			List<RefactorDocument> documents = new List<RefactorDocument>();
			HashSet<String> seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			foreach ((String path, String text) in workspace.GetOpenDocumentTexts())
			{
				if (seen.Add(path))
				{
					documents.Add(new RefactorDocument(path, text, true));
				}
			}

			if (current.FilePath is null)
			{
				documents.Add(new RefactorDocument(UnsavedPath, current.TextDocument.Text, true));
			}

			if (!includeProject)
			{
				return documents;
			}

			String? folder = this._settings.Settings.Paths.SolutionFolder;
			if (folder is null || !Directory.Exists(folder))
			{
				return documents;
			}

			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			foreach (String file in EnumerateProjectFiles(folder))
			{
				String full = GetFullPath(file);
				if (!seen.Add(full))
				{
					continue;
				}

				try
				{
					documents.Add(new RefactorDocument(full, DocumentTextCodec.Decode(File.ReadAllBytes(full), fallback).Text, false));
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// A file that cannot be read is skipped.
				}
			}

			return documents;
		}

		private static IEnumerable<String> EnumerateProjectFiles(String folder)
		{
			try
			{
				return Directory.EnumerateFiles(folder, "*.ssl", SearchOption.AllDirectories)
					.Concat(Directory.EnumerateFiles(folder, "*.h", SearchOption.AllDirectories))
					.ToList();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Array.Empty<String>();
			}
		}

		private async Task<Int32> ApplyAsync(IWorkspaceManager workspace, DocumentViewModel current, RenameResult result, IReadOnlyList<RefactorDocument> documents)
		{
			// The edits are offsets into the texts the rename was computed on (before the dialogs); any file that is not exactly
			// that text any more would be corrupted, so everything is checked before the first file is touched.
			Dictionary<String, String> computedOn = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
			foreach (RefactorDocument source in documents)
			{
				computedOn.TryAdd(source.Path, source.Text);
			}

			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			List<(FileEdits File, DocumentViewModel? Open, DecodedText? Disk)> plan = new List<(FileEdits, DocumentViewModel?, DecodedText?)>();
			List<String> problems = new List<String>();
			foreach (FileEdits file in result.Files)
			{
				computedOn.TryGetValue(file.Path, out String? expected);
				DocumentViewModel? open = String.Equals(file.Path, UnsavedPath, StringComparison.Ordinal) ? current : workspace.FindDocument(file.Path);
				if (open is not null)
				{
					if (expected is not null && !String.Equals(open.TextDocument.Text, expected, StringComparison.Ordinal))
					{
						problems.Add(Loc.Format("Str.Shell.Dlg.RenameFileChanged", file.Path));
					}

					plan.Add((file, open, null));
					continue;
				}

				try
				{
					DecodedText decoded = DocumentTextCodec.Decode(await File.ReadAllBytesAsync(file.Path), fallback);
					if (expected is not null && !String.Equals(decoded.Text, expected, StringComparison.Ordinal))
					{
						problems.Add(Loc.Format("Str.Shell.Dlg.RenameFileChanged", file.Path));
					}

					plan.Add((file, null, decoded));
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					problems.Add(Loc.Format("Str.Shell.Dlg.RenameFileFailed", file.Path, ex.Message));
				}
			}

			if (problems.Count > 0)
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Common.Rename"), Loc.Get("Str.Shell.Dlg.RenameAborted") + "\n\n" + String.Join("\n\n", problems));
				return 0;
			}

			List<String> modified = new List<String>();
			List<DocumentViewModel> touched = new List<DocumentViewModel>();
			foreach ((FileEdits file, DocumentViewModel? open, DecodedText? disk) in plan)
			{
				if (open is not null)
				{
					open.TextDocument.ApplyEdits(file.Edits);
					touched.Add(open);
					modified.Add(file.Path);
					continue;
				}

				try
				{
					await this._writer.WriteAsync(file.Path, DocumentTextCodec.Encode(disk!.Encoding, file.ApplyTo(disk.Text)));
					modified.Add(file.Path);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					problems.Add(Loc.Format("Str.Shell.Dlg.RenameFileFailed", file.Path, ex.Message));
				}
			}

			foreach (DocumentViewModel document in touched)
			{
				await workspace.RequestParseAsync(document, true);
			}

			if (problems.Count > 0)
			{
				// Some files are renamed and some are not: say exactly which, so the user can finish or undo the rename by hand.
				String report = modified.Count > 0 ? Loc.Format("Str.Shell.Dlg.RenamePartial", String.Join("\n", modified)) + "\n\n" : String.Empty;
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Common.Rename"), report + String.Join("\n\n", problems));
			}

			return modified.Count;
		}

		private async Task<String?> AskNewNameAsync(IScriptSymbol symbol, ProgramInfo info, DocumentViewModel document, IReadOnlyList<RefactorDocument> documents)
		{
			if (symbol is ScriptProcedure)
			{
				IReadOnlyCollection<String> existing = info.Procedures.Select(p => p.Name).ToList();
				String? name = await this._scriptTools.PromptProcedureNameAsync(symbol.Name, ProcedureNameDialogMode.Rename, existing);
				return String.IsNullOrWhiteSpace(name) ? null : name.Trim();
			}

			String title = symbol switch
			{
				ScriptMacro => Loc.Get("Str.Shell.Dlg.RenameMacroTitle"),
				ScriptVariable { IsArgument: true } => Loc.Get("Str.Shell.Dlg.RenameArgumentTitle"),
				_ => Loc.Get("Str.Shell.Dlg.RenameVariableTitle"),
			};
			while (true)
			{
				String? name = await this._dialogs.PromptAsync(title, Loc.Format("Str.Shell.Dlg.NewNamePrompt", symbol.Name), symbol.Name);
				if (name is null)
				{
					return null;
				}

				name = name.Trim();
				RenameValidation validation = this._refactoring.Validate(new RenameRequest(info, symbol, name, document.FilePath ?? UnsavedPath, documents));
				if (validation.Error == RenameError.SameName || validation.Error == RenameError.EmptyName)
				{
					return null;
				}

				if (!validation.IsValid)
				{
					await this._dialogs.ShowWarningAsync(title, validation.Message ?? Loc.Get("Str.Shell.Dlg.NameNotValid"));
					continue;
				}

				return validation.NewName;
			}
		}
	}
}
