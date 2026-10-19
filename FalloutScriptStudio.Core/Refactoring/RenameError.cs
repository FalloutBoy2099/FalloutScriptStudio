namespace FalloutScriptStudio.Core.Refactoring
{
	/// <summary>
	/// Why a rename cannot be done.
	/// </summary>
	public enum RenameError
	{
		/// <summary>No problem.</summary>
		None = 0,

		/// <summary>The new name is empty.</summary>
		EmptyName,

		/// <summary>The new name contains characters other than letters, digits and underscores, or is a reserved word.</summary>
		InvalidName,

		/// <summary>The new name is the same as the old one.</summary>
		SameName,

		/// <summary>A procedure, variable or macro with the new name already exists.</summary>
		NameExists,

		/// <summary>The kind of symbol cannot be renamed.</summary>
		UnsupportedSymbol,
	}
}