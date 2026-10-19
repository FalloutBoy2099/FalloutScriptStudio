namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>Options of <see cref="ScriptMessageConsistency"/>.</summary>
	public sealed record ScriptMessageOptions
	{
		/// <summary>The number added to a message number to get its female variant (the convention of the dialogue preview).</summary>
		public const Int32 DefaultFemaleOffset = 1000;

		/// <summary>Gets the number added to a message number to get its female variant.</summary>
		public Int32 FemaleOffset { get; init; } = DefaultFemaleOffset;

		/// <summary>Gets a value indicating whether the missing female variants of the dialogue are reported (when the script or the message file uses them).</summary>
		public Boolean CheckFemaleVariants { get; init; } = true;
	}
}