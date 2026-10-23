namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Finds what a translation has to keep as it is in a message: the <c>printf</c> conversions of the game (<c>%s</c>, <c>%d</c>, <c>%02d</c>...),
	/// the <c>&lt;...&gt;</c> tags, the <c>@...@</c> tags and the <c>$...$</c> variables.
	/// </summary>
	public static class MessagePlaceholders
	{
		private static readonly SearchValues<Char> _markers = SearchValues.Create("%<@$");

		private static readonly Regex _conversion = new(@"%[-+0#]*(?:\d+|\*)?(?:\.(?:\d+|\*))?(?:hh|h|ll|l|L|z|j|t)?[diouxXeEfFgGaAcsp]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
		private static readonly Regex _angle = new(@"<[^<>\r\n]{1,64}>", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
		private static readonly Regex _at = new(@"@[^@\r\n]{1,64}@", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
		private static readonly Regex _variable = new(@"\$[^$\r\n]{1,64}\$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

		/// <summary>Compares the placeholders of a reference text and of its translation.</summary>
		/// <param name="reference">The reference text.</param>
		/// <param name="target">The translation.</param>
		/// <returns>The issues: <see cref="MessageComparisonIssues.PlaceholderCount"/>, <see cref="MessageComparisonIssues.TagCount"/> and <see cref="MessageComparisonIssues.VariableCount"/>, or <see cref="MessageComparisonIssues.None"/>.</returns>
		public static MessageComparisonIssues Compare(String reference, String target)
		{
			ArgumentNullException.ThrowIfNull(reference);
			ArgumentNullException.ThrowIfNull(target);

			if (reference.AsSpan().IndexOfAny(_markers) < 0 && target.AsSpan().IndexOfAny(_markers) < 0)
			{
				return MessageComparisonIssues.None;
			}

			MessageComparisonIssues issues = MessageComparisonIssues.None;
			if (!Same(FindConversions(reference), FindConversions(target)))
			{
				issues |= MessageComparisonIssues.PlaceholderCount;
			}

			if (_angle.Matches(reference).Count != _angle.Matches(target).Count || !Same(Find(_at, reference), Find(_at, target)))
			{
				issues |= MessageComparisonIssues.TagCount;
			}

			if (!Same(Find(_variable, reference), Find(_variable, target)))
			{
				issues |= MessageComparisonIssues.VariableCount;
			}

			return issues;
		}

		/// <summary>Removes the placeholders, tags and variables from a text and collapses its white space, to tell whether two texts differ only by them.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The text without them.</returns>
		public static String Strip(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			String stripped = text;
			if (text.AsSpan().IndexOfAny(_markers) >= 0)
			{
				stripped = _conversion.Replace(stripped.Replace("%%", String.Empty, StringComparison.Ordinal), String.Empty);
				stripped = _variable.Replace(_at.Replace(_angle.Replace(stripped, String.Empty), String.Empty), String.Empty);
			}

			return CollapseWhiteSpace(stripped);
		}

		private static String CollapseWhiteSpace(String text)
		{
			StringBuilder builder = new(text.Length);
			Boolean pending = false;
			foreach (Char c in text)
			{
				if (Char.IsWhiteSpace(c))
				{
					pending = builder.Length > 0;
					continue;
				}

				if (pending)
				{
					builder.Append(' ');
					pending = false;
				}

				builder.Append(c);
			}

			return builder.ToString();
		}

		private static List<String> FindConversions(String text)
		{
			if (!text.Contains('%', StringComparison.Ordinal))
			{
				return [];
			}

			return _conversion.Matches(text.Replace("%%", String.Empty, StringComparison.Ordinal)).Select(match => match.Value[^1].ToString()).Order(StringComparer.Ordinal).ToList();
		}

		private static List<String> Find(Regex pattern, String text)
		{
			return pattern.Matches(text).Select(match => match.Value.ToLowerInvariant()).Order(StringComparer.Ordinal).ToList();
		}

		private static Boolean Same(List<String> first, List<String> second)
		{
			return first.SequenceEqual(second, StringComparer.Ordinal);
		}
	}
}