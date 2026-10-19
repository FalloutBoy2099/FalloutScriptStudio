namespace FalloutScriptStudio.Core.Parsing
{
	/// <summary>
	/// Scope of a script variable. Values match the native parser constants.
	/// </summary>
	public enum VariableScope
	{
		/// <summary>Local to a procedure (or one of its arguments).</summary>
		Local = 1,

		/// <summary>Script-level variable.</summary>
		Global = 2,

		/// <summary>Variable imported from another script.</summary>
		Import = 3,

		/// <summary>Variable exported to other scripts.</summary>
		Export = 4,
	}
}