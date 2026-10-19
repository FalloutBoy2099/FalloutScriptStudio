namespace FalloutScriptStudio.Core.Compilation
{
	/// <summary>
	/// Kind of an entry in the build log or in an error list.
	/// </summary>
	public enum BuildErrorType
	{
		/// <summary>Unspecified entry.</summary>
		None = 0,

		/// <summary>An error.</summary>
		Error,

		/// <summary>A warning.</summary>
		Warning,

		/// <summary>An informational message.</summary>
		Message,

		/// <summary>A search result.</summary>
		Search,

		/// <summary>An entry produced by the script parser.</summary>
		Parser,
	}
}