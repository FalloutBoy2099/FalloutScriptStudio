namespace FalloutScriptStudio.Core.Globals
{
	/// <summary>What is suspicious about a global variable.</summary>
	public enum GlobalFindingKind
	{
		/// <summary>The value is written but no script of the project reads it (information: another mod or a hook may).</summary>
		WrittenNeverRead = 0,

		/// <summary>The value is read but no script of the project writes it: it stays at its default unless the engine or another mod sets it (warning).</summary>
		ReadNeverWritten = 1,

		/// <summary>An sfall global is used as an integer in one place and as a floating point number in another (warning).</summary>
		TypeConflict = 2,

		/// <summary>The name differs only by case or by one character from the name of another variable: a probable typo (information).</summary>
		SimilarName = 3,

		/// <summary>The name of an sfall global does not have exactly 8 characters: sfall ignores the call (warning).</summary>
		NameLength = 4,

		/// <summary>Two different long names of sfall globals start with the same 8 characters (warning).</summary>
		PrefixCollision = 5,
	}
}