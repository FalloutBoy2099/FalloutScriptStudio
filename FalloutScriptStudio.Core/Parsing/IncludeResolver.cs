namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	/// <summary>
	/// Resolves <c>#include</c> paths to files on disk (legacy <c>ParserInternal.GetIncludePath</c> and <c>GetAllIncludes</c>).
	/// </summary>
	/// <remarks>
	/// Search order: the directory of the including script, then the headers directory (when include search is enabled),
	/// then the program directory. The object is immutable and stateless.
	/// </remarks>
	public sealed class IncludeResolver
	{
		private readonly String? _headersDirectory;
		private readonly String? _programDirectory;

		/// <summary>
		/// Initializes a new instance of the <see cref="IncludeResolver"/> class.
		/// </summary>
		/// <param name="headersDirectory">Directory with the game headers, or <c>null</c> when include search is disabled.</param>
		/// <param name="programDirectory">Directory of the application (lowest priority), or <c>null</c>.</param>
		public IncludeResolver(String? headersDirectory, String? programDirectory)
		{
			this._headersDirectory = String.IsNullOrEmpty(headersDirectory) ? null : headersDirectory;
			this._programDirectory = String.IsNullOrEmpty(programDirectory) ? null : programDirectory;
		}

		/// <summary>
		/// Extracts the file name from an <c>#include "file"</c> line.
		/// </summary>
		/// <param name="line">The (trimmed) line.</param>
		/// <returns>The path between the quotes, or <c>null</c> when the line has no valid quoted path.</returns>
		public static String? ExtractIncludePath(String line)
		{
			String[] parts = line.Split('"');
			if (parts.Length < 2 || parts[1].IndexOfAny(Path.GetInvalidPathChars()) != -1)
			{
				return null;
			}

			return parts[1];
		}

		/// <summary>
		/// Finds the file referenced by an include directive.
		/// </summary>
		/// <param name="includePath">The path as written in the directive.</param>
		/// <param name="scriptDirectory">Directory of the including script; may be <c>null</c> or empty for an unsaved script.</param>
		/// <returns>
		/// The full path of the first existing candidate. When the file cannot be found anywhere the path relative to the
		/// script directory is returned (the legacy code returned the program-directory path in that case).
		/// </returns>
		public String Resolve(String includePath, String? scriptDirectory)
		{
			try
			{
				String scriptRelative = String.IsNullOrEmpty(scriptDirectory)
					? Path.GetFullPath(includePath)
					: Path.GetFullPath(Path.Combine(scriptDirectory, includePath));

				String candidate = scriptRelative;
				if (this._headersDirectory is not null && !Path.IsPathRooted(includePath) && !File.Exists(scriptRelative))
				{
					candidate = Path.GetFullPath(Path.Combine(this._headersDirectory, includePath));
				}

				if (File.Exists(candidate))
				{
					return candidate;
				}

				if (this._programDirectory is not null)
				{
					String programCandidate = Path.GetFullPath(Path.Combine(this._programDirectory, includePath));
					if (File.Exists(programCandidate))
					{
						return programCandidate;
					}
				}

				return scriptRelative;
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return includePath;
			}
		}

		/// <summary>
		/// Lists the files included by a script (legacy <c>GetAllIncludes</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="scriptFilePath">Full path of the script; may be <c>null</c> for an unsaved script.</param>
		/// <returns>The resolved full paths of every <c>#include</c> line, in order of appearance.</returns>
		public IReadOnlyList<String> GetAllIncludes(String text, String? scriptFilePath)
		{
			var result = new List<String>();
			String? directory = String.IsNullOrEmpty(scriptFilePath) ? null : Path.GetDirectoryName(scriptFilePath);
			var comments = new CommentTracker();

			foreach (String rawLine in SslSyntax.SplitLines(text))
			{
				String line = rawLine.Trim();
				if (line.Length == 0)
				{
					continue;
				}

				if (comments.ShouldSkip(ref line))
				{
					continue;
				}

				if (!line.StartsWith(SslKeywords.Include, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				String? path = ExtractIncludePath(line);
				if (path is not null)
				{
					result.Add(this.Resolve(path, directory));
				}
			}

			return result;
		}
	}
}
