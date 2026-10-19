namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Search;

	/// <summary>
	/// "Replace in Files": finds the replacements, shows them in a preview, applies the ones the user ticked, reports the files that could not
	/// be changed as problems, and keeps what the last run changed so that it can be undone.
	/// </summary>
	public interface IReplaceInFilesService
	{
		/// <summary>Raised when <see cref="CanUndo"/> changed.</summary>
		event EventHandler? UndoStateChanged;

		/// <summary>Gets a value indicating whether the last run can be undone.</summary>
		Boolean CanUndo { get; }

		/// <summary>Runs the three steps: the preview, the choice of the user, the replacement.</summary>
		/// <param name="request">What to replace.</param>
		/// <param name="progress">Receives the progress of the search; may be <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the search and, later, the replacement between two files.</param>
		/// <returns>How the run ended.</returns>
		/// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The regular expression took too long.</exception>
		Task<ReplaceOutcome> RunAsync(ReplaceInFilesRequest request, IProgress<SearchProgress>? progress, CancellationToken cancellationToken);

		/// <summary>Puts back what the last run replaced, in every file that was not changed since.</summary>
		/// <param name="cancellationToken">Cancels between two files.</param>
		/// <returns>The outcome, or <see langword="null"/> when there was nothing to undo.</returns>
		Task<ReplaceUndoResult?> UndoLastAsync(CancellationToken cancellationToken);
	}
}