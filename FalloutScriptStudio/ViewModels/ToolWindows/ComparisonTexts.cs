namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using Core.Messages;
	using Localization;

	/// <summary>The texts of the comparison of message files in the language of the interface: statuses, issues and notes. They are computed when they are needed, never stored.</summary>
	public static class ComparisonTexts
	{
		private static readonly (MessageComparisonIssues Issue, String Key)[] _issues =
		{
			(MessageComparisonIssues.PlaceholderCount, "Str.ToolWindows.Compare.Issue.PlaceholderCount"),
			(MessageComparisonIssues.TagCount, "Str.ToolWindows.Compare.Issue.TagCount"),
			(MessageComparisonIssues.VariableCount, "Str.ToolWindows.Compare.Issue.VariableCount"),
			(MessageComparisonIssues.WhitespaceOnly, "Str.ToolWindows.Compare.Issue.WhitespaceOnly"),
			(MessageComparisonIssues.TrailingSpaces, "Str.ToolWindows.Compare.Issue.TrailingSpaces"),
			(MessageComparisonIssues.SuspiciousCharacters, "Str.ToolWindows.Compare.Issue.SuspiciousCharacters"),
			(MessageComparisonIssues.PlayerTabDiffers, "Str.ToolWindows.Compare.Issue.PlayerTab"),
			(MessageComparisonIssues.DescriptionDiffers, "Str.ToolWindows.Compare.Issue.Description"),
		};

		/// <summary>Gets the name of a status.</summary>
		/// <param name="status">The status.</param>
		/// <returns>The name.</returns>
		public static String StatusName(MessageComparisonStatus status)
		{
			return Loc.Get("Str.ToolWindows.Compare.Status." + StatusKey(status));
		}

		/// <summary>Gets the key suffix of a status.</summary>
		/// <param name="status">The status.</param>
		/// <returns>The suffix.</returns>
		public static String StatusKey(MessageComparisonStatus status)
		{
			return status switch
			{
				MessageComparisonStatus.MissingInTarget => "Missing",
				MessageComparisonStatus.ExtraInTarget => "Extra",
				MessageComparisonStatus.SameText => "Same",
				MessageComparisonStatus.PlaceholdersOnly => "Placeholders",
				MessageComparisonStatus.EmptyInTarget => "Empty",
				MessageComparisonStatus.PlayerOrDescription => "PlayerDescription",
				_ => "Ok",
			};
		}

		/// <summary>Gets the names of the issues of a row, one text.</summary>
		/// <param name="issues">The issues.</param>
		/// <returns>The names separated by a semicolon; empty when there is none.</returns>
		public static String IssueNames(MessageComparisonIssues issues)
		{
			if (issues == MessageComparisonIssues.None)
			{
				return String.Empty;
			}

			List<String> names = [];
			foreach ((MessageComparisonIssues issue, String key) in _issues)
			{
				if ((issues & issue) != 0)
				{
					names.Add(Loc.Get(key));
				}
			}

			return String.Join("; ", names);
		}

		/// <summary>Gets the text of a note about a file.</summary>
		/// <param name="note">The note.</param>
		/// <returns>The text.</returns>
		public static String NoteText(MessageComparisonNote note)
		{
			ArgumentNullException.ThrowIfNull(note);
			String key = "Str.ToolWindows.Compare.Note." + note.Kind;
			return note.Detail is not null ? Loc.Format(key, note.Detail) : Loc.Format(key, note.Count);
		}

		/// <summary>Gets the headings and names of a report.</summary>
		/// <returns>The labels.</returns>
		public static MessageReportLabels CreateReportLabels()
		{
			return new MessageReportLabels(
				Loc.Get("Str.ToolWindows.Compare.Col.Number"),
				Loc.Get("Str.ToolWindows.Compare.Col.Reference"),
				Loc.Get("Str.ToolWindows.Compare.Col.Target"),
				Loc.Get("Str.ToolWindows.Compare.Col.Status"),
				Loc.Get("Str.ToolWindows.Compare.Col.Issues"),
				StatusName,
				IssueNames);
		}
	}
}