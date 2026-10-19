namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Linq;
	using Core.Analysis;

	/// <summary>The problems of the semantic analysis of a script.</summary>
	public static partial class ProblemFactory
	{
		/// <summary>Creates the problem of one finding of <see cref="ScriptDiagnostics"/> (a wrong number of arguments, an unknown name...).</summary>
		/// <param name="diagnostic">The finding.</param>
		/// <param name="file">The script, or <see langword="null"/> when it was never saved.</param>
		/// <param name="owner">The document of the script.</param>
		/// <returns>
		/// The problem: its message and hint are the keys of the rule, its place is the range of the finding, its action applies the quick fix
		/// of the finding when it has one and the script has a file.
		/// </returns>
		public static Problem ScriptDiagnostic(Core.Analysis.ScriptDiagnostic diagnostic, String? file, Object? owner)
		{
			ArgumentNullException.ThrowIfNull(diagnostic);

			return new Problem
			{
				Severity = diagnostic.Severity switch
				{
					ScriptDiagnosticSeverity.Error => ProblemSeverity.Error,
					ScriptDiagnosticSeverity.Warning => ProblemSeverity.Warning,
					_ => ProblemSeverity.Info,
				},
				Source = ProblemSource.Parse,
				MessageKey = diagnostic.MessageKey,
				MessageArguments = diagnostic.MessageArguments.Cast<Object?>().ToArray(),
				HintKey = diagnostic.HintKey,
				File = file,
				Line = diagnostic.Line,
				Column = diagnostic.Column,
				Length = diagnostic.Length,
				Action = diagnostic.Fix is { } fix && file is not null ? ProblemAction.ApplyQuickFix(file, diagnostic.Line, diagnostic.Column, fix.Id, fix.Argument) : null,
				Owner = owner,
				Origin = ProblemOrigins.LiveDiagnostics,
			};
		}
	}
}