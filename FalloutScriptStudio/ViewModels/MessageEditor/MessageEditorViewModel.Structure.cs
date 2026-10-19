namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;

	/// <summary>The structure check of the file: scheduling, the issue markers of the rows and the status summary.</summary>
	public sealed partial class MessageEditorViewModel
	{
		/// <summary>From this number of rows on, the structure check runs once after a pause in the edits instead of after every edit.</summary>
		private const Int32 DeferredCheckRows = 2000;

		/// <summary>How long the edits of a big file must pause before the structure check runs.</summary>
		private static readonly TimeSpan _structureCheckDelay = TimeSpan.FromMilliseconds(250);

		/// <summary>
		/// Checks the structure after a change of the rows: at once for a small file, after a pause in the edits for a big one (the
		/// check formats and scans the whole file, which would make every edit of a file of tens of thousands of lines slow).
		/// </summary>
		private void ScheduleStructureCheck()
		{
			if (this._rows.Count < DeferredCheckRows || this._disposed)
			{
				this.RunStructureCheck();
				return;
			}

			if (this._structureCheckTimer is null)
			{
				this._structureCheckTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background) { Interval = _structureCheckDelay };
				this._structureCheckTimer.Tick += this.OnStructureCheckTimer;
			}

			this._structureCheckTimer.Stop();
			this._structureCheckTimer.Start();
		}

		/// <summary>Runs the structure check that is waiting for a pause in the edits, if there is one.</summary>
		private void FlushStructureCheck()
		{
			if (this._structureCheckTimer is { IsEnabled: true })
			{
				this.RunStructureCheck();
			}
		}

		private void OnStructureCheckTimer(Object? sender, EventArgs e)
		{
			this.RunStructureCheck();
		}

		/// <summary>
		/// Re-runs the structure check on the current content and updates the markers of the rows.
		/// </summary>
		public void RunStructureCheck()
		{
			this._structureCheckTimer?.Stop();
			Int32 count = this._rows.Count;
			Int32[] firstLine = new Int32[count];
			StringBuilder text = new();
			Int32 line = 1;
			for (Int32 i = 0; i < count; i++)
			{
				String formatted = this._rows[i].Entry.Format();
				firstLine[i] = line;
				if (i > 0)
				{
					text.Append(MessageEntry.LineBreak);
				}

				text.Append(formatted);
				line++;
				for (Int32 c = 0; c < formatted.Length; c++)
				{
					if (formatted[c] == '\n')
					{
						line++;
					}
				}
			}

			MessageStructureReport report = this._checker.Check(text.ToString());
			MessageIssueSeverity[] severities = new MessageIssueSeverity[count];
			List<String>?[] messages = new List<String>?[count];
			Int32 errors = 0;
			Int32 warnings = 0;
			Int32 hints = 0;

			foreach (MessageStructureIssue issue in report.Issues)
			{
				Int32 index = Array.BinarySearch(firstLine, issue.Line);
				if (index < 0)
				{
					index = ~index - 1;
				}

				if (index < 0 || index >= count)
				{
					continue;
				}

				MessageIssueSeverity severity = issue.Type switch
				{
					BuildErrorType.Error => MessageIssueSeverity.Error,
					BuildErrorType.Warning => MessageIssueSeverity.Warning,
					_ => MessageIssueSeverity.Hint,
				};
				switch (severity)
				{
					case MessageIssueSeverity.Error:
						errors++;
						break;
					case MessageIssueSeverity.Warning:
						warnings++;
						break;
					default:
						hints++;
						break;
				}

				AddIssue(severities, messages, index, severity, LocalizeIssue(issue.Message));
			}

			for (Int32 i = 0; i < count; i++)
			{
				if (this._rows[i].Kind == MessageLineKind.Raw)
				{
					warnings++;
					AddIssue(severities, messages, i, MessageIssueSeverity.Warning, Loc.Get("Str.MessageEditor.Issue.Unrecognized"));
				}
			}

			for (Int32 i = 0; i < count; i++)
			{
				this._rows[i].Severity = severities[i];
				this._rows[i].IssueText = messages[i] is null ? String.Empty : String.Join(Environment.NewLine, messages[i]!);
			}

			this.ErrorCount = errors;
			this.WarningCount = warnings;
			this.HintCount = hints;
			this.IssueSummary = DescribeIssueCounts(errors, warnings, hints);
		}

		/// <summary>The messages of the structure checker (English, from Core) and the keys of their translations.</summary>
		private static readonly Dictionary<String, String> _issueKeys = new(StringComparer.Ordinal)
		{
			["Wrong structure of the paired curves brackets."] = "Str.MessageEditor.Issue.Brackets",
			["A new line without a white space character."] = "Str.MessageEditor.Issue.NewLineSpace",
			["The white space character is missing."] = "Str.MessageEditor.Issue.MissingSpace",
			["The sentence begins with a small letter instead of the capital letter."] = "Str.MessageEditor.Issue.SmallLetter",
			["Missing '#' character of comment?"] = "Str.MessageEditor.Issue.MissingHash",
			["Missing line number of the message."] = "Str.MessageEditor.Issue.MissingNumber",
			["Invalid line number of the message."] = "Str.MessageEditor.Issue.InvalidNumber",
		};

		private const String DuplicateNumberPrefix = "Duplicate message line number: ";

		private static String LocalizeIssue(String message)
		{
			if (_issueKeys.TryGetValue(message, out String? key))
			{
				return Loc.Get(key);
			}

			return message.StartsWith(DuplicateNumberPrefix, StringComparison.Ordinal)
				? Loc.Format("Str.MessageEditor.Issue.DuplicateNumber", message.Substring(DuplicateNumberPrefix.Length))
				: message;
		}

		private static String DescribeIssueCounts(Int32 errors, Int32 warnings, Int32 hints)
		{
			List<String> parts = [];
			if (errors > 0)
			{
				parts.Add(Loc.Format(errors == 1 ? "Str.MessageEditor.Issue.ErrorOne" : "Str.MessageEditor.Issue.ErrorMany", errors));
			}

			if (warnings > 0)
			{
				parts.Add(Loc.Format(warnings == 1 ? "Str.MessageEditor.Issue.WarningOne" : "Str.MessageEditor.Issue.WarningMany", warnings));
			}

			if (hints > 0)
			{
				parts.Add(Loc.Format(hints == 1 ? "Str.MessageEditor.Issue.HintOne" : "Str.MessageEditor.Issue.HintMany", hints));
			}

			return parts.Count == 0 ? Loc.Get("Str.MessageEditor.Issue.None") : String.Join(Loc.Get("Str.MessageEditor.Issue.Separator"), parts);
		}

		private static void AddIssue(MessageIssueSeverity[] severities, List<String>?[] messages, Int32 index, MessageIssueSeverity severity, String message)
		{
			if (severity > severities[index])
			{
				severities[index] = severity;
			}

			messages[index] ??= [];
			if (!messages[index]!.Contains(message))
			{
				messages[index]!.Add(message);
			}
		}
	}
}