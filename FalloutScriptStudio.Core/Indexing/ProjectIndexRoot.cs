namespace FalloutScriptStudio.Core.Indexing
{
	using System;

	/// <summary>A folder whose scripts and headers are indexed.</summary>
	/// <param name="Path">The full path of the folder.</param>
	/// <param name="Recursive">Whether the sub folders are indexed too (hidden folders, whose name starts with a dot, never are).</param>
	public sealed record ProjectIndexRoot(String Path, Boolean Recursive);
}