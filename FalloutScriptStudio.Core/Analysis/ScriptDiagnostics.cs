namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Threading;

	/// <summary>
	/// The semantic checks of an SSL script, done while the user types: the mistakes that the compiler (sslc) would only report when the
	/// script is compiled. The analysis is pure: it works on the text, on what <see cref="ScriptEnvironment"/> knows about the headers and the
	/// macros, and never touches the disk.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The <c>SSL1xxx</c> rules (<see cref="ScriptDiagnosticCodes"/>) are rules sslc enforces, found by compiling small scripts with the real
	/// compiler: a wrong number of arguments (default values and forward declarations taken into account), an unknown name, a procedure that is
	/// used before its definition or forward declaration (sslc reads the file once, from top to bottom), a variable used before its declaration
	/// or outside its procedure, an assignment to a constant, a procedure or variable declared twice, a <c>begin</c> without <c>end</c> and an
	/// <c>end</c> without <c>begin</c>, an <c>if</c> without <c>then</c>, a <c>while</c> without <c>do</c>, a statement that calls a procedure
	/// without <c>call</c>, a procedure that is declared and used but never defined. A missing <c>return</c> is not an error in SSL and is never
	/// reported.
	/// </para>
	/// <para>
	/// The rules never report a script the compiler accepts as an error. When the analysis cannot be sure (a header that could not be read, a
	/// macro that may declare names, a block that is compiled only under a condition) it says nothing, or it lowers the severity to a warning.
	/// </para>
	/// </remarks>
	public static class ScriptDiagnostics
	{
		/// <summary>Analyses the text of a script.</summary>
		/// <param name="text">The text, with any line break style.</param>
		/// <param name="environment">The macros, headers and function descriptions; nothing is assumed when <see langword="null"/>.</param>
		/// <param name="options">What to report; the defaults when <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the analysis.</param>
		/// <returns>The diagnostics in text order.</returns>
		/// <exception cref="OperationCanceledException">The analysis was cancelled.</exception>
		public static IReadOnlyList<ScriptDiagnostic> Analyze(String text, ScriptEnvironment? environment = null, ScriptDiagnosticOptions? options = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(text);
			return Analyze(SslTokenizer.Tokenize(text), environment, options, cancellationToken);
		}

		/// <summary>Analyses tokens.</summary>
		/// <param name="tokens">The tokens of a script text.</param>
		/// <param name="environment">The macros, headers and function descriptions; nothing is assumed when <see langword="null"/>.</param>
		/// <param name="options">What to report; the defaults when <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the analysis.</param>
		/// <returns>The diagnostics in text order.</returns>
		/// <exception cref="OperationCanceledException">The analysis was cancelled.</exception>
		public static IReadOnlyList<ScriptDiagnostic> Analyze(SslTokenStream tokens, ScriptEnvironment? environment = null, ScriptDiagnosticOptions? options = null, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(tokens);
			ScriptAnalyzer analyzer = new ScriptAnalyzer(tokens, environment ?? ScriptEnvironment.Empty, options ?? new ScriptDiagnosticOptions(), cancellationToken);
			return analyzer.Analyze();
		}

		/// <summary>Lists the paths of the <c>#include</c> directives of a script, as written.</summary>
		/// <param name="tokens">The tokens of the script.</param>
		/// <returns>The paths, in order.</returns>
		public static IReadOnlyList<String> FindIncludes(SslTokenStream tokens)
		{
			ArgumentNullException.ThrowIfNull(tokens);
			return ScriptAnalyzer.FindIncludes(tokens);
		}

		/// <summary>Finds the diagnostic that covers an offset; when several do, the most serious one.</summary>
		/// <param name="diagnostics">The diagnostics of a text.</param>
		/// <param name="offset">The offset, for example of the caret.</param>
		/// <param name="mustHaveFix">Whether only diagnostics with a quick fix count.</param>
		/// <returns>The diagnostic, or <see langword="null"/>.</returns>
		public static ScriptDiagnostic? FindAt(IReadOnlyList<ScriptDiagnostic> diagnostics, Int32 offset, Boolean mustHaveFix = false)
		{
			ArgumentNullException.ThrowIfNull(diagnostics);

			ScriptDiagnostic? best = null;
			foreach (ScriptDiagnostic diagnostic in diagnostics)
			{
				if (!diagnostic.Contains(offset) || (mustHaveFix && diagnostic.Fix is null))
				{
					continue;
				}

				if (best is null || diagnostic.Severity > best.Severity)
				{
					best = diagnostic;
				}
			}

			return best;
		}
	}
}