namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;

	/// <summary>What the window "Compare Message Files" shows when it opens.</summary>
	/// <param name="ReferencePath">The full path of the reference message file. The files of the other languages of the game that belong to it (<c>text\&lt;language&gt;\...</c>) are offered as targets.</param>
	public sealed record CompareMessagesRequest(String ReferencePath);
}