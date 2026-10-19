namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;

	/// <summary>
	/// The verdict on registering a script file.
	/// </summary>
	/// <param name="Kind">Whether the script can be registered.</param>
	/// <param name="ScriptFileName">The compiled script name (<c>name.int</c>) to register; empty when refused.</param>
	/// <param name="Title">The title of the message for the user.</param>
	/// <param name="Message">The message for the user; empty when allowed.</param>
	public sealed record ScriptRegistrationCheck(ScriptRegistrationCheckKind Kind, String ScriptFileName, String Title, String Message);
}