namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// A quoted string <c>"..."</c> inside a line (legacy <c>Utilities.Quote</c>).
	/// </summary>
	/// <param name="Open">Index of the opening quote.</param>
	/// <param name="Close">Index of the closing quote.</param>
	public readonly record struct QuoteSpan(Int32 Open, Int32 Close)
	{
		/// <summary>Determines whether an index lies strictly between the quotes.</summary>
		/// <param name="index">The index.</param>
		/// <returns><c>true</c> when the index is inside the string.</returns>
		public Boolean ContainsInside(Int32 index)
		{
			return index > this.Open && index < this.Close;
		}
	}
}