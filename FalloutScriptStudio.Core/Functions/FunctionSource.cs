namespace FalloutScriptStudio.Core.Functions
{
	/// <summary>
	/// The file a node of the functions tree comes from.
	/// </summary>
	public enum FunctionSource
	{
		/// <summary>The shipped <c>functions.ini</c>.</summary>
		BuiltIn,

		/// <summary>The user's <c>userfunctions.ini</c>.</summary>
		User,
	}
}