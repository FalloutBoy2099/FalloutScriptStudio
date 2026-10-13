namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;

	/// <summary>What the project index covers: folders and single files.</summary>
	public sealed record ProjectIndexRoots
	{
		/// <summary>Gets an empty set of roots.</summary>
		public static ProjectIndexRoots None { get; } = new ProjectIndexRoots();

		/// <summary>Gets the folders.</summary>
		public IReadOnlyList<ProjectIndexRoot> Folders { get; init; } = Array.Empty<ProjectIndexRoot>();

		/// <summary>Gets single files that are indexed whatever their folder (the open scripts).</summary>
		public IReadOnlyList<String> Files { get; init; } = Array.Empty<String>();

		/// <summary>Gets a value indicating whether there is nothing to index.</summary>
		public Boolean IsEmpty => this.Folders.Count == 0 && this.Files.Count == 0;
	}
}
