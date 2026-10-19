namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The list of problems that are not part of the parse or build results of the active document: missing tools, files that could not
	/// be opened or saved, damaged settings... Everything that fails reports it here, and the Errors tab shows it.
	/// </summary>
	/// <remarks>Members are meant to be called on the UI thread (the Errors tab rebuilds itself when <see cref="Changed"/> is raised).</remarks>
	public interface IProblemService
	{
		/// <summary>Raised after the list changed.</summary>
		event EventHandler? Changed;

		/// <summary>Gets a snapshot of the problems in the order they were reported.</summary>
		IReadOnlyList<Problem> Problems { get; }

		/// <summary>Adds a problem; an equal problem that is already listed is replaced, not listed twice.</summary>
		/// <param name="problem">The problem.</param>
		void Report(Problem problem);

		/// <summary>Adds several problems at once (the list is rebuilt once, not once per problem).</summary>
		/// <param name="problems">The problems.</param>
		void ReportRange(IEnumerable<Problem> problems);

		/// <summary>Removes the problems that match a condition.</summary>
		/// <param name="predicate">The condition.</param>
		/// <returns>The number of problems removed.</returns>
		Int32 Remove(Func<Problem, Boolean> predicate);

		/// <summary>Removes the problems of one owner (a document), optionally only those of some sources.</summary>
		/// <param name="owner">The owner.</param>
		/// <param name="sources">The sources to remove; all when none is given.</param>
		/// <returns>The number of problems removed.</returns>
		Int32 ClearOwner(Object owner, params ProblemSource[] sources);

		/// <summary>Removes the problems made by one producer.</summary>
		/// <param name="origin">The name of the producer (see <see cref="ProblemOrigins"/>).</param>
		/// <returns>The number of problems removed.</returns>
		Int32 ClearOrigin(String origin);

		/// <summary>Removes every problem.</summary>
		void Clear();
	}
}