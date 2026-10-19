namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Core.Dialogue;

	/// <summary>The problems of the check of a script against its message file.</summary>
	public static partial class ProblemFactory
	{
		/// <summary>Creates the problem of one finding of <see cref="ScriptMessageConsistency"/>.</summary>
		/// <param name="finding">The finding.</param>
		/// <param name="scriptPath">The script that was checked.</param>
		/// <param name="messageFilePath">The message file of the script, or <see langword="null"/> when there is none.</param>
		/// <param name="missingNumbers">The numbers the message file lacks (what the fix of a missing message appends).</param>
		/// <param name="femaleOffset">The number that is added to a message number to get its female variant.</param>
		/// <param name="owner">The script document.</param>
		/// <returns>The problem.</returns>
		public static Problem ScriptMessage(ScriptMessageFinding finding, String scriptPath, String? messageFilePath, IReadOnlyList<Int32> missingNumbers, Int32 femaleOffset, Object? owner)
		{
			ArgumentNullException.ThrowIfNull(finding);
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(missingNumbers);

			String fileName = messageFilePath is null ? "?" : Path.GetFileName(messageFilePath);
			String fileLabel = finding.FileNumber == ScriptMessageUsage.OwnFile ? fileName : "#" + finding.FileNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
			(String name, Object?[] arguments, ProblemAction? action) = finding.Kind switch
			{
				ScriptMessageFindingKind.MissingMessage => (
					"MsgMissing",
					new Object?[] { finding.Number, fileLabel },
					finding.FileNumber == ScriptMessageUsage.OwnFile && missingNumbers.Count > 0 ? ProblemAction.AddMissingMessages(scriptPath, missingNumbers) : null),
				ScriptMessageFindingKind.UnusedMessage => ("MsgUnused", new Object?[] { finding.Number, fileName }, null),
				ScriptMessageFindingKind.DuplicateMessage => ("MsgDuplicate", new Object?[] { finding.Number, fileName }, null),
				ScriptMessageFindingKind.EmptyMessage => ("MsgEmpty", new Object?[] { finding.Number, fileName }, null),
				ScriptMessageFindingKind.MissingFemaleVariant => ("MsgFemaleMissing", new Object?[] { finding.Number, finding.Number + femaleOffset, fileName }, null),
				ScriptMessageFindingKind.MessageFileMissing => ("MsgNoFile", new Object?[] { Path.GetFileName(scriptPath), finding.Count }, ProblemAction.CreateMessageFile(scriptPath)),
				ScriptMessageFindingKind.UnresolvedUsages => ("MsgUnresolved", new Object?[] { finding.Count }, null),
				_ => ("MsgNoUsage", new Object?[] { fileName, finding.Count }, null),
			};

			return new Problem
			{
				Severity = finding.Severity switch
				{
					ScriptMessageSeverity.Error => ProblemSeverity.Error,
					ScriptMessageSeverity.Warning => ProblemSeverity.Warning,
					_ => ProblemSeverity.Info,
				},
				Source = ProblemSource.Message,
				MessageKey = KeyPrefix + name,
				MessageArguments = arguments,
				HintKey = KeyPrefix + name + "Hint",
				File = finding.InScript || messageFilePath is null ? scriptPath : messageFilePath,
				Line = finding.Line,
				Column = finding.Column,
				Length = finding.Length,
				Action = action,
				Owner = owner,
				Origin = ProblemOrigins.MessageConsistency,
			};
		}
	}
}