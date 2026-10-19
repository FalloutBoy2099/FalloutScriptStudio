namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>
	/// Why a new dialogue node could not be created.
	/// </summary>
	public enum NodeCreationError
	{
		/// <summary>No problem.</summary>
		None = 0,

		/// <summary>The name is empty.</summary>
		EmptyName,

		/// <summary>The name still contains the <c>#</c> placeholder of the suggested name.</summary>
		PlaceholderInName,

		/// <summary>The name contains characters other than letters, digits and underscores.</summary>
		InvalidName,

		/// <summary>A procedure, variable or macro with that name already exists.</summary>
		NameExists,
	}
}