namespace FalloutScriptStudio.Core.Formatting
{
	/// <summary>Where an <c>else</c> that follows the <c>end</c> of a block goes.</summary>
	public enum ElsePlacement
	{
		/// <summary>Leave it where the user put it.</summary>
		Keep = 0,

		/// <summary>On the line of the <c>end</c> (<c>end else begin</c>).</summary>
		AfterEnd = 1,

		/// <summary>On a line of its own, under the <c>end</c>.</summary>
		OwnLine = 2,
	}
}