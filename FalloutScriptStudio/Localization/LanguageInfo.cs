namespace FalloutScriptStudio.Localization
{
	using System;

	/// <summary>A language of the user interface.</summary>
	/// <param name="Code">The two-letter ISO 639-1 code (also the suffix of the string files).</param>
	/// <param name="NativeName">The name of the language in that language.</param>
	public sealed record LanguageInfo(String Code, String NativeName);
}