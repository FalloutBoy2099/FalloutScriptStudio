namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Outcome of a rename: the edits to apply to each file.
	/// </summary>
	/// <param name="Validation">The check of the new name; when it is not valid there are no edits.</param>
	/// <param name="Files">The edits per file; only files that change are listed.</param>
	public sealed record RenameResult(RenameValidation Validation, IReadOnlyList<FileEdits> Files)
	{
		/// <summary>Gets the references the rename could not reach (for example because their file was not supplied); legacy "not all references were renamed".</summary>
		public IReadOnlyList<SourceReference> UnresolvedReferences { get; init; } = Array.Empty<SourceReference>();

		/// <summary>Gets notes for the user, for example that the defining file of a procedure was not supplied.</summary>
		public IReadOnlyList<String> Warnings { get; init; } = Array.Empty<String>();

		/// <summary>Gets a value indicating whether the name was accepted.</summary>
		public Boolean Succeeded => this.Validation.IsValid;

		/// <summary>Gets the total number of edits.</summary>
		public Int32 EditCount => this.Files.Sum(f => f.Edits.Count);

		/// <summary>Creates a result for a rejected name.</summary>
		/// <param name="validation">The failed check.</param>
		/// <returns>The result.</returns>
		public static RenameResult Rejected(RenameValidation validation)
		{
			return new RenameResult(validation, Array.Empty<FileEdits>());
		}
	}
}
