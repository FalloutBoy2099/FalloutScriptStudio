namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>The likely mistakes found in the target text of a message, whatever its <see cref="MessageComparisonStatus"/>.</summary>
	[Flags]
	public enum MessageComparisonIssues
	{
		/// <summary>Nothing is suspicious.</summary>
		None = 0,

		/// <summary>The target has other <c>%s</c> / <c>%d</c> conversions than the reference.</summary>
		PlaceholderCount = 1,

		/// <summary>The target has another number of <c>&lt;...&gt;</c> tags, or other <c>@...@</c> tags, than the reference.</summary>
		TagCount = 2,

		/// <summary>The target has other <c>$...$</c> variables than the reference.</summary>
		VariableCount = 4,

		/// <summary>The target text consists of white space only.</summary>
		WhitespaceOnly = 8,

		/// <summary>The target text ends with spaces (the reference text does not).</summary>
		TrailingSpaces = 16,

		/// <summary>The target text holds characters that no code page of the file can give: the file was probably read with the wrong encoding.</summary>
		SuspiciousCharacters = 32,

		/// <summary>One of the two messages is a player line (leading tab) and the other is not.</summary>
		PlayerTabDiffers = 64,

		/// <summary>The text after the closing brace (the description) differs.</summary>
		DescriptionDiffers = 128,
	}
}