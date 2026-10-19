namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>One message number a script uses, with the place of the number in the script.</summary>
	/// <param name="Number">The message number.</param>
	/// <param name="FileNumber">The number of the message file in <c>scripts.lst</c>, or <see cref="ScriptMessageUsage.OwnFile"/> for the message file of the script itself.</param>
	/// <param name="Line">The 1-based line of the number argument.</param>
	/// <param name="Column">The 1-based column of the number argument.</param>
	/// <param name="Length">The length of the number argument in characters.</param>
	/// <param name="Kind">What the message is used for.</param>
	public sealed record ScriptMessageUsage(Int32 Number, Int32 FileNumber, Int32 Line, Int32 Column, Int32 Length, ScriptMessageUsageKind Kind)
	{
		/// <summary>The <see cref="FileNumber"/> of the message file of the script itself.</summary>
		public const Int32 OwnFile = -1;

		/// <summary>Gets a value indicating whether the message belongs to the message file of the script.</summary>
		public Boolean IsOwnFile => this.FileNumber == OwnFile;

		/// <summary>Gets a value indicating whether the message is a line of a dialogue (an NPC text or a player option), the only kind that has a female variant.</summary>
		public Boolean IsDialogue => this.Kind != ScriptMessageUsageKind.Display;
	}
}