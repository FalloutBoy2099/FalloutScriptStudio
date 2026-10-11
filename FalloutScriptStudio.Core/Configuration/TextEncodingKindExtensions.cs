namespace FalloutScriptStudio.Core.Configuration
{
	using System.Text;
	using FalloutScriptStudio.Core.IO;

	/// <summary>
	/// Conversions of <see cref="TextEncodingKind"/>.
	/// </summary>
	public static class TextEncodingKindExtensions
	{
		/// <summary>
		/// Gets the encoding that belongs to a setting value.
		/// </summary>
		/// <param name="kind">The setting value.</param>
		/// <returns>The encoding.</returns>
		public static Encoding ToEncoding(this TextEncodingKind kind)
		{
			return kind == TextEncodingKind.Oem866 ? TextEncodings.Oem866 : TextEncodings.Ansi;
		}
	}
}
