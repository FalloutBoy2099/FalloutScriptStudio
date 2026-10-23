namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>The message file of one language that belongs to another message file.</summary>
	/// <param name="Language">The name of the language folder (<c>french</c>).</param>
	/// <param name="Path">The full path of the file.</param>
	public sealed record MessageLanguageFile(String Language, String Path);
}