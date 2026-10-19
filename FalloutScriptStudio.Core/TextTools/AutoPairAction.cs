namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// The decision of <see cref="AutoPairHandler"/> for a typed character.
	/// </summary>
	/// <param name="Kind">What to do before the typed character is inserted.</param>
	/// <param name="Text">For <see cref="AutoPairActionKind.InsertClosing"/> the text to insert at the caret; otherwise <c>null</c>.</param>
	/// <param name="ArmOvertype">
	/// Whether the editor should remember that a bracket was closed automatically, so that typing the closing bracket right away
	/// replaces it (the legacy code kept this for two key presses).
	/// </param>
	public sealed record AutoPairAction(AutoPairActionKind Kind, String? Text, Boolean ArmOvertype)
	{
		/// <summary>Gets the action that does nothing.</summary>
		public static AutoPairAction None { get; } = new AutoPairAction(AutoPairActionKind.None, null, false);
	}
}