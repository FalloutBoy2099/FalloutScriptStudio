namespace FalloutScriptStudio.Problems
{
	using System;
	using System.IO;
	using Services.ScriptTools.Replace;

	/// <summary>The problems of "Replace in Files": the files that were left unchanged.</summary>
	public static partial class ProblemFactory
	{
		/// <summary>A file in which "Replace in Files" made no replacement.</summary>
		/// <param name="path">The file (or the name of a document that was never saved).</param>
		/// <param name="reason">Why it was left unchanged.</param>
		/// <param name="matches">The number of replacements that were not made in it (0 when it is not known).</param>
		/// <param name="detail">The message of the error behind it, or an empty text.</param>
		/// <returns>The problem.</returns>
		public static Problem ReplaceFileSkipped(String path, ReplaceFailureReason reason, Int32 matches, String detail)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(detail);

			String name = "Replace" + reason;
			Boolean isFile = Path.IsPathRooted(path);
			return new Problem
			{
				Severity = ProblemSeverity.Warning,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + name,
				MessageArguments = new Object?[] { isFile ? Path.GetFileName(path) : path, matches, detail },
				HintKey = KeyPrefix + name + "Hint",
				File = isFile ? path : null,
				Action = isFile ? ProblemAction.OpenFile(path) : null,
				Origin = ProblemOrigins.ReplaceInFiles,
			};
		}
	}
}