namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// Optimization level passed to the sslc compiler as <c>-O&lt;level&gt;</c>.
	/// </summary>
	public enum OptimizationLevel : Byte
	{
		/// <summary>No optimization.</summary>
		None = 0,

		/// <summary>Basic optimization (default).</summary>
		Basic = 1,

		/// <summary>Full optimization.</summary>
		Full = 2,

		/// <summary>Experimental optimization.</summary>
		Experimental = 3,
	}
}