namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Parsing;
	using TextTools;

	/// <summary>
	/// Default <see cref="IRefactoringService"/>.
	/// </summary>
	/// <remarks>
	/// Differences from the legacy code: references are matched against the file they belong to (the legacy code compared only
	/// line numbers, so a reference of another file could rename the wrong word), the <c>#define</c> alignment is applied only
	/// in the file that holds the definition and cannot loop forever, and a case-only rename of a procedure or variable is allowed.
	/// </remarks>
	public sealed class RefactoringService : IRefactoringService
	{
		private static readonly String[] _reservedWords = { "procedure", "begin", "end", "variable" };

		/// <inheritdoc />
		public RenameValidation Validate(RenameRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			String name = request.NewName.Trim();
			IScriptSymbol symbol = request.Symbol;
			if (name.Length == 0)
			{
				return new RenameValidation(RenameError.EmptyName, name, "The new name is empty.");
			}

			if (!ProcedureNameParser.IsIdentifier(name) || Char.IsDigit(name[0]) || Array.Exists(_reservedWords, w => String.Equals(w, name, StringComparison.OrdinalIgnoreCase)))
			{
				return new RenameValidation(RenameError.InvalidName, name, "The name can contain only letters, digits and underscores and cannot start with a digit.");
			}

			if (String.Equals(name, symbol.Name, StringComparison.Ordinal))
			{
				return new RenameValidation(RenameError.SameName, name, "The new name is the same as the old one.");
			}

			Boolean caseOnly = symbol.Kind != SymbolKind.Macro && String.Equals(name, symbol.Name, StringComparison.OrdinalIgnoreCase);
			if (!caseOnly && NameExists(request.Info, symbol, name))
			{
				return new RenameValidation(RenameError.NameExists, name, "A procedure, variable or declared macro with this name already exists.");
			}

			if (symbol.Kind == SymbolKind.None)
			{
				return new RenameValidation(RenameError.UnsupportedSymbol, name, "This kind of name cannot be renamed.");
			}

			return new RenameValidation(RenameError.None, name, null);
		}

		/// <inheritdoc />
		public RenameResult Rename(RenameRequest request)
		{
			ArgumentNullException.ThrowIfNull(request);

			RenameValidation validation = this.Validate(request);
			if (!validation.IsValid)
			{
				return RenameResult.Rejected(validation);
			}

			return request.Symbol switch
			{
				ScriptVariable variable => RenameVariable(request, validation, variable),
				ScriptProcedure procedure => RenameProcedure(request, validation, procedure),
				ScriptMacro macro => this.RenameMacro(request, validation, macro),
				_ => RenameResult.Rejected(new RenameValidation(RenameError.UnsupportedSymbol, validation.NewName, "This kind of name cannot be renamed.")),
			};
		}

		/// <inheritdoc />
		public MacroRenamePreview PreviewMacroRename(ScriptMacro macro, String newName, IEnumerable<RefactorDocument> documents)
		{
			ArgumentNullException.ThrowIfNull(macro);
			ArgumentNullException.ThrowIfNull(newName);
			ArgumentNullException.ThrowIfNull(documents);

			Regex regex = CreateWordRegex(macro.Token, RegexOptions.None);
			var found = new List<RefactorDocument>();
			var matches = new List<PreviewMatch>();
			foreach (RefactorDocument document in documents)
			{
				MatchCollection collection = regex.Matches(document.Text);
				if (collection.Count == 0)
				{
					continue;
				}

				var map = new TextLineMap(document.Text);
				found.Add(document);
				foreach (Match match in collection)
				{
					Int32 line = map.GetLineFromOffset(match.Index);
					String preview = document.Text.Substring(match.Index, map.GetLineEnd(line) - match.Index).TrimEnd();
					matches.Add(new PreviewMatch(document.Path, document.IsOpen, new TextSpan(match.Index, match.Length), line, preview));
				}
			}

			return new MacroRenamePreview(macro, newName, found, matches);
		}

		/// <inheritdoc />
		public RenameResult ApplyMacroRename(MacroRenamePreview preview, IEnumerable<PreviewMatch> selected)
		{
			ArgumentNullException.ThrowIfNull(preview);
			ArgumentNullException.ThrowIfNull(selected);

			HashSet<PreviewMatch> allowed = preview.Matches.ToHashSet();
			List<PreviewMatch> chosen = selected.Where(allowed.Contains).Distinct().ToList();
			Int32 diff = preview.NewName.Length - preview.Macro.Token.Length;

			var files = new List<FileEdits>();
			foreach (RefactorDocument document in preview.Documents)
			{
				List<PreviewMatch> inFile = chosen.Where(m => PathEquals(m.FilePath, document.Path)).OrderBy(m => m.Span.Offset).ToList();
				if (inFile.Count == 0)
				{
					continue;
				}

				var edits = inFile.Select(m => new TextEdit(m.Span.Offset, m.Span.Length, preview.NewName)).ToList();
				if (diff != 0 && PathEquals(document.Path, preview.Macro.DeclaredFile) && inFile.Any(m => m.Line == preview.Macro.DeclaredLine))
				{
					TextEdit? adjust = MacroDefineAdjuster.CreateEdit(document.Text, preview.Macro.Token, preview.Macro.DeclaredLine, diff);
					if (adjust is not null)
					{
						edits.Add(adjust);
					}
				}

				files.Add(new FileEdits(document.Path, TextEditApplier.Normalize(edits), document.IsOpen));
			}

			return new RenameResult(new RenameValidation(RenameError.None, preview.NewName, null), files);
		}

		private static Boolean PathEquals(String? a, String? b)
		{
			return String.Equals(a ?? String.Empty, b ?? String.Empty, StringComparison.OrdinalIgnoreCase);
		}

		private static Regex CreateWordRegex(String word, RegexOptions options)
		{
			return new Regex(@"\b" + Regex.Escape(word) + @"\b", options | RegexOptions.CultureInvariant);
		}

		private static RenameResult RenameVariable(RenameRequest request, RenameValidation validation, ScriptVariable variable)
		{
			Regex regex = CreateWordRegex(variable.Name, RegexOptions.IgnoreCase);
			Int32 declaredLine = variable.IsArgument ? variable.ArgumentDeclaredLine : variable.DeclaredLine;
			return RenameVariableByReferences(request, validation, regex, variable.References, variable.DeclaredFile, declaredLine);
		}

		private static RenameResult RenameProcedure(RenameRequest request, RenameValidation validation, ScriptProcedure procedure)
		{
			Boolean hasReferences = procedure.References.Count > 0;
			Regex byReference = CreateWordRegex(procedure.Name, RegexOptions.IgnoreCase | RegexOptions.Multiline);
			Regex byPattern = new Regex(
				@"(\bprocedure\s|\bcall\s|[=,(]\s*)\s*" + Regex.Escape(procedure.Name) + @"\b",
				RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

			var files = new List<FileEdits>();
			var warnings = new List<String>();
			var unresolved = new List<SourceReference>(hasReferences ? procedure.References : Array.Empty<SourceReference>());
			Boolean definingSeen = false;

			foreach (RefactorDocument document in request.Documents)
			{
				Boolean isCurrent = PathEquals(document.Path, request.CurrentFile);
				Boolean isDefining = !isCurrent && procedure.StartFile.Length > 0 && PathEquals(document.Path, procedure.StartFile);
				definingSeen |= isDefining;

				IReadOnlyList<TextEdit> edits;
				if (isDefining && request.RenameInDefiningFile)
				{
					edits = TextReplacer.CreateEdits(hasReferences ? byReference : byPattern, document.Text, validation.NewName, !hasReferences);
					RemoveReferencesOf(unresolved, document.Path);
				}
				else if (hasReferences)
				{
					List<SourceReference> mine = procedure.References.Where(r => PathEquals(r.File, document.Path)).ToList();
					Int32 declared = PathEquals(procedure.DeclaredFile, document.Path) ? procedure.DeclaredLine : -1;
					Int32 defined = PathEquals(procedure.StartFile, document.Path) ? procedure.DefinedLine : -1;
					edits = MatchByReferences(document, byReference, mine, declared, defined, false, validation.NewName, out List<SourceReference> left);
					RemoveReferencesOf(unresolved, document.Path);
					unresolved.AddRange(left);
				}
				else if (isCurrent)
				{
					edits = TextReplacer.CreateEdits(byPattern, document.Text, validation.NewName, true);
				}
				else
				{
					continue;
				}

				if (edits.Count > 0)
				{
					files.Add(new FileEdits(document.Path, TextEditApplier.Normalize(edits), document.IsOpen));
				}
			}

			if (request.RenameInDefiningFile && procedure.StartFile.Length > 0 && !PathEquals(procedure.StartFile, request.CurrentFile) && !definingSeen)
			{
				warnings.Add("The file that defines the procedure was not supplied: " + procedure.StartFile);
			}

			return new RenameResult(validation, files) { UnresolvedReferences = unresolved, Warnings = warnings };
		}

		private static RenameResult RenameVariableByReferences(
			RenameRequest request,
			RenameValidation validation,
			Regex regex,
			IReadOnlyList<SourceReference> references,
			String? declaredFile,
			Int32 declaredLine)
		{
			var files = new List<FileEdits>();
			var unresolved = new List<SourceReference>(references);
			foreach (RefactorDocument document in request.Documents)
			{
				List<SourceReference> mine = references.Where(r => PathEquals(r.File, document.Path)).ToList();
				Int32 declared = PathEquals(declaredFile, document.Path) ? declaredLine : -1;
				if (mine.Count == 0 && declared < 1)
				{
					continue;
				}

				List<TextEdit> edits = MatchByReferences(document, regex, mine, declared, -1, true, validation.NewName, out List<SourceReference> left);
				RemoveReferencesOf(unresolved, document.Path);
				unresolved.AddRange(left);
				if (edits.Count > 0)
				{
					files.Add(new FileEdits(document.Path, TextEditApplier.Normalize(edits), document.IsOpen));
				}
			}

			return new RenameResult(validation, files) { UnresolvedReferences = unresolved };
		}

		private static void RemoveReferencesOf(List<SourceReference> references, String path)
		{
			references.RemoveAll(r => PathEquals(r.File, path));
		}

		/// <summary>
		/// Picks the matches of a word that are the declaration, the definition or a recorded reference
		/// (legacy <c>Utilities.ReplaceByReferences</c>): the first match on the declaration line, the first on the definition
		/// line, and one match per recorded reference line.
		/// </summary>
		private static List<TextEdit> MatchByReferences(
			RefactorDocument document,
			Regex regex,
			List<SourceReference> references,
			Int32 declaredLine,
			Int32 definedLine,
			Boolean isVariable,
			String newName,
			out List<SourceReference> unresolved)
		{
			var map = new TextLineMap(document.Text);
			var edits = new List<TextEdit>();
			List<SourceReference> remaining = references.ToList();
			Boolean declarationDone = declaredLine < 1;
			Boolean definitionDone = isVariable || definedLine < 1;

			foreach (Match match in regex.Matches(document.Text))
			{
				Int32 line = map.GetLineFromOffset(match.Index);
				if (!declarationDone && declaredLine == line)
				{
					edits.Add(TextReplacer.CreateEdit(match, newName, false));
					declarationDone = true;
				}
				else if (!definitionDone && definedLine == line)
				{
					edits.Add(TextReplacer.CreateEdit(match, newName, false));
					definitionDone = true;
				}
				else
				{
					Int32 index = remaining.FindIndex(r => r.Line == line);
					if (index >= 0)
					{
						edits.Add(TextReplacer.CreateEdit(match, newName, false));
						remaining.RemoveAt(index);
					}
				}
			}

			unresolved = remaining;
			return edits;
		}

		private RenameResult RenameMacro(RenameRequest request, RenameValidation validation, ScriptMacro macro)
		{
			MacroRenamePreview preview = this.PreviewMacroRename(macro, validation.NewName, request.Documents);
			RenameResult result = this.ApplyMacroRename(preview, preview.Matches);
			return result with { Validation = validation };
		}

		private static Boolean NameExists(ProgramInfo info, IScriptSymbol symbol, String name)
		{
			switch (symbol)
			{
				case ScriptVariable variable when variable.Kind == SymbolKind.LocalVariable:
					return info.NameExists(name, SymbolKind.LocalVariable, variable.DeclaredFile, variable.DeclaredLine);
				default:
					return info.NameExists(name, symbol.Kind);
			}
		}
	}
}