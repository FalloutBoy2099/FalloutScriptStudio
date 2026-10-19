namespace FalloutScriptStudio.Core.Formatting
{
	/// <summary>Whether <c>else</c> and the <c>begin</c> that follows it share a line.</summary>
	public enum ElseBeginStyle
	{
		/// <summary>Leave it as the user typed it (the general <see cref="BeginPlacement"/> applies).</summary>
		Keep = 0,

		/// <summary>Always <c>else begin</c> on one line.</summary>
		Join = 1,

		/// <summary>Always <c>else</c> and <c>begin</c> on two lines.</summary>
		Split = 2,
	}
}