namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>The outcome of the staleness check of a set of scripts.</summary>
	public sealed class BuildPlan
	{
		/// <summary>Initializes a new instance of the <see cref="BuildPlan"/> class.</summary>
		/// <param name="decisions">One decision per script, in the order the scripts were asked for.</param>
		/// <param name="cacheStatus">How the build cache was found.</param>
		public BuildPlan(IReadOnlyList<BuildDecision> decisions, BuildCacheStatus cacheStatus = BuildCacheStatus.Loaded)
		{
			ArgumentNullException.ThrowIfNull(decisions);
			this.Decisions = decisions;
			this.CacheStatus = cacheStatus;
			this.Stale = decisions.Where(decision => !decision.IsUpToDate).ToList();
			this.UpToDate = decisions.Where(decision => decision.IsUpToDate).ToList();
		}

		/// <summary>Gets one decision per script, in the order the scripts were asked for.</summary>
		public IReadOnlyList<BuildDecision> Decisions { get; }

		/// <summary>Gets the decisions of the scripts that have to be built.</summary>
		public IReadOnlyList<BuildDecision> Stale { get; }

		/// <summary>Gets the decisions of the scripts that can be skipped.</summary>
		public IReadOnlyList<BuildDecision> UpToDate { get; }

		/// <summary>Gets how the build cache was found.</summary>
		public BuildCacheStatus CacheStatus { get; }
	}
}