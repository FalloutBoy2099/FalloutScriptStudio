namespace FalloutScriptStudio.Core.Formatting
{
	using System;

	/// <summary>Runs the passes of the formatter in the order that makes the result stable (formatting a formatted text changes nothing).</summary>
	internal static class FormatPipeline
	{
		/// <summary>Formats a text.</summary>
		/// <param name="text">The text.</param>
		/// <param name="options">The options.</param>
		/// <returns>The formatted text.</returns>
		public static String Format(String text, FormatOptions options)
		{
			FormatOptions normalized = options.Normalize();
			String result = SpacingPass.Apply(text, normalized);
			if (!normalized.IsStructural)
			{
				return result;
			}

			result = ParenthesisPass.Apply(result, normalized.ParenthesisSpacing);
			result = PlacementPass.Apply(result, normalized);
			if (normalized.Reindent)
			{
				result = IndentPass.Apply(result, normalized);
			}

			if (normalized.MaxLineLength > 0)
			{
				result = WrapPass.Apply(result, normalized);
			}

			result = BlankLinePass.Apply(result, normalized);
			if (normalized.AlignDefines)
			{
				result = DefineAlignmentPass.Apply(result);
			}

			if (normalized.TrimTrailingWhitespace)
			{
				result = WhitespacePass.TrimTrailing(result);
			}

			if (normalized.EnsureFinalNewline)
			{
				result = WhitespacePass.EnsureFinalNewline(result);
			}

			return result;
		}
	}
}