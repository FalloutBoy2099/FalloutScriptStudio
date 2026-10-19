namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;

	/// <summary>The files a script depends on: everything it includes, directly or through other files.</summary>
	/// <param name="Files">The full paths of the files that exist, in alphabetical order; the script itself is not part of it.</param>
	/// <param name="OptionalMissing">The paths of includes inside a conditional block (<c>#if</c>) that do not exist: the compiler may well skip them, but the script must be built again when one of them appears.</param>
	/// <param name="Unresolved">The includes that are not conditional and cannot be resolved (a missing file, a macro instead of a path, an unreadable file).</param>
	public sealed record DependencySet(IReadOnlyList<String> Files, IReadOnlyList<String> OptionalMissing, IReadOnlyList<UnresolvedInclude> Unresolved)
	{
		/// <summary>Gets a value indicating whether every dependency is known, so that the staleness of the script can be decided.</summary>
		public Boolean IsComplete => this.Unresolved.Count == 0;
	}
}