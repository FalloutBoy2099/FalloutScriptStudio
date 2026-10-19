namespace FalloutScriptStudio.Core.Functions
{
	using System;

	/// <summary>
	/// Where the functions ini files live.
	/// </summary>
	/// <param name="BuiltInFile">Full path of the shipped <c>functions.ini</c> (<c>descriptions\functions.ini</c>).</param>
	/// <param name="UserFile">Full path of the user's <c>userfunctions.ini</c> (created from a template when missing).</param>
	public sealed record FunctionCatalogPaths(String BuiltInFile, String UserFile);
}