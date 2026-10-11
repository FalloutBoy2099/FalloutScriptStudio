namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;

	/// <summary>
	/// The files edited by the script registration.
	/// </summary>
	/// <param name="ListPath">The <c>scripts.lst</c> file (always present).</param>
	/// <param name="NameMessagePath">The <c>scrname.msg</c> file, or <see langword="null"/> if it does not exist.</param>
	/// <param name="HeaderPath">The <c>scripts.h</c> file, or <see langword="null"/> if it is not configured or does not exist.</param>
	public sealed record ScriptRegistryPaths(String ListPath, String? NameMessagePath, String? HeaderPath);
}
