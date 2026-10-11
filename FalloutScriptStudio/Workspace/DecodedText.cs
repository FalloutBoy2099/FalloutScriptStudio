namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Text;

	/// <summary>The text of a file together with the encoding it was read with (and is saved with).</summary>
	/// <param name="Text">The text.</param>
	/// <param name="Encoding">The detected encoding.</param>
	public sealed record DecodedText(String Text, Encoding Encoding);
}
