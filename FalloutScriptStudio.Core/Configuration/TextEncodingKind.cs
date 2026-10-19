namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// The code page used to read and write scripts and message files.
	/// </summary>
	public enum TextEncodingKind : Byte
	{
		/// <summary>The Windows ANSI code page of the system.</summary>
		Ansi = 0,

		/// <summary>The DOS Cyrillic code page (cp866).</summary>
		Oem866 = 1,
	}
}