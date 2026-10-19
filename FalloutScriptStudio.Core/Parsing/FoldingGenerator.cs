namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// Computes the collapsible regions of a script (legacy <c>CodeFolder.GenerateFoldMarkers</c>) as plain data;
	/// the editor turns them into its own folding objects.
	/// </summary>
	public sealed class FoldingGenerator
	{
		/// <summary>The title of the declaration region.</summary>
		public const String DeclarationRegionTitle = " - Declaration Region - ";

		private readonly ProcedureLocator _locator = new ProcedureLocator();

		/// <summary>
		/// Generates the folding regions.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="fileName">Name (or path) of the script; only procedures whose body is in this file get a region.</param>
		/// <param name="procedures">The procedures known for the script (including those of included files).</param>
		/// <returns>
		/// One region per procedure body, the declaration region (for <c>.ssl</c> files), and the <c>variable begin ... end</c>
		/// and <c>#if ... #endif</c> blocks, ordered by start line.
		/// </returns>
		public IReadOnlyList<FoldingRegion> Generate(String text, String fileName, IEnumerable<ScriptProcedure> procedures)
		{
			String name = Path.GetFileName(fileName).ToLowerInvariant();
			var regions = new List<FoldingRegion>();
			Int32 firstStart = -1;

			foreach (ScriptProcedure procedure in procedures)
			{
				String bodyFile = Path.GetFileName(procedure.StartFile).ToLowerInvariant();
				if (bodyFile != name || procedure.StartLine >= procedure.EndLine)
				{
					continue;
				}

				if (firstStart == -1 || procedure.StartLine < firstStart)
				{
					firstStart = procedure.StartLine;
				}

				regions.Add(new FoldingRegion(procedure.StartLine, procedure.EndLine, FoldingKind.Procedure, " " + procedure.Name.ToUpperInvariant() + " "));
			}

			if (regions.Count > 0 && Path.GetExtension(name) == ".ssl")
			{
				LineRange declaration = this._locator.GetDeclarationRegion(text, firstStart);
				Int32 end = declaration.EndLine < 0 ? firstStart - 2 : declaration.EndLine;
				if (end > declaration.StartLine)
				{
					regions.Add(new FoldingRegion(declaration.StartLine, end, FoldingKind.Region, DeclarationRegionTitle));
				}
			}

			regions.AddRange(FoldingScanner.Scan(text));
			return regions.OrderBy(r => r.StartLine).ThenByDescending(r => r.EndLine).ToList();
		}
	}
}