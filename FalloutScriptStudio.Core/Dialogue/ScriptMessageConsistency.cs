namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Linq;
	using Messages;
	using Parsing;

	/// <summary>
	/// Checks a script against its message file (<c>.msg</c>), the way the game uses them: the script shows the lines of the file by
	/// number. It reports the numbers the script uses and the file lacks (error), the numbers the file has and the script never uses
	/// (information), the numbers that appear twice in the file (error), the used messages whose text is empty (warning), the used
	/// dialogue messages that have no female variant while the script or the file uses them (information) and the missing file.
	/// </summary>
	/// <remarks>
	/// The check is pure: it reads the script text, its parse result and the parsed message file, nothing else. The numbers are found by
	/// <see cref="ScriptMessageScanner"/> (the calls of the dialogue rules, <c>message_str</c> and the macros that expand to them);
	/// the unused messages are only listed when every number of the script could be worked out, so an unknown variable never makes a
	/// message look unused. Comment lines of the message file are not messages. Numbers of the other message files of
	/// <c>scripts.lst</c> are checked against the files given, and ignored when the file is not given.
	/// </remarks>
	public static class ScriptMessageConsistency
	{
		/// <summary>Checks a script.</summary>
		/// <param name="scriptText">The text of the script.</param>
		/// <param name="info">The parse result of the script.</param>
		/// <param name="rules">The dialogue function rules.</param>
		/// <param name="ownMessages">The message file of the script, or <see langword="null"/> when it does not exist or is not associated.</param>
		/// <param name="otherFiles">The other message files the script uses, by their number in <c>scripts.lst</c>.</param>
		/// <param name="options">The options; the defaults when <see langword="null"/>.</param>
		/// <returns>The report.</returns>
		public static ScriptMessageReport Check(
			String scriptText,
			ProgramInfo info,
			DialogueFunctionRules rules,
			MessageDocument? ownMessages,
			IReadOnlyDictionary<Int32, MessageDocument>? otherFiles = null,
			ScriptMessageOptions? options = null)
		{
			return Check(ScriptMessageScanner.Scan(scriptText, info, rules), ownMessages, otherFiles, options);
		}

		/// <summary>Checks the numbers a script uses against its message file.</summary>
		/// <param name="scan">What <see cref="ScriptMessageScanner"/> found in the script.</param>
		/// <param name="ownMessages">The message file of the script, or <see langword="null"/> when it does not exist or is not associated.</param>
		/// <param name="otherFiles">The other message files the script uses, by their number in <c>scripts.lst</c>.</param>
		/// <param name="options">The options; the defaults when <see langword="null"/>.</param>
		/// <returns>The report.</returns>
		public static ScriptMessageReport Check(ScriptMessageScan scan, MessageDocument? ownMessages, IReadOnlyDictionary<Int32, MessageDocument>? otherFiles = null, ScriptMessageOptions? options = null)
		{
			ArgumentNullException.ThrowIfNull(scan);

			options ??= new ScriptMessageOptions();
			List<ScriptMessageUsage> ownUsages = scan.OwnUsages.ToList();
			List<Int32> used = ownUsages.Select(usage => usage.Number).Distinct().Order().ToList();
			List<ScriptMessageFinding> findings = [];
			List<Int32> missing = [];

			if (ownMessages is null)
			{
				if (used.Count > 0)
				{
					findings.Add(new ScriptMessageFinding(ScriptMessageFindingKind.MessageFileMissing, ScriptMessageSeverity.Warning, 0, ScriptMessageUsage.OwnFile, false, -1, -1, -1, used.Count));
					missing.AddRange(used);
				}
			}
			else
			{
				MessageIndex index = new MessageIndex(ownMessages);
				AddMissing(findings, missing, ownUsages, index, ScriptMessageUsage.OwnFile);
				AddDuplicates(findings, index);
				AddEmpty(findings, used, index);
				if (options.CheckFemaleVariants)
				{
					AddMissingFemale(findings, ownUsages, used, index, scan.UsesFemaleVariants, options.FemaleOffset);
				}

				AddUnused(findings, scan, ownUsages.Count, used, index, options.FemaleOffset);
			}

			AddOtherFiles(findings, scan, otherFiles);
			return new ScriptMessageReport(findings, missing, used, scan);
		}

		private static void AddMissing(List<ScriptMessageFinding> findings, List<Int32> missing, List<ScriptMessageUsage> usages, MessageIndex index, Int32 fileNumber)
		{
			foreach (ScriptMessageUsage usage in usages.OrderBy(u => u.Line).ThenBy(u => u.Column))
			{
				if (!index.Contains(usage.Number))
				{
					findings.Add(new ScriptMessageFinding(ScriptMessageFindingKind.MissingMessage, ScriptMessageSeverity.Error, usage.Number, fileNumber, true, usage.Line, usage.Column, usage.Length));
					if (fileNumber == ScriptMessageUsage.OwnFile && !missing.Contains(usage.Number))
					{
						missing.Add(usage.Number);
					}
				}
			}

			missing.Sort();
		}

		private static void AddDuplicates(List<ScriptMessageFinding> findings, MessageIndex index)
		{
			foreach ((Int32 id, Int32 line) in index.Duplicates)
			{
				findings.Add(new ScriptMessageFinding(ScriptMessageFindingKind.DuplicateMessage, ScriptMessageSeverity.Error, id, ScriptMessageUsage.OwnFile, false, line, 1, -1));
			}
		}

		private static void AddEmpty(List<ScriptMessageFinding> findings, List<Int32> used, MessageIndex index)
		{
			List<ScriptMessageFinding> empty = [];
			foreach (Int32 number in used)
			{
				if (index.TryGet(number, out MessageEntry? entry, out Int32 line) && entry.Text.Trim().Length == 0)
				{
					empty.Add(new ScriptMessageFinding(ScriptMessageFindingKind.EmptyMessage, ScriptMessageSeverity.Warning, number, ScriptMessageUsage.OwnFile, false, line, 1, -1));
				}
			}

			findings.AddRange(empty.OrderBy(finding => finding.Line));
		}

		private static void AddMissingFemale(List<ScriptMessageFinding> findings, List<ScriptMessageUsage> usages, List<Int32> used, MessageIndex index, Boolean scriptUsesFemale, Int32 offset)
		{
			if (offset <= 0)
			{
				return;
			}

			List<Int32> dialogue = usages.Where(usage => usage.IsDialogue).Select(usage => usage.Number).Distinct().ToList();

			// The file uses variants when a used dialogue message has one that the script itself does not use (a number that is used for
			// something else, such as 1100 next to 100 in a file that numbers its nodes by thousands, is no variant).
			HashSet<Int32> usedSet = new(used);
			Boolean fileUsesVariants = dialogue.Any(number => index.Contains(number + offset) && !usedSet.Contains(number + offset));
			if (!scriptUsesFemale && !fileUsesVariants)
			{
				return;
			}

			List<ScriptMessageFinding> found = [];
			foreach (Int32 number in dialogue)
			{
				if (index.TryGet(number, out _, out Int32 line) && !index.Contains(number + offset))
				{
					found.Add(new ScriptMessageFinding(ScriptMessageFindingKind.MissingFemaleVariant, ScriptMessageSeverity.Info, number, ScriptMessageUsage.OwnFile, false, line, 1, -1));
				}
			}

			findings.AddRange(found.OrderBy(finding => finding.Line));
		}

		private static void AddUnused(List<ScriptMessageFinding> findings, ScriptMessageScan scan, Int32 usageCount, List<Int32> used, MessageIndex index, Int32 femaleOffset)
		{
			if (scan.UnresolvedCount > 0)
			{
				findings.Add(new ScriptMessageFinding(ScriptMessageFindingKind.UnresolvedUsages, ScriptMessageSeverity.Info, 0, ScriptMessageUsage.OwnFile, true, -1, -1, -1, scan.UnresolvedCount));
				return;
			}

			if (usageCount == 0)
			{
				if (index.Count > 0)
				{
					findings.Add(new ScriptMessageFinding(ScriptMessageFindingKind.NoUsageFound, ScriptMessageSeverity.Info, 0, ScriptMessageUsage.OwnFile, true, -1, -1, -1, index.Count));
				}

				return;
			}

			// The female variant of a used message is used with it.
			HashSet<Int32> covered = new(used);
			if (femaleOffset > 0)
			{
				covered.UnionWith(used.Select(number => number + femaleOffset));
			}

			foreach ((Int32 id, Int32 line) in index.Messages)
			{
				if (!covered.Contains(id))
				{
					findings.Add(new ScriptMessageFinding(ScriptMessageFindingKind.UnusedMessage, ScriptMessageSeverity.Info, id, ScriptMessageUsage.OwnFile, false, line, 1, -1));
				}
			}
		}

		private static void AddOtherFiles(List<ScriptMessageFinding> findings, ScriptMessageScan scan, IReadOnlyDictionary<Int32, MessageDocument>? otherFiles)
		{
			if (otherFiles is null)
			{
				return;
			}

			foreach (IGrouping<Int32, ScriptMessageUsage> group in scan.Usages.Where(usage => !usage.IsOwnFile).GroupBy(usage => usage.FileNumber).OrderBy(group => group.Key))
			{
				if (otherFiles.TryGetValue(group.Key, out MessageDocument? document))
				{
					AddMissing(findings, [], group.ToList(), new MessageIndex(document), group.Key);
				}
			}
		}

		/// <summary>The messages of a message file with the physical line each one starts on.</summary>
		private sealed class MessageIndex
		{
			private readonly Dictionary<Int32, (MessageEntry Entry, Int32 Line)> _first = [];
			private readonly List<(Int32 Id, Int32 Line)> _messages = [];
			private readonly List<(Int32 Id, Int32 Line)> _duplicates = [];

			public MessageIndex(MessageDocument document)
			{
				Int32 line = 1;
				foreach (MessageEntry entry in document.Entries)
				{
					if (entry.Kind == MessageLineKind.Message)
					{
						if (this._first.TryAdd(entry.Id, (entry, line)))
						{
							this._messages.Add((entry.Id, line));
						}
						else
						{
							this._duplicates.Add((entry.Id, line));
						}
					}

					line += 1 + (entry.Kind == MessageLineKind.Message ? CountLineBreaks(entry.Text) : 0);
				}
			}

			/// <summary>Gets the distinct messages in file order.</summary>
			public IReadOnlyList<(Int32 Id, Int32 Line)> Messages => this._messages;

			/// <summary>Gets the second and later lines of a number that appears more than once.</summary>
			public IReadOnlyList<(Int32 Id, Int32 Line)> Duplicates => this._duplicates;

			/// <summary>Gets the number of distinct messages.</summary>
			public Int32 Count => this._messages.Count;

			public Boolean Contains(Int32 id)
			{
				return this._first.ContainsKey(id);
			}

			public Boolean TryGet(Int32 id, [NotNullWhen(true)] out MessageEntry? entry, out Int32 line)
			{
				if (this._first.TryGetValue(id, out (MessageEntry Entry, Int32 Line) found))
				{
					entry = found.Entry;
					line = found.Line;
					return true;
				}

				entry = null;
				line = -1;
				return false;
			}

			private static Int32 CountLineBreaks(String text)
			{
				Int32 count = 0;
				Int32 index = 0;
				while ((index = text.IndexOf(MessageEntry.LineBreak, index, StringComparison.Ordinal)) >= 0)
				{
					count++;
					index += MessageEntry.LineBreak.Length;
				}

				return count;
			}
		}
	}
}