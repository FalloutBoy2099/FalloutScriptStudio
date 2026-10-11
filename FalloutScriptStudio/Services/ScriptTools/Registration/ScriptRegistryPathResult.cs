namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System.Collections.Generic;

	/// <summary>
	/// The outcome of locating the registration files.
	/// </summary>
	/// <param name="Paths">The files, or <see langword="null"/> when the registration cannot continue.</param>
	/// <param name="Problems">The errors and warnings found.</param>
	public sealed record ScriptRegistryPathResult(ScriptRegistryPaths? Paths, IReadOnlyList<ScriptRegistryProblem> Problems);
}
