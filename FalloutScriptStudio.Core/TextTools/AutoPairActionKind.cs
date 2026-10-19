namespace FalloutScriptStudio.Core.TextTools
{
	/// <summary>
	/// What the editor does around a typed bracket or quote.
	/// </summary>
	public enum AutoPairActionKind
	{
		/// <summary>Nothing extra: just insert the typed character.</summary>
		None = 0,

		/// <summary>Insert <see cref="AutoPairAction.Text"/> at the caret before inserting the typed character, so the typed opening character ends up in front of it.</summary>
		InsertClosing,

		/// <summary>Remove the character after the caret before inserting the typed character, so the typed closing character replaces it.</summary>
		RemoveNext,
	}
}