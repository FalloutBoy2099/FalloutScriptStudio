namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Services.ToolWindows;

	/// <summary>
	/// The operations that use the whole project: going to the definition of a symbol that is defined in another script or header,
	/// the "Go to symbol" list, project wide references, deleting a procedure that nothing uses, listing the unused variables and
	/// extracting statements into a procedure.
	/// </summary>
	public sealed class CodeIntelligenceOperations
	{
		/// <summary>The most lines of a procedure shown in the confirmation of its deletion.</summary>
		public const Int32 MaxPreviewLines = 18;

		/// <summary>The most uses listed in the message that refuses a deletion.</summary>
		public const Int32 MaxListedUses = 8;

		private readonly ProjectSymbolService _symbols;
		private readonly IToolWindowService _toolWindows;
		private readonly IScriptToolService _scriptTools;
		private readonly IDialogService _dialogs;
		private readonly ISettingsService _settings;
		private readonly ParseCoordinator _parser;
		private readonly IProblemService _problems;
		private readonly IOutputSink _output;

		/// <summary>Initializes the operations.</summary>
		public CodeIntelligenceOperations(
			ProjectSymbolService symbols,
			IToolWindowService toolWindows,
			IScriptToolService scriptTools,
			IDialogService dialogs,
			ISettingsService settings,
			ParseCoordinator parser,
			IProblemService problems,
			IOutputSink output)
		{
			ArgumentNullException.ThrowIfNull(symbols);
			ArgumentNullException.ThrowIfNull(toolWindows);
			ArgumentNullException.ThrowIfNull(scriptTools);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(problems);
			ArgumentNullException.ThrowIfNull(output);
			this._symbols = symbols;
			this._toolWindows = toolWindows;
			this._scriptTools = scriptTools;
			this._dialogs = dialogs;
			this._settings = settings;
			this._parser = parser;
			this._problems = problems;
			this._output = output;
		}

		/// <summary>Shows the "Go to symbol" list and opens the symbol the user chooses.</summary>
		/// <param name="workspace">The workspace that opens the file.</param>
		/// <returns>A task that completes when the list was closed and the symbol opened.</returns>
		public async Task GoToSymbolAsync(IWorkspace workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);

			IndexedSymbol? chosen = await this._toolWindows.PickSymbolAsync(new SymbolPickerRequest
			{
				Mode = SymbolPickerMode.Search,
				Index = this._symbols.Index,
				RootFolder = this._settings.Effective.ProjectFolder,
			});
			if (chosen is not null)
			{
				await this.OpenSymbolAsync(workspace, chosen, chosen.Line);
			}
		}

		/// <summary>
		/// Goes to the definition or the declaration of a symbol that was not found in the document or in the files it includes, or lists
		/// its references: the candidates of the project index are offered in a list when there are several, the nearest first.
		/// </summary>
		/// <param name="workspace">The workspace that opens the file.</param>
		/// <param name="from">The document the symbol is used in.</param>
		/// <param name="name">The name of the symbol.</param>
		/// <param name="kind">What to look for.</param>
		/// <returns>A task that completes when the navigation ended.</returns>
		public async Task NavigateAsync(IWorkspace workspace, DocumentViewModel from, String name, ProjectNavigationKind kind)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(from);
			ArgumentException.ThrowIfNullOrEmpty(name);

			IReadOnlyList<IndexedSymbol> candidates = this._symbols.FindCandidates(name, IndexedSymbolKind.All, from);
			if (kind == ProjectNavigationKind.Definition && candidates.Any(c => c.HasBody && !IsImport(c)))
			{
				candidates = candidates.Where(c => c.HasBody && !IsImport(c)).ToList();
			}

			if (candidates.Count == 0)
			{
				workspace.ReportStatus(Loc.Format("Str.Editor.Status.NotFoundInProject", name));
				return;
			}

			IndexedSymbol? target = candidates[0];
			if (candidates.Count > 1)
			{
				target = await this._toolWindows.PickSymbolAsync(new SymbolPickerRequest
				{
					Mode = SymbolPickerMode.Choose,
					Candidates = candidates,
					Name = name,
					RootFolder = this._settings.Effective.ProjectFolder,
				});
			}

			if (target is null)
			{
				return;
			}

			if (kind == ProjectNavigationKind.References)
			{
				await this.ShowReferencesOfAsync(workspace, name, target, null);
				return;
			}

			await this.OpenSymbolAsync(workspace, target, kind == ProjectNavigationKind.Declaration ? target.DeclarationLine : target.Line);
		}

		/// <summary>Lists the references of a symbol in the files of the project that can see its definition, with the ones the parser found.</summary>
		/// <param name="workspace">The workspace that shows the results.</param>
		/// <param name="symbol">The symbol found in the document or in its includes.</param>
		/// <param name="name">The name of the symbol.</param>
		/// <returns>A task that completes when the references are shown.</returns>
		public async Task ShowReferencesAsync(IWorkspace workspace, IScriptSymbol symbol, String name)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(symbol);

			IndexedSymbolKind kind = symbol.Kind switch
			{
				SymbolKind.Macro => IndexedSymbolKind.Macro,
				SymbolKind.Procedure => IndexedSymbolKind.Procedure,
				_ => IndexedSymbolKind.Variable,
			};
			if (symbol.Kind == SymbolKind.LocalVariable)
			{
				await workspace.ShowReferencesAsync(name, symbol.References);
				return;
			}

			String? definingFile = symbol.Declaration?.File;
			await this.ShowReferencesCoreAsync(workspace, name, kind, definingFile, symbol.IsImported || symbol.IsExported, symbol.References);
		}

		/// <summary>Deletes a procedure that nothing uses.</summary>
		/// <param name="workspace">The workspace.</param>
		/// <param name="document">The script that holds the body of the procedure.</param>
		/// <param name="name">The name of the procedure.</param>
		/// <returns><see langword="true"/> when the procedure was deleted.</returns>
		public async Task<Boolean> DeleteUnusedProcedureAsync(IWorkspace workspace, DocumentViewModel document, String name)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);
			ArgumentException.ThrowIfNullOrEmpty(name);

			String title = Loc.Get("Str.Shell.Dlg.DeleteUnusedTitle");
			if (!document.IsParsable)
			{
				return false;
			}

			if (UnusedProcedureRemover.IsEngineHandler(name, this.LoadHandlerNames()))
			{
				await this._dialogs.ShowWarningAsync(title, Loc.Format("Str.Shell.Dlg.DeleteUnusedHandler", name));
				return false;
			}

			ProcedureRemovalPlan? plan = UnusedProcedureRemover.Plan(document.TextDocument.Text, name);
			if (plan is null)
			{
				await this._dialogs.ShowWarningAsync(title, Loc.Format("Str.Shell.Dlg.DeleteUnusedNoBody", name));
				return false;
			}

			IReadOnlyList<SourceReference> uses = await this.FindUsesAsync(document, plan);
			if (uses.Count > 0)
			{
				await this._dialogs.ShowWarningAsync(title, Loc.Format("Str.Shell.Dlg.DeleteUnusedReferenced", plan.Name, uses.Count, FormatUses(uses)));
				await workspace.ShowReferencesAsync(plan.Name, uses);
				return false;
			}

			String preview = FormatPreview(plan);
			if (!await this._dialogs.ConfirmAsync(title, Loc.Format("Str.Shell.Dlg.DeleteUnusedConfirm", plan.Name, preview)))
			{
				return false;
			}

			document.TextDocument.ApplyEdits(plan.Edits);
			workspace.ActiveDocument = document;
			await this._parser.ParseAsync(document, true);
			workspace.ReportStatus(Loc.Format("Str.Shell.Status.ProcedureDeleted", plan.Name));
			return true;
		}

		/// <summary>Lists the unused variables of a script in the list of problems.</summary>
		/// <param name="workspace">The workspace.</param>
		/// <param name="document">The script.</param>
		/// <returns>The number of variables listed.</returns>
		public Task<Int32> FindUnusedVariablesAsync(IWorkspace workspace, DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);

			this._problems.Remove(problem => problem.Origin == ProblemOrigins.UnusedVariables && ReferenceEquals(problem.Owner, document));
			if (!document.IsParsable)
			{
				return Task.FromResult(0);
			}

			Boolean script = document.Kind == DocumentKind.Script;
			IReadOnlyList<UnusedVariable> found = UnusedVariableScan.Find(document.TextDocument.Text, document.ProgramInfo, true, script, true);
			this._problems.ReportRange(found.Select(variable => ProblemFactory.UnusedVariable(variable, document.FilePath, document)));
			if (found.Count > 0)
			{
				this._output.ShowTab(OutputTab.Errors);
			}

			workspace.ReportStatus(found.Count == 0
				? Loc.Format("Str.Shell.Status.NoUnusedVariables", document.FileName)
				: Loc.Format("Str.Shell.Status.UnusedVariablesFound", found.Count, document.FileName));
			return Task.FromResult(found.Count);
		}

		/// <summary>Removes the declaration of an unused variable (the fix of a problem).</summary>
		/// <param name="workspace">The workspace that opens the script when it is not open.</param>
		/// <param name="file">The script.</param>
		/// <param name="line">The 1-based line of the name of the variable.</param>
		/// <param name="column">The 1-based column of the name of the variable.</param>
		/// <returns>A task that completes when the declaration was removed.</returns>
		public async Task RemoveUnusedVariableAsync(IWorkspaceManager workspace, String file, Int32 line, Int32 column)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentException.ThrowIfNullOrEmpty(file);

			DocumentViewModel? document = workspace.FindDocument(file) ?? await workspace.OpenAsync(file);
			if (document is null)
			{
				return;
			}

			String text = document.TextDocument.Text;
			TextLineMap map = new TextLineMap(text);
			if (line < 1 || line > map.LineCount)
			{
				return;
			}

			Int32 offset = map.GetLineStart(line) + Math.Max(column - 1, 0);
			UnusedVariable? variable = UnusedVariableScan.Find(text, document.ProgramInfo, true, true, true).FirstOrDefault(v => v.Offset == offset);
			TextEdit? edit = variable is null ? null : VariableDeclarationEditor.CreateRemoval(text, variable);
			if (edit is null)
			{
				workspace.ReportStatus(Loc.Get("Str.Shell.Status.UnusedVariableNotRemoved"));
				return;
			}

			document.TextDocument.ApplyEdits(new[] { edit });
			this._problems.Remove(problem => problem.Origin == ProblemOrigins.UnusedVariables && ReferenceEquals(problem.Owner, document) && problem.Line == line);
			await this._parser.ParseAsync(document, true);
		}

		/// <summary>Extracts the statements selected in a script into a new procedure placed in front of the current one.</summary>
		/// <param name="workspace">The workspace.</param>
		/// <param name="document">The script.</param>
		/// <returns>A task that completes when the refactoring ended.</returns>
		public async Task ExtractProcedureAsync(IWorkspace workspace, DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(document);

			String title = Loc.Get("Str.Shell.Dlg.ExtractTitle");
			if (document.Editor is not { } editor || !document.IsParsable)
			{
				return;
			}

			String text = document.TextDocument.Text;
			ExtractAnalysis analysis = ProcedureExtractor.Analyze(text, editor.SelectionStart, editor.SelectionLength);
			if (!analysis.IsValid)
			{
				await this._dialogs.ShowWarningAsync(title, GetFailureMessage(analysis));
				return;
			}

			List<String> existing = new InternalParser().ParseProcedures(text, null).Select(p => p.Name).ToList();
			if (document.ProgramInfo is { } info)
			{
				existing.AddRange(info.Procedures.Select(p => p.Name));
			}

			String initial = "NewProcedure" + (analysis.Parameters.Count == 0 ? String.Empty : "(" + String.Join(",", analysis.Parameters) + ")");
			ProcedureNameResult? answer;
			while (true)
			{
				answer = await this._scriptTools.PromptProcedureAsync(new ProcedureNameRequest
				{
					InitialName = initial,
					Mode = ProcedureNameDialogMode.Extract,
					ExistingNames = existing,
					Subject = String.Join(", ", analysis.Parameters),
				});
				if (answer is null)
				{
					return;
				}

				if (analysis.MatchesParameters(ProcedureExtractor.GetArgumentNames(answer.Declaration)))
				{
					break;
				}

				await this._dialogs.ShowWarningAsync(title, Loc.Format("Str.Shell.Dlg.ExtractParameters", String.Join(", ", analysis.Parameters)));
				initial = answer.Name + (analysis.Parameters.Count == 0 ? String.Empty : "(" + String.Join(",", analysis.Parameters) + ")");
			}

			ExtractResult result = ProcedureExtractor.CreateEdits(text, analysis, answer.Declaration, this.GetIndent());
			document.TextDocument.ApplyEdits(result.Edits);
			workspace.ActiveDocument = document;
			DocumentNavigation.Navigate(document, new OpenDocumentOptions { Line = result.CallLine, Column = 1 });
			await this._parser.ParseAsync(document, true);
			workspace.ReportStatus(Loc.Format("Str.Shell.Status.ProcedureExtracted", answer.Name));
		}

		/// <summary>Gets the text that tells why a selection cannot be extracted.</summary>
		/// <param name="analysis">The analysis, which is not valid.</param>
		/// <returns>The message in the current language.</returns>
		public static String GetFailureMessage(ExtractAnalysis analysis)
		{
			ArgumentNullException.ThrowIfNull(analysis);
			return GetFailureMessage(analysis.Failure, analysis.Detail);
		}

		/// <summary>Gets the text that tells why a selection cannot be extracted.</summary>
		/// <param name="failure">Why.</param>
		/// <param name="detail">The name that explains the failure (a variable), or <see langword="null"/>.</param>
		/// <returns>The message in the current language.</returns>
		public static String GetFailureMessage(ExtractFailure failure, String? detail)
		{
			return failure switch
			{
				ExtractFailure.EmptySelection => Loc.Get("Str.Shell.Dlg.ExtractEmptySelection"),
				ExtractFailure.NotInsideProcedure => Loc.Get("Str.Shell.Dlg.ExtractNotInProcedure"),
				ExtractFailure.ContainsPreprocessorLine => Loc.Get("Str.Shell.Dlg.ExtractPreprocessor"),
				ExtractFailure.UnbalancedBlock => Loc.Get("Str.Shell.Dlg.ExtractUnbalanced"),
				ExtractFailure.PartialStatement => Loc.Get("Str.Shell.Dlg.ExtractPartial"),
				ExtractFailure.ContainsReturn => Loc.Get("Str.Shell.Dlg.ExtractReturn"),
				ExtractFailure.ContainsLoopControl => Loc.Get("Str.Shell.Dlg.ExtractLoopControl"),
				ExtractFailure.DeclaredVariableUsedAfter => Loc.Format("Str.Shell.Dlg.ExtractDeclaredUsedAfter", detail ?? String.Empty),
				ExtractFailure.TooManyResults => Loc.Format("Str.Shell.Dlg.ExtractTooManyResults", detail ?? String.Empty),
				_ => String.Empty,
			};
		}

		private static Boolean IsImport(IndexedSymbol symbol)
		{
			return (symbol.Kind == IndexedSymbolKind.Procedure && (symbol.Flags & ProcedureFlags.Import) != ProcedureFlags.None)
				|| (symbol.Kind == IndexedSymbolKind.Variable && symbol.Scope == VariableScope.Import);
		}

		private static String FormatUses(IReadOnlyList<SourceReference> uses)
		{
			IEnumerable<String> lines = uses.Take(MaxListedUses).Select(use => "  " + Path.GetFileName(use.File ?? String.Empty) + ":" + use.Line.ToString(System.Globalization.CultureInfo.InvariantCulture));
			String text = String.Join("\n", lines);
			return uses.Count > MaxListedUses ? text + "\n  ..." : text;
		}

		private static String FormatPreview(ProcedureRemovalPlan plan)
		{
			String[] lines = plan.PreviewText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
			String preview = lines.Length > MaxPreviewLines
				? String.Join("\n", lines.Take(MaxPreviewLines)) + "\n..."
				: String.Join("\n", lines);
			return plan.DeclarationText is null ? preview : preview + "\n\n" + plan.DeclarationText;
		}

		private IReadOnlyList<String> LoadHandlerNames()
		{
			String path = Path.Combine(this._settings.Paths.DescriptionsDirectory, "prochandlers.ini");
			try
			{
				if (!File.Exists(path))
				{
					return Array.Empty<String>();
				}

				return ProcedureHandlerCatalog.Parse(File.ReadAllLines(path)).Handlers.Select(h => h.Name).ToList();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Array.Empty<String>();
			}
		}

		private async Task<IReadOnlyList<SourceReference>> FindUsesAsync(DocumentViewModel document, ProcedureRemovalPlan plan)
		{
			String? file = document.FilePath is null ? null : DocumentPaths.GetFullPath(document.FilePath);
			List<SourceReference> uses = new List<SourceReference>();
			IEnumerable<SymbolOccurrence> occurrences;
			if (file is null)
			{
				occurrences = SymbolOccurrenceScanner.Scan(document.TextDocument.Text, String.Empty, plan.Name, false, true);
			}
			else
			{
				occurrences = await this._symbols.FindOccurrencesAsync(plan.Name, IndexedSymbolKind.Procedure, file, plan.IsExported, true);
			}

			foreach (SymbolOccurrence occurrence in occurrences.Where(UnusedProcedureRemover.IsUsage))
			{
				Boolean insideItself = String.Equals(occurrence.File, file ?? String.Empty, StringComparison.OrdinalIgnoreCase)
					&& occurrence.Line >= plan.FirstLine && occurrence.Line <= plan.LastLine;
				if (!insideItself)
				{
					uses.Add(new SourceReference(occurrence.File.Length == 0 ? null : occurrence.File, occurrence.Line));
				}
			}

			return uses;
		}

		private async Task ShowReferencesOfAsync(IWorkspace workspace, String name, IndexedSymbol target, IReadOnlyList<SourceReference>? known)
		{
			Boolean shared = IsImport(target) || (target.Kind == IndexedSymbolKind.Procedure && (target.Flags & ProcedureFlags.Export) != ProcedureFlags.None)
				|| (target.Kind == IndexedSymbolKind.Variable && target.Scope == VariableScope.Export);
			await this.ShowReferencesCoreAsync(workspace, name, target.Kind, target.File, shared, known ?? Array.Empty<SourceReference>());
		}

		private async Task ShowReferencesCoreAsync(IWorkspace workspace, String name, IndexedSymbolKind kind, String? definingFile, Boolean allFiles, IReadOnlyList<SourceReference> known)
		{
			IReadOnlyList<SourceReference> found = await this._symbols.FindReferencesAsync(name, kind, definingFile, allFiles);
			List<SourceReference> merged = new List<SourceReference>(known);
			HashSet<(String File, Int32 Line)> seen = new HashSet<(String File, Int32 Line)>(known.Select(r => (Normalize(r.File), r.Line)));
			foreach (SourceReference reference in found)
			{
				if (seen.Add((Normalize(reference.File), reference.Line)))
				{
					merged.Add(reference);
				}
			}

			if (merged.Count == 0)
			{
				workspace.ReportStatus(Loc.Format("Str.Editor.Status.NoReferences", name));
				return;
			}

			merged.Sort((a, b) =>
			{
				Int32 byFile = String.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
				return byFile != 0 ? byFile : a.Line.CompareTo(b.Line);
			});
			await workspace.ShowReferencesAsync(name, merged);
		}

		private static String Normalize(String? file)
		{
			return String.IsNullOrEmpty(file) ? String.Empty : DocumentPaths.GetFullPath(file).ToUpperInvariant();
		}

		private async Task OpenSymbolAsync(IWorkspace workspace, IndexedSymbol symbol, Int32 line)
		{
			Int32 column = -1;
			Int32 length = -1;
			String? lineText = ReadLine(workspace, symbol.File, line);
			if (lineText is not null)
			{
				Int32 index = lineText.IndexOf(symbol.Name, symbol.Kind == IndexedSymbolKind.Macro ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
				if (index >= 0)
				{
					column = index + 1;
					length = symbol.Name.Length;
				}
			}

			await workspace.GoToAsync(symbol.File, line, column, length);
		}

		private String? ReadLine(IWorkspace workspace, String file, Int32 line)
		{
			if (workspace.FindDocument(file) is { } open)
			{
				return line >= 1 && line <= open.TextDocument.LineCount ? open.TextDocument.GetText(open.TextDocument.GetLineByNumber(line)) : null;
			}

			try
			{
				Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
				Int32 current = 0;
				foreach (String text in File.ReadLines(file, fallback))
				{
					current++;
					if (current == line)
					{
						return text;
					}
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return null;
			}

			return null;
		}

		private String GetIndent()
		{
			EditorSettings editor = this._settings.Settings.Editor;
			return editor.TabsToSpaces ? new String(' ', editor.TabSize) : "\t";
		}
	}
}
