namespace FalloutScriptStudio.Core.Building
{
	using System;

	/// <summary>Whether one script has to be built, and why.</summary>
	/// <param name="ScriptPath">The script.</param>
	/// <param name="OutputPath">The compiled file the script is built into, or <see langword="null"/> when it is not known.</param>
	/// <param name="Reason">Why the script is stale, or <see cref="StaleReason.UpToDate"/>.</param>
	/// <param name="Detail">The file the reason is about (the changed or unresolved include), if any.</param>
	public sealed record BuildDecision(String ScriptPath, String? OutputPath, StaleReason Reason, String? Detail = null)
	{
		/// <summary>Gets a value indicating whether the script can be skipped.</summary>
		public Boolean IsUpToDate => this.Reason == StaleReason.UpToDate;
	}
}