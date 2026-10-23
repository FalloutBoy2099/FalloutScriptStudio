namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using Core.Messages;

	/// <summary>The problems of the comparison of a message file with the file of another language.</summary>
	public static partial class ProblemFactory
	{
		/// <summary>Creates the problems of one row of a comparison: one for its status (unless the message is fine) and one for its issues.</summary>
		/// <param name="row">The row.</param>
		/// <param name="referencePath">The reference file.</param>
		/// <param name="targetPath">The target file.</param>
		/// <param name="issueNames">Gives the text of the issues of a row, in the language of the interface (the factory is not in charge of the interface texts of the window).</param>
		/// <returns>The problems; none for a message that is fine.</returns>
		public static IEnumerable<Problem> MessageComparison(MessageComparisonRow row, String referencePath, String targetPath, Func<MessageComparisonIssues, String> issueNames)
		{
			ArgumentNullException.ThrowIfNull(row);
			ArgumentException.ThrowIfNullOrEmpty(referencePath);
			ArgumentException.ThrowIfNullOrEmpty(targetPath);
			ArgumentNullException.ThrowIfNull(issueNames);

			String reference = Path.GetFileName(referencePath);
			String target = Path.GetFileName(targetPath);
			Boolean inTarget = row.Status != MessageComparisonStatus.MissingInTarget;
			String file = inTarget ? targetPath : referencePath;
			Int32 line = inTarget ? row.TargetLine : row.ReferenceLine;
			(String name, ProblemSeverity severity)? status = row.Status switch
			{
				MessageComparisonStatus.MissingInTarget => ("CmpMissing", ProblemSeverity.Warning),
				MessageComparisonStatus.ExtraInTarget => ("CmpExtra", ProblemSeverity.Info),
				MessageComparisonStatus.SameText => ("CmpSame", ProblemSeverity.Info),
				MessageComparisonStatus.PlaceholdersOnly => ("CmpPlaceholders", ProblemSeverity.Warning),
				MessageComparisonStatus.EmptyInTarget => ("CmpEmpty", ProblemSeverity.Warning),
				MessageComparisonStatus.PlayerOrDescription => ("CmpPlayer", ProblemSeverity.Info),
				_ => null,
			};

			if (status is { } found)
			{
				yield return Create(found.name, found.severity, new Object?[] { row.Number, target, reference }, file, line);
			}

			MessageComparisonIssues others = row.Issues & ~(MessageComparisonIssues.PlayerTabDiffers | MessageComparisonIssues.DescriptionDiffers);
			if (others != MessageComparisonIssues.None)
			{
				yield return Create("CmpIssue", ProblemSeverity.Warning, new Object?[] { row.Number, target, issueNames(others) }, file, line);
			}
		}

		private static Problem Create(String name, ProblemSeverity severity, Object?[] arguments, String file, Int32 line)
		{
			return new Problem
			{
				Severity = severity,
				Source = ProblemSource.Message,
				MessageKey = KeyPrefix + name,
				MessageArguments = arguments,
				HintKey = KeyPrefix + name + "Hint",
				HintArguments = arguments,
				File = file,
				Line = line,
				Origin = ProblemOrigins.MessageComparison,
			};
		}
	}
}