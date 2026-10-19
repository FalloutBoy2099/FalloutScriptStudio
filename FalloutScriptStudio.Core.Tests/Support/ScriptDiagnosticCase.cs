namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// A small script together with what the real compiler does with it and what <c>ScriptDiagnostics</c> must report. The same table
	/// is used twice: without the compiler it checks the analysis, with the real <c>compile.exe</c> it checks that the table tells the
	/// truth (<c>RealScriptDiagnosticsTests</c>), so the rules cannot drift away from what sslc enforces.
	/// </summary>
	internal sealed class ScriptDiagnosticCase
	{
		private ScriptDiagnosticCase(String name, String rule, Boolean compilerRejects, String[] expected, String[] lines, Boolean canCompile)
		{
			this.Name = name;
			this.Rule = rule;
			this.CompilerRejects = compilerRejects;
			this.Expected = expected;
			this.Lines = lines;
			this.CanCompile = canCompile;
		}

		/// <summary>Gets the unique name of the case.</summary>
		public String Name { get; }

		/// <summary>Gets the rule the case is about (a code, or <c>none</c> for a script that must not be reported).</summary>
		public String Rule { get; }

		/// <summary>Gets a value indicating whether sslc rejects the script.</summary>
		public Boolean CompilerRejects { get; }

		/// <summary>Gets the codes of the diagnostics of warning level or above that the analysis must report, and no others.</summary>
		public String[] Expected { get; }

		/// <summary>Gets the lines of the script.</summary>
		public String[] Lines { get; }

		/// <summary>Gets a value indicating whether the script can be given to the real compiler (it needs no header that does not exist).</summary>
		public Boolean CanCompile { get; }

		/// <summary>Gets the script with CRLF line breaks.</summary>
		public String Crlf => String.Join("\r\n", this.Lines) + "\r\n";

		/// <summary>Gets the script with LF line breaks.</summary>
		public String Lf => String.Join("\n", this.Lines) + "\n";

		/// <summary>Creates a case that sslc rejects because of one or several rules.</summary>
		public static ScriptDiagnosticCase Rejected(String codes, String name, params String[] lines)
		{
			return new ScriptDiagnosticCase(name, codes, true, codes.Split(',', StringSplitOptions.RemoveEmptyEntries), lines, true);
		}

		/// <summary>Creates a case that sslc accepts and that must not be reported at all.</summary>
		public static ScriptDiagnosticCase Accepted(String name, params String[] lines)
		{
			return new ScriptDiagnosticCase(name, "none", false, Array.Empty<String>(), lines, true);
		}

		/// <summary>Creates a case that sslc accepts but that deserves a warning.</summary>
		public static ScriptDiagnosticCase Warned(String codes, String name, params String[] lines)
		{
			return new ScriptDiagnosticCase(name, codes, false, codes.Split(',', StringSplitOptions.RemoveEmptyEntries), lines, true);
		}

		/// <summary>Creates a case that is not given to the compiler (it uses a header the repository does not have).</summary>
		public static ScriptDiagnosticCase Offline(String codes, String name, params String[] lines)
		{
			return new ScriptDiagnosticCase(name, codes.Length == 0 ? "none" : codes, codes.Length > 0, codes.Split(',', StringSplitOptions.RemoveEmptyEntries), lines, false);
		}

		/// <summary>Gets every case.</summary>
		public static IReadOnlyList<ScriptDiagnosticCase> All { get; } = ScriptDiagnosticCases.Create();

		/// <summary>Gets the cases of a rule.</summary>
		public static IEnumerable<ScriptDiagnosticCase> ForRule(String rule)
		{
			return All.Where(c => String.Equals(c.Rule, rule, StringComparison.Ordinal));
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Name;
		}
	}
}