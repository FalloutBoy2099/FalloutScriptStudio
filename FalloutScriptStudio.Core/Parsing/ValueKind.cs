namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// Kind of the initial value of a variable. Values match the native parser constants.
	/// </summary>
	public enum ValueKind
	{
		/// <summary>The variable has no initial value.</summary>
		None = 0,

		/// <summary>Integer value.</summary>
		Int = 1,

		/// <summary>Floating point value.</summary>
		Float = 2,

		/// <summary>String value.</summary>
		String = 3,
	}
}
