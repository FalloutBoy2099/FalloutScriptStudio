namespace FalloutScriptStudio.Core.IO
{
	using System;
	using System.Globalization;
	using System.Text;

	/// <summary>
	/// Text encodings used by Fallout scripts and message files.
	/// </summary>
	/// <remarks>
	/// <see cref="Encoding.Default"/> is UTF-8 on .NET Core, whereas Fallout files are written
	/// with the Windows ANSI code page, so the legacy code pages must be requested explicitly.
	/// </remarks>
	public static class TextEncodings
	{
		private static readonly Lazy<Encoding> _ansi = new Lazy<Encoding>(CreateAnsi);
		private static readonly Lazy<Encoding> _oem866 = new Lazy<Encoding>(() => GetEncoding(866));

		static TextEncodings()
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
		}

		/// <summary>Gets the Windows ANSI code page of the current culture (what .NET Framework called <c>Encoding.Default</c>).</summary>
		public static Encoding Ansi => _ansi.Value;

		/// <summary>Gets the DOS Cyrillic code page (cp866).</summary>
		public static Encoding Oem866 => _oem866.Value;

		/// <summary>Gets UTF-8 without a byte order mark.</summary>
		public static Encoding Utf8 { get; } = new UTF8Encoding(false);

		private static Encoding CreateAnsi()
		{
			Int32 codePage = CultureInfo.CurrentCulture.TextInfo.ANSICodePage;
			return GetEncoding(codePage == 0 ? 1252 : codePage);
		}

		private static Encoding GetEncoding(Int32 codePage)
		{
			try
			{
				return Encoding.GetEncoding(codePage);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
			{
				return Encoding.Latin1;
			}
		}
	}
}
