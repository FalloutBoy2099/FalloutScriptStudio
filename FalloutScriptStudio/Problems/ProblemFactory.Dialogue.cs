namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Core.Dialogue;

	/// <summary>The problems of the check of the dialogue of a script.</summary>
	public static partial class ProblemFactory
	{
		private const Int32 MaxListedNames = 6;
		private const Int32 MaxListedNumbers = 8;

		/// <summary>Creates the problem of one finding of <see cref="DialogueQualityChecker"/>.</summary>
		/// <param name="finding">The finding.</param>
		/// <param name="scriptPath">The script that was checked.</param>
		/// <param name="owner">The script document.</param>
		/// <returns>The problem; a click goes to the line of the script.</returns>
		public static Problem DialogueQuality(DialogueQualityFinding finding, String scriptPath, Object? owner)
		{
			ArgumentNullException.ThrowIfNull(finding);
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);

			(String name, Object?[] arguments) = finding.Code switch
			{
				DialogueQualityCode.UnreachableNode => ("DlgUnreachable", new Object?[] { finding.Node }),
				DialogueQualityCode.DeadEnd => ("DlgDeadEnd", new Object?[] { finding.Node }),
				DialogueQualityCode.UndefinedTarget => ("DlgUndefinedTarget", new Object?[] { finding.Node, finding.RelatedNode }),
				DialogueQualityCode.ExitlessCycle => ("DlgCycle", new Object?[] { ListNames(finding.AffectedNodes), finding.Count }),
				DialogueQualityCode.NodeWithoutText => ("DlgNoText", new Object?[] { finding.Node, finding.Count }),
				DialogueQualityCode.DuplicateTarget => ("DlgDuplicateTarget", new Object?[] { finding.Node, finding.RelatedNode }),
				DialogueQualityCode.MissingMessages => ("DlgMissingMessages", new Object?[] { finding.Node, ListNumbers(finding.Numbers) }),
				DialogueQualityCode.TooManyOptions => ("DlgTooManyOptions", new Object?[] { finding.Node, finding.Count }),
				_ => ("DlgDuplicateNumber", new Object?[] { finding.Node, finding.Numbers.Count > 0 ? finding.Numbers[0] : 0 }),
			};

			return new Problem
			{
				Severity = finding.Severity switch
				{
					DialogueQualitySeverity.Error => ProblemSeverity.Error,
					DialogueQualitySeverity.Warning => ProblemSeverity.Warning,
					_ => ProblemSeverity.Info,
				},
				Source = ProblemSource.Parse,
				MessageKey = KeyPrefix + name,
				MessageArguments = arguments,
				HintKey = KeyPrefix + name + "Hint",
				HintArguments = arguments,
				File = scriptPath,
				Line = finding.Line,
				Owner = owner,
				Origin = ProblemOrigins.DialogueQuality,
			};
		}

		private static String ListNames(IReadOnlyList<String> names)
		{
			String list = String.Join(", ", names.Take(MaxListedNames));
			return names.Count > MaxListedNames ? list + ", ..." : list;
		}

		private static String ListNumbers(IReadOnlyList<Int32> numbers)
		{
			String list = String.Join(", ", numbers.Take(MaxListedNumbers).Select(number => number.ToString(CultureInfo.InvariantCulture)));
			return numbers.Count > MaxListedNumbers ? list + ", ..." : list;
		}
	}
}