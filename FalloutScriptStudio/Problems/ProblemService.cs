namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The default <see cref="IProblemService"/>: an in-memory list with a size limit.</summary>
	public sealed class ProblemService : IProblemService
	{
		/// <summary>The number of problems after which the oldest ones are dropped (a mass compilation of a big project can report thousands).</summary>
		public const Int32 MaxProblems = 2000;

		private readonly Object _gate = new Object();
		private readonly List<Problem> _problems = new List<Problem>();

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public IReadOnlyList<Problem> Problems
		{
			get
			{
				lock (this._gate)
				{
					return this._problems.ToArray();
				}
			}
		}

		/// <inheritdoc />
		public void Report(Problem problem)
		{
			ArgumentNullException.ThrowIfNull(problem);
			this.ReportRange(new[] { problem });
		}

		/// <inheritdoc />
		public void ReportRange(IEnumerable<Problem> problems)
		{
			ArgumentNullException.ThrowIfNull(problems);
			lock (this._gate)
			{
				foreach (Problem problem in problems)
				{
					String identity = problem.Identity;
					Int32 index = this._problems.FindIndex(existing => ReferenceEquals(existing.Owner, problem.Owner) && String.Equals(existing.Identity, identity, StringComparison.Ordinal));
					if (index >= 0)
					{
						this._problems[index] = problem;
					}
					else
					{
						this._problems.Add(problem);
					}
				}

				if (this._problems.Count > MaxProblems)
				{
					this._problems.RemoveRange(0, this._problems.Count - MaxProblems);
				}
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc />
		public Int32 Remove(Func<Problem, Boolean> predicate)
		{
			ArgumentNullException.ThrowIfNull(predicate);
			Int32 removed;
			lock (this._gate)
			{
				removed = this._problems.RemoveAll(new Predicate<Problem>(predicate));
			}

			if (removed > 0)
			{
				this.Changed?.Invoke(this, EventArgs.Empty);
			}

			return removed;
		}

		/// <inheritdoc />
		public Int32 ClearOwner(Object owner, params ProblemSource[] sources)
		{
			ArgumentNullException.ThrowIfNull(owner);
			return this.Remove(problem => ReferenceEquals(problem.Owner, owner) && (sources.Length == 0 || sources.Contains(problem.Source)));
		}

		/// <inheritdoc />
		public Int32 ClearOrigin(String origin)
		{
			ArgumentException.ThrowIfNullOrEmpty(origin);
			return this.Remove(problem => String.Equals(problem.Origin, origin, StringComparison.Ordinal));
		}

		/// <inheritdoc />
		public void Clear()
		{
			this.Remove(static _ => true);
		}
	}
}
