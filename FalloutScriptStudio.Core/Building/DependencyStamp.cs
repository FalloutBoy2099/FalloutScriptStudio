namespace FalloutScriptStudio.Core.Building
{
	using System;

	/// <summary>A file a script depended on when it was compiled, with the stamp the file had then.</summary>
	/// <param name="Path">The full path of the file.</param>
	/// <param name="Stamp">The stamp when the compilation started; <see cref="FileStamp.Missing"/> for an optional include that did not exist.</param>
	public readonly record struct DependencyStamp(String Path, FileStamp Stamp);
}