namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// Controls how a mass compilation runs.
	/// </summary>
	public sealed record BatchCompileOptions
	{
		/// <summary>Gets a value indicating whether several scripts are compiled at the same time.</summary>
		public Boolean MultiThreaded { get; init; } = true;

		/// <summary>Gets the maximum number of parallel compilations; the processor count when <see langword="null"/>.</summary>
		public Int32? MaxDegreeOfParallelism { get; init; }
	}
}
