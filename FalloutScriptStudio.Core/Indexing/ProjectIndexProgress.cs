namespace FalloutScriptStudio.Core.Indexing
{
	using System;

	/// <summary>How far the project index is.</summary>
	/// <param name="Phase">The step.</param>
	/// <param name="Done">The number of files indexed so far in this run.</param>
	/// <param name="Total">The number of files to index in this run (the ones that are new or changed).</param>
	/// <param name="Symbols">The number of symbols of the index.</param>
	public sealed record ProjectIndexProgress(ProjectIndexPhase Phase, Int32 Done, Int32 Total, Int32 Symbols)
	{
		/// <summary>Gets the progress of an index that does nothing.</summary>
		public static ProjectIndexProgress Idle { get; } = new ProjectIndexProgress(ProjectIndexPhase.Idle, 0, 0, 0);

		/// <summary>Gets a value indicating whether a run is in progress.</summary>
		public Boolean IsBusy => this.Phase != ProjectIndexPhase.Idle;
	}
}