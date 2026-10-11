namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// Modifiers of a procedure. Values match the flags reported by the native parser.
	/// </summary>
	[Flags]
	public enum ProcedureFlags
	{
		/// <summary>No modifier.</summary>
		None = 0,

		/// <summary>The procedure is used as a timed event.</summary>
		Timed = 0x01,

		/// <summary>The procedure is used as a conditional procedure.</summary>
		Conditional = 0x02,

		/// <summary>The procedure is imported from another script (<c>import procedure</c>).</summary>
		Import = 0x04,

		/// <summary>The procedure is exported to other scripts (<c>export procedure</c>).</summary>
		Export = 0x08,

		/// <summary>The procedure is <c>critical</c>.</summary>
		Critical = 0x10,

		/// <summary>The procedure is <c>pure</c>.</summary>
		Pure = 0x20,

		/// <summary>The procedure is <c>inline</c>.</summary>
		Inline = 0x40,
	}
}
