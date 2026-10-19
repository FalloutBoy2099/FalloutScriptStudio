namespace FalloutScriptStudio.Core.Formatting
{
	/// <summary>What the formatter does with the white space around an operator or after a comma.</summary>
	public enum SpacingStyle
	{
		/// <summary>Leave the text as the user typed it.</summary>
		Keep = 0,

		/// <summary>Put the usual spaces (the behaviour of the first versions of the formatter).</summary>
		Add = 1,

		/// <summary>Remove the spaces (a space stays where two symbols would otherwise fuse into another operator).</summary>
		Remove = 2,
	}
}