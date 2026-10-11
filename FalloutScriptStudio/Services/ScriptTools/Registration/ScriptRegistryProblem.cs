namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;

	/// <summary>
	/// A condition found while locating the registration files.
	/// </summary>
	/// <param name="Kind">What is wrong.</param>
	/// <param name="IsError">Whether the registration cannot continue (otherwise it is only a warning).</param>
	/// <param name="Message">The text for the user.</param>
	public sealed record ScriptRegistryProblem(ScriptRegistryProblemKind Kind, Boolean IsError, String Message);
}
