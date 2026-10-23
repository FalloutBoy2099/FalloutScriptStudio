namespace FalloutScriptStudio.Core.Globals
{
	/// <summary>The type of the value of an sfall global at a use.</summary>
	public enum GlobalValueType
	{
		/// <summary>Not known (the value is an expression, or the variable has a single type).</summary>
		Unknown = 0,

		/// <summary>An integer: <c>get_sfall_global_int</c>, or a written integer literal.</summary>
		Int = 1,

		/// <summary>A floating point number: <c>get_sfall_global_float</c>, or a written literal with a decimal point.</summary>
		Float = 2,
	}
}