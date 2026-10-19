namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Folding;

	/// <summary>
	/// Folding of procedures, the declaration region, <c>variable begin ... end</c> and <c>#if</c> blocks
	/// (legacy <c>CodeFolder</c>), the persistence of the collapsed state and the "outline" commands.
	/// </summary>
	public sealed class FoldingController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly FoldingGenerator _generator = new();
		private FoldingManager? _manager;
		private FoldingRedrawGate? _redrawGate;
		private Boolean _stateRestored;

		public FoldingController(EditorContext context)
		{
			this._context = context;
		}

		/// <summary>Gets a value indicating whether folding is available (scripts and headers only).</summary>
		public Boolean IsEnabled => this._manager is not null;

		/// <summary>Gets the sections of the procedure bodies in document order.</summary>
		public IReadOnlyList<FoldingSection> ProcedureSections => this._manager is null
			? Array.Empty<FoldingSection>()
			: GetProcedureSections(this._manager).OrderBy(s => s.StartOffset).ToList();

		/// <summary>Gets the number of folding sections.</summary>
		public Int32 Count => this._manager?.AllFoldings.Count() ?? 0;

		public void Attach()
		{
			this.Attach(null);
		}

		/// <summary>Installs the folding and, for the second pane of a split view, starts with the folded sections of the first pane.</summary>
		/// <param name="mirror">The controller of the first pane, or <see langword="null"/>.</param>
		public void Attach(FoldingController? mirror)
		{
			if (!this._context.IsScript)
			{
				return;
			}

			this._manager = FoldingManager.Install(this._context.TextArea);
			this._redrawGate = FoldingRedrawGate.TryCreate(this._manager, this._context.Document);
			if (mirror is not null)
			{
				// The state of the first pane is the one that counts; this pane never restores or stores it.
				this._stateRestored = true;
			}

			this.Update();
			if (mirror?._manager is FoldingManager source)
			{
				HashSet<String> folded = new(source.AllFoldings.Where(s => s.IsFolded).Select(s => s.Tag as String).OfType<String>(), StringComparer.Ordinal);
				foreach (FoldingSection section in this._manager.AllFoldings)
				{
					section.IsFolded = section.Tag is String key && folded.Contains(key);
				}
			}
		}

		/// <summary>Detaches the folding manager; the state must have been captured before.</summary>
		public void Dispose()
		{
			this._redrawGate?.Dispose();
			this._redrawGate = null;
			if (this._manager is not null)
			{
				FoldingManager.Uninstall(this._manager);
				this._manager = null;
			}
		}

		/// <summary>Recomputes the foldings from the current parse result (call after every parse).</summary>
		public void Update()
		{
			FoldingManager? manager = this._manager;
			if (manager is null)
			{
				return;
			}

			TextDocument document = this._context.Document;
			ProgramInfo? info = this._context.ProgramInfo;
			IReadOnlyList<FoldingRegion> regions = this._generator.Generate(
				document.Text,
				this._context.DocumentModel.FileName,
				info?.Procedures ?? Array.Empty<ScriptProcedure>());

			List<(NewFolding Folding, String Key)> foldings = [];
			Dictionary<String, Int32> occurrences = new(StringComparer.Ordinal);
			foreach (FoldingRegion region in regions)
			{
				Int32 startLine = Math.Clamp(region.StartLine, 1, document.LineCount);
				Int32 endLine = Math.Clamp(region.EndLine, 1, document.LineCount);
				Int32 start = document.GetLineByNumber(startLine).Offset;
				Int32 end = document.GetLineByNumber(endLine).EndOffset;
				if (end <= start)
				{
					continue;
				}

				String baseKey = (Int32)region.Kind + ":" + region.Title.Trim().ToUpperInvariant();
				occurrences.TryGetValue(baseKey, out Int32 count);
				occurrences[baseKey] = count + 1;
				foldings.Add((new NewFolding(start, end) { Name = region.Title, DefaultClosed = false }, baseKey + "#" + count));
			}

			foldings.Sort((a, b) => a.Folding.StartOffset.CompareTo(b.Folding.StartOffset));
			manager.UpdateFoldings(foldings.Select(f => f.Folding), -1);

			Dictionary<Int32, String> keys = [];
			foreach ((NewFolding folding, String key) in foldings)
			{
				keys[folding.StartOffset] = key;
			}

			foreach (FoldingSection section in manager.AllFoldings)
			{
				section.Tag = keys.TryGetValue(section.StartOffset, out String? key) ? key : null;
			}

			if (!this._stateRestored && (info is not null || this._context.ViewState.FoldedRegions is not null))
			{
				this.RestoreState(manager);
				this._stateRestored = info is not null;
			}
		}

		/// <summary>Captures the folded sections into the view state and into the collapsed-procedure store.</summary>
		public void CaptureState()
		{
			FoldingManager? manager = this._manager;
			if (manager is null || !this._stateRestored && this._context.ProgramInfo is null)
			{
				return;
			}

			HashSet<String> folded = new(StringComparer.Ordinal);
			foreach (FoldingSection section in manager.AllFoldings)
			{
				if (section.IsFolded && section.Tag is String key)
				{
					folded.Add(key);
				}
			}

			this._context.ViewState.FoldedRegions = folded;
			this.RememberCollapsedProcedures(manager);
		}

		/// <summary>Collapses every procedure except the one that contains the caret, or expands all when none is expanded.</summary>
		public void ToggleOutline()
		{
			FoldingManager? manager = this._manager;
			if (manager is null)
			{
				return;
			}

			List<FoldingSection> procedures = GetProcedureSections(manager).ToList();
			Int32 caret = this._context.TextArea.Caret.Offset;
			List<FoldingSection> others = procedures.Where(s => caret < s.StartOffset || caret > s.EndOffset).ToList();
			Boolean collapse = others.Any(s => !s.IsFolded);
			foreach (FoldingSection section in others)
			{
				section.IsFolded = collapse;
			}

			this._context.TextArea.Caret.BringCaretToView();
		}

		/// <summary>
		/// Collapses every procedure that does not contain the caret and expands the one that does (legacy "only procedure" button);
		/// the declaration region and the other sections are left as they are.
		/// </summary>
		public void ShowOnlyCurrentProcedure()
		{
			if (this._manager is null)
			{
				return;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			foreach (FoldingSection section in GetProcedureSections(this._manager))
			{
				section.IsFolded = caret < section.StartOffset || caret > section.EndOffset;
			}

			this._context.TextArea.Caret.BringCaretToView();
		}

		/// <summary>Collapses all procedures that do not contain the caret.</summary>
		public void CollapseAll()
		{
			this.SetAll(true);
		}

		/// <summary>Expands all sections.</summary>
		public void ExpandAll()
		{
			this.SetAll(false);
		}

		/// <summary>Expands the sections that hide an offset so that it becomes visible.</summary>
		/// <param name="offset">The offset to reveal.</param>
		public void Reveal(Int32 offset)
		{
			if (this._manager is null)
			{
				return;
			}

			foreach (FoldingSection section in this._manager.GetFoldingsContaining(offset))
			{
				if (section.IsFolded)
				{
					section.IsFolded = false;
				}
			}
		}

		/// <summary>Determines whether a folding section starts on a line.</summary>
		/// <param name="line">The 1-based line.</param>
		/// <returns><see langword="true"/> when a section (a procedure, for example) starts there.</returns>
		public Boolean IsFoldStart(Int32 line)
		{
			if (this._manager is null)
			{
				return false;
			}

			TextDocument document = this._context.Document;
			return this._manager.AllFoldings.Any(section => section.StartOffset <= document.TextLength
				&& document.GetLineByOffset(section.StartOffset).LineNumber == line);
		}

		/// <summary>Gets the 1-based line of the first line of the declaration region, or 1.</summary>
		/// <returns>The line.</returns>
		public Int32 GetDeclarationRegionLine()
		{
			if (this._manager is null)
			{
				return 1;
			}

			foreach (FoldingSection section in this._manager.AllFoldings)
			{
				if (section.Tag is String key && key.StartsWith(((Int32)FoldingKind.Region).ToString(CultureInfo.InvariantCulture) + ":", StringComparison.Ordinal))
				{
					return this._context.Document.GetLocation(section.StartOffset).Line;
				}
			}

			return 1;
		}

		private static IEnumerable<FoldingSection> GetProcedureSections(FoldingManager manager)
		{
			String prefix = ((Int32)FoldingKind.Procedure).ToString(CultureInfo.InvariantCulture) + ":";
			return manager.AllFoldings.Where(s => s.Tag is String key && key.StartsWith(prefix, StringComparison.Ordinal));
		}

		private void SetAll(Boolean folded)
		{
			if (this._manager is null)
			{
				return;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			foreach (FoldingSection section in this._manager.AllFoldings)
			{
				Boolean containsCaret = caret >= section.StartOffset && caret <= section.EndOffset;
				section.IsFolded = folded && !containsCaret;
			}
		}

		private void RestoreState(FoldingManager manager)
		{
			ISet<String>? saved = this._context.ViewState.FoldedRegions;
			if (saved is not null)
			{
				foreach (FoldingSection section in manager.AllFoldings)
				{
					section.IsFolded = section.Tag is String key && saved.Contains(key);
				}

				return;
			}

			if (this._context.DocumentModel.FilePath is null)
			{
				return;
			}

			ICollapsedProcedureStore store = this._context.Services.CollapsedProcedures;
			String script = this._context.ScriptName;
			foreach (FoldingSection section in GetProcedureSections(manager))
			{
				if (store.IsCollapsed(script, (section.Title ?? String.Empty).Trim()))
				{
					section.IsFolded = true;
				}
			}
		}

		private void RememberCollapsedProcedures(FoldingManager manager)
		{
			if (this._context.DocumentModel.FilePath is null || this._context.DocumentModel.IsModified)
			{
				return;
			}

			ICollapsedProcedureStore store = this._context.Services.CollapsedProcedures;
			String script = this._context.ScriptName;
			foreach (FoldingSection section in GetProcedureSections(manager))
			{
				store.SetCollapsed(script, (section.Title ?? String.Empty).Trim(), section.IsFolded);
			}
		}
	}
}