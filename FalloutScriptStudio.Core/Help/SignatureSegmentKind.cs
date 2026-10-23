namespace FalloutScriptStudio.Core.Help
{
	/// <summary>The part of a signature a piece of text belongs to (the help shows each kind in its own color).</summary>
	public enum SignatureSegmentKind
	{
		/// <summary>The type of the result.</summary>
		ReturnType = 0,

		/// <summary>The name of the function.</summary>
		Name = 1,

		/// <summary>Parentheses, commas, brackets and spaces.</summary>
		Punctuation = 2,

		/// <summary>The type of a parameter.</summary>
		ParameterType = 3,

		/// <summary>The name of a parameter.</summary>
		ParameterName = 4,

		/// <summary>The <c>...</c> that stands for any number of further arguments.</summary>
		Variadic = 5,
	}
}