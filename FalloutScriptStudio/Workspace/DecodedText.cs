namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Text;

	/// <summary>The text of a file together with the encoding it was read with (and is saved with).</summary>
	/// <param name="Text">The text.</param>
	/// <param name="Encoding">The detected encoding.</param>
	public sealed record DecodedText(String Text, Encoding Encoding)
	{
		/// <summary>Gets a value indicating whether the file has non-ASCII bytes that are not valid UTF-8, so the code page of the settings was a guess.</summary>
		public Boolean UsedFallback { get; init; }
	}
}