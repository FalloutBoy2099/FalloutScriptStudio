namespace FalloutScriptStudio.Core.Formatting
{
	/// <summary>Where the <c>begin</c> that opens a block goes.</summary>
	public enum BeginPlacement
	{
		/// <summary>Leave it where the user put it.</summary>
		Keep = 0,

		/// <summary>On the line of the header (<c>if (x) then begin</c>, <c>procedure start begin</c>).</summary>
		SameLine = 1,

		/// <summary>On a line of its own, under the header.</summary>
		NextLine = 2,
	}
}