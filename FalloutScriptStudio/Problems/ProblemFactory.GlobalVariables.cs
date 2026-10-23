namespace FalloutScriptStudio.Problems
{
	using System;
	using Core.Globals;

	/// <summary>The problems of the check of the global variables of the project.</summary>
	public static partial class ProblemFactory
	{
		/// <summary>Creates the problem of one finding of <see cref="GlobalVariableAnalyzer"/>.</summary>
		/// <param name="finding">The finding.</param>
		/// <returns>
		/// The problem, located at the use the finding points at. Written but never read and probable typos are information, the other findings
		/// are warnings. It belongs to no document: the next check of the global variables replaces it.
		/// </returns>
		public static Problem GlobalVariable(GlobalFinding finding)
		{
			ArgumentNullException.ThrowIfNull(finding);

			(String name, Object?[] arguments) = finding.Kind switch
			{
				GlobalFindingKind.WrittenNeverRead => ("GlobalWrittenNeverRead", new Object?[] { finding.Variable, finding.Number }),
				GlobalFindingKind.ReadNeverWritten => ("GlobalReadNeverWritten", new Object?[] { finding.Variable, finding.Number }),
				GlobalFindingKind.TypeConflict => ("GlobalTypeConflict", new Object?[] { finding.Variable }),
				GlobalFindingKind.SimilarName => ("GlobalSimilarName", new Object?[] { finding.Variable, finding.Other }),
				GlobalFindingKind.NameLength => ("GlobalNameLength", new Object?[] { finding.Variable, finding.Number, GlobalVariableAnalyzer.SfallNameLength }),
				_ => ("GlobalPrefixCollision", new Object?[] { finding.Variable, finding.Other, finding.Number }),
			};

			return new Problem
			{
				Severity = finding.Severity == GlobalFindingSeverity.Warning ? ProblemSeverity.Warning : ProblemSeverity.Info,
				Source = ProblemSource.Parse,
				MessageKey = KeyPrefix + name,
				MessageArguments = arguments,
				HintKey = KeyPrefix + name + "Hint",
				HintArguments = arguments,
				File = finding.File,
				Line = finding.Line,
				Origin = ProblemOrigins.GlobalVariables,
			};
		}
	}
}