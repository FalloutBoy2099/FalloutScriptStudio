namespace FalloutScriptStudio.Problems
{
	using System;
	using System.IO;

	/// <summary>The problems of "Format Project": the files that were not formatted.</summary>
	public static partial class ProblemFactory
	{
		/// <summary>A file that changed after the preview, so it was left alone.</summary>
		/// <param name="path">The file (or the name of a document that was never saved).</param>
		/// <returns>The problem.</returns>
		public static Problem FormatFileChanged(String path)
		{
			return CreateFormatProblem(path, "FormatFileChanged", Array.Empty<Object?>());
		}

		/// <summary>A read-only file that was left alone.</summary>
		/// <param name="path">The file.</param>
		/// <returns>The problem.</returns>
		public static Problem FormatReadOnly(String path)
		{
			return CreateFormatProblem(path, "FormatReadOnly", Array.Empty<Object?>());
		}

		/// <summary>A file that could not be read, is too big, or could not be written.</summary>
		/// <param name="path">The file.</param>
		/// <param name="reason">The message of the error.</param>
		/// <returns>The problem.</returns>
		public static Problem FormatFailed(String path, String reason)
		{
			ArgumentNullException.ThrowIfNull(reason);
			return CreateFormatProblem(path, "FormatFailed", new Object?[] { reason });
		}

		private static Problem CreateFormatProblem(String path, String name, Object?[] extra)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			Boolean isFile = Path.IsPathRooted(path);
			Object?[] arguments = new Object?[extra.Length + 1];
			arguments[0] = isFile ? Path.GetFileName(path) : path;
			Array.Copy(extra, 0, arguments, 1, extra.Length);
			return new Problem
			{
				Severity = ProblemSeverity.Warning,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + name,
				MessageArguments = arguments,
				HintKey = KeyPrefix + name + "Hint",
				File = isFile ? path : null,
				Action = isFile ? ProblemAction.OpenFile(path) : null,
				Origin = ProblemOrigins.FormatProject,
			};
		}
	}
}