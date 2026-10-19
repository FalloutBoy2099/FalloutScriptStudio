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

		/// <summary>Gets a value indicating whether the scripts that are up to date are skipped (the staleness check needs <see cref="BuildCacheFile"/>).</summary>
		public Boolean OnlyChanged { get; init; }

		/// <summary>
		/// Gets the build cache file (<c>fss-build.json</c>), or <see langword="null"/> to neither check nor remember builds. When it is set,
		/// every successful compilation is remembered, so that a later run with <see cref="OnlyChanged"/> can skip the script.
		/// </summary>
		public String? BuildCacheFile { get; init; }
	}
}