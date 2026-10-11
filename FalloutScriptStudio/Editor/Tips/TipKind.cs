namespace FalloutScriptStudio.Editor.Tips
{
	/// <summary>What a tooltip describes; it selects the color of the tooltip title.</summary>
	public enum TipKind
	{
		/// <summary>A built-in function (opcode).</summary>
		Opcode,

		/// <summary>A procedure of the script.</summary>
		Procedure,

		/// <summary>A variable.</summary>
		Variable,

		/// <summary>A <c>#define</c> macro.</summary>
		Macro,

		/// <summary>The text of a message of the associated <c>.msg</c> file.</summary>
		Message,

		/// <summary>The target of an <c>#include</c> directive.</summary>
		Include,

		/// <summary>A plain hint.</summary>
		Hint,

		/// <summary>A parser error.</summary>
		Error,

		/// <summary>A parser warning.</summary>
		Warning,
	}
}
