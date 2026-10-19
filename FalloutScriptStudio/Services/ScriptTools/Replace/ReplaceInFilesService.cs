namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Documents;
	using Localization;
	using Problems;
	using Search;

	/// <inheritdoc />
	public sealed class ReplaceInFilesService : IReplaceInFilesService
	{
		private readonly ReplaceInFilesPlanner _planner;
		private readonly ReplaceInFilesApplier _applier;
		private readonly IReplacePreviewPresenter _presenter;
		private readonly IWorkspace _workspace;
		private readonly IProblemService _problems;
		private ReplaceUndoRecord? _undo;

		/// <summary>Initializes a new instance of the <see cref="ReplaceInFilesService"/> class.</summary>
		/// <param name="planner">Finds the replacements.</param>
		/// <param name="applier">Makes them.</param>
		/// <param name="presenter">Shows the preview.</param>
		/// <param name="workspace">The status line.</param>
		/// <param name="problems">Receives the files that were left unchanged.</param>
		public ReplaceInFilesService(ReplaceInFilesPlanner planner, ReplaceInFilesApplier applier, IReplacePreviewPresenter presenter, IWorkspace workspace, IProblemService? problems = null)
		{
			ArgumentNullException.ThrowIfNull(planner);
			ArgumentNullException.ThrowIfNull(applier);
			ArgumentNullException.ThrowIfNull(presenter);
			ArgumentNullException.ThrowIfNull(workspace);
			this._planner = planner;
			this._applier = applier;
			this._presenter = presenter;
			this._workspace = workspace;
			this._problems = problems ?? new ProblemService();
		}

		/// <inheritdoc />
		public event EventHandler? UndoStateChanged;

		/// <inheritdoc />
		public Boolean CanUndo => this._undo is not null;

		/// <inheritdoc />
		public async Task<ReplaceOutcome> RunAsync(ReplaceInFilesRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			this._problems.ClearOrigin(ProblemOrigins.ReplaceInFiles);
			ReplacePlan plan = await this._planner.PlanAsync(request, progress, cancellationToken);
			this.ReportSkipped(plan);
			if (plan.Files.Count == 0)
			{
				return new ReplaceOutcome(ReplaceOutcomeKind.NothingFound, plan, null);
			}

			IReadOnlyList<ReplaceSelection>? selection = await this._presenter.ShowAsync(plan, request);
			if (selection is null || selection.Count == 0)
			{
				return new ReplaceOutcome(ReplaceOutcomeKind.Cancelled, plan, null);
			}

			ReplaceResult result = await this._applier.ApplyAsync(plan, selection, null, cancellationToken);
			this._problems.ReportRange(result.Failures.Select(failure => ProblemFactory.ReplaceFileSkipped(failure.Path, failure.Reason, failure.Matches, failure.Detail)));
			if (result.Undo is not null)
			{
				this._undo = result.Undo;
				this.UndoStateChanged?.Invoke(this, EventArgs.Empty);
			}

			this._workspace.ReportStatus(Describe(result));
			return new ReplaceOutcome(ReplaceOutcomeKind.Applied, plan, result);
		}

		/// <inheritdoc />
		public async Task<ReplaceUndoResult?> UndoLastAsync(CancellationToken cancellationToken)
		{
			ReplaceUndoRecord? record = this._undo;
			if (record is null)
			{
				return null;
			}

			ReplaceUndoResult result = await this._applier.UndoAsync(record, cancellationToken);
			this._undo = null;
			this.UndoStateChanged?.Invoke(this, EventArgs.Empty);
			this._problems.ClearOrigin(ProblemOrigins.ReplaceInFiles);
			this._problems.ReportRange(result.Failures.Select(failure => ProblemFactory.ReplaceFileSkipped(failure.Path, failure.Reason, failure.Matches, failure.Detail)));
			this._workspace.ReportStatus(result.Failures.Count == 0
				? Loc.Format("Str.ScriptTools.ReplUndone", result.Restored)
				: Loc.Format("Str.ScriptTools.ReplUndonePartial", result.Restored, result.Failures.Count));
			return result;
		}

		private static String Describe(ReplaceResult result)
		{
			if (result.Replacements == 0 && result.Failures.Count == 0)
			{
				return Loc.Get("Str.ScriptTools.ReplNothingReplaced");
			}

			String text = Loc.Format("Str.ScriptTools.ReplDone", result.Replacements, result.FilesChanged);
			if (result.Failures.Count > 0)
			{
				text += " " + Loc.Format("Str.ScriptTools.ReplDoneFailed", result.Failures.Count);
			}

			return result.Cancelled ? text + " " + Loc.Get("Str.ScriptTools.ReplDoneCancelled") : text;
		}

		private void ReportSkipped(ReplacePlan plan)
		{
			this._problems.ReportRange(plan.Skipped.Select(skip => ProblemFactory.ReplaceFileSkipped(
				skip.Path,
				skip.Reason == ReplaceSkipReason.ReadOnly ? ReplaceFailureReason.ReadOnly : ReplaceFailureReason.Unreadable,
				skip.Matches,
				String.Empty)));
		}
	}
}