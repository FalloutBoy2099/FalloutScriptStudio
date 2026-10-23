namespace FalloutScriptStudio.Core.Globals
{
	using System;

	/// <summary>What a use does with a global variable.</summary>
	[Flags]
	public enum GlobalAccess
	{
		/// <summary>The value is read.</summary>
		Read = 1,

		/// <summary>The value is written.</summary>
		Write = 2,

		/// <summary>The value is read and written (<c>inc_global_var</c>, <c>set_gvar_bit_on</c>...).</summary>
		ReadWrite = Read | Write,
	}
}