namespace FalloutScriptStudio.Core.Functions
{
	/// <summary>
	/// Why an edit of the functions tree was not done.
	/// </summary>
	public enum FunctionOperationError
	{
		/// <summary>The operation succeeded.</summary>
		None = 0,

		/// <summary>The operation is not allowed for that node (for example renaming a built-in category).</summary>
		NotAllowed,

		/// <summary>The name or definition is not acceptable.</summary>
		InvalidDefinition,

		/// <summary>The file no longer contains the node where it was read: load the catalog again.</summary>
		NodeChanged,

		/// <summary>The file could not be read or written.</summary>
		IoError,
	}
}
