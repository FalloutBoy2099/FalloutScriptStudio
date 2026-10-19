namespace FalloutScriptStudio.Core.Building
{
	/// <summary>Why a script has to be built again, or <see cref="UpToDate"/> when it does not.</summary>
	public enum StaleReason
	{
		/// <summary>The compiled script exists, was built from the current sources with the current options and tools: nothing to do.</summary>
		UpToDate = 0,

		/// <summary>The user asked to build everything (Rebuild All).</summary>
		Forced = 1,

		/// <summary>The compilation is done by the user batch file, whose result cannot be examined.</summary>
		NotCacheable = 2,

		/// <summary>The folder for compiled scripts is not set, so the output file is unknown.</summary>
		OutputFolderNotSet = 3,

		/// <summary>The script does not exist (the compiler will say so).</summary>
		SourceMissing = 4,

		/// <summary>There is no compiled script, or it was deleted.</summary>
		OutputMissing = 5,

		/// <summary>The build cache knows nothing about this script (never built, built by something else, or the cache was lost).</summary>
		NotRecorded = 6,

		/// <summary>The compiler options (optimization, short-circuit, define, ...) differ from the ones the script was built with.</summary>
		OptionsChanged = 7,

		/// <summary>The compiler or the preprocessor was replaced.</summary>
		CompilerChanged = 8,

		/// <summary>The compiled script is not the file the last build produced (it was rebuilt by something else or replaced).</summary>
		OutputChanged = 9,

		/// <summary>The script itself changed.</summary>
		SourceChanged = 10,

		/// <summary>A file the script includes changed (<see cref="BuildDecision.Detail"/> names it).</summary>
		DependencyChanged = 11,

		/// <summary>The set of files the script includes is not the one it was built with (an include was added, removed, or now resolves to another file).</summary>
		DependenciesChanged = 12,

		/// <summary>An include cannot be resolved, so the dependencies are not known (<see cref="BuildDecision.Detail"/> names it).</summary>
		UnresolvedInclude = 13,

		/// <summary>The script has unsaved changes in the editor.</summary>
		UnsavedChanges = 14,
	}
}