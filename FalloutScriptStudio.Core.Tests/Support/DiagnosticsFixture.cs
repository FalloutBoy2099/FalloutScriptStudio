namespace FalloutScriptStudio.TestSupport
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;

	/// <summary>The environment and the helpers shared by the tests of <see cref="ScriptDiagnostics"/>.</summary>
	internal static class DiagnosticsFixture
	{
		private static readonly Lazy<FunctionSignatureCatalog> _shipped = new Lazy<FunctionSignatureCatalog>(
			() => FunctionSignatureCatalog.LoadFromFiles(Description("opcodes.txt"), Description("functions.ini")));

		private static String Description(String fileName)
		{
			return System.IO.Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions", fileName);
		}

		/// <summary>Gets the descriptions of the functions that the editor ships.</summary>
		public static FunctionSignatureCatalog ShippedCatalog => _shipped.Value;

		/// <summary>Gets an environment with the shipped function descriptions and nothing else.</summary>
		public static ScriptEnvironment ShippedEnvironment { get; } = new ScriptEnvironment { Functions = ShippedCatalog };

		/// <summary>Analyses a script with the shipped function descriptions.</summary>
		public static IReadOnlyList<ScriptDiagnostic> Analyze(String text)
		{
			return ScriptDiagnostics.Analyze(text, ShippedEnvironment);
		}

		/// <summary>Analyses lines joined with CRLF.</summary>
		public static IReadOnlyList<ScriptDiagnostic> AnalyzeLines(params String[] lines)
		{
			return Analyze(String.Join("\r\n", lines) + "\r\n");
		}

		/// <summary>Gets the codes of the diagnostics of warning level or above, without duplicates, in order.</summary>
		public static String[] Codes(IEnumerable<ScriptDiagnostic> diagnostics)
		{
			return diagnostics.Where(d => d.Severity >= ScriptDiagnosticSeverity.Warning).Select(d => d.Code).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal).ToArray();
		}
	}
}