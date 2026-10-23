namespace FalloutScriptStudio.Core.Globals
{
	/// <summary>The family of variables a call of a script function reads or writes.</summary>
	public enum GlobalVariableKind
	{
		/// <summary>An sfall global variable (<c>set_sfall_global</c>, <c>get_sfall_global_int</c>, <c>get_sfall_global_float</c>), named by an 8 character string or by a number.</summary>
		SfallGlobal = 0,

		/// <summary>A game global variable (<c>global_var</c>, <c>set_global_var</c>, the <c>GVAR_*</c> macros).</summary>
		GameVariable = 1,

		/// <summary>A map variable (<c>map_var</c>, <c>set_map_var</c>, the <c>MVAR_*</c> macros).</summary>
		MapVariable = 2,

		/// <summary>A local variable of a script (<c>local_var</c>, <c>set_local_var</c>, the <c>LVAR_*</c> macros).</summary>
		LocalVariable = 3,
	}
}