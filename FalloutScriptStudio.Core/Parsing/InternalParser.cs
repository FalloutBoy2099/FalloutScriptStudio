namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	/// <summary>
	/// The managed (text based) parser: finds the procedures of a script without the native <c>parser.dll</c>
	/// (legacy <c>ParserInternal</c>). It is used when the native parser is disabled, unavailable or fails.
	/// </summary>
	/// <remarks>
	/// The class is stateless and thread-safe. No method changes the objects it is given; operations that update a
	/// <see cref="ProgramInfo"/> return a new snapshot. Line numbers are 1-based. The internal parser knows nothing about
	/// variables, references or arguments, so the procedures it produces have none.
	/// </remarks>
	public sealed class InternalParser
	{
		/// <summary>
		/// Finds the procedures of a script.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="scriptFile">Path of the script; may be <c>null</c> for an unsaved script.</param>
		/// <param name="diagnostics">Receives errors and warnings (unclosed procedures, declarations without a body); may be <c>null</c>.</param>
		/// <returns>The procedures in order of first appearance.</returns>
		public IReadOnlyList<ScriptProcedure> ParseProcedures(String text, String? scriptFile, ParseDiagnostics? diagnostics = null)
		{
			String fullPath = GetFullPath(scriptFile);
			String fileName = String.IsNullOrEmpty(scriptFile) ? String.Empty : Path.GetFileName(scriptFile).ToLowerInvariant();

			return ProcedureScanner.Scan(text, diagnostics)
				.Select(block => new ScriptProcedure(block.Name)
				{
					Flags = block.Flags,
					DeclaredFile = fullPath,
					StartFile = fullPath,
					FileName = fileName,
					DeclaredLine = block.DeclarationLine > 0 ? block.DeclarationLine : block.BeginLine,
					DefinedLine = block.BeginLine,
					StartLine = block.BeginLine,
					EndLine = block.EndLine,
				})
				.ToList();
		}

		/// <summary>
		/// Parses a script from scratch.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="scriptFile">Path of the script; may be <c>null</c> for an unsaved script.</param>
		/// <param name="globalMacros">The global macro registry to attach; an empty one is created when <c>null</c>.</param>
		/// <param name="diagnostics">Receives errors and warnings; may be <c>null</c>.</param>
		/// <returns>A snapshot marked as parsed, without native data.</returns>
		public ProgramInfo Parse(String text, String? scriptFile, MacroRegistry? globalMacros = null, ParseDiagnostics? diagnostics = null)
		{
			IReadOnlyList<ScriptProcedure> procedures = this.ParseProcedures(text, scriptFile, diagnostics);
			return new ProgramInfo(procedures, Array.Empty<ScriptVariable>(), null, globalMacros, String.IsNullOrEmpty(scriptFile) ? null : GetFullPath(scriptFile))
				.WithFlags(isParsed: true);
		}

		/// <summary>
		/// Updates a previous result with the procedures of the current text, keeping the data the text scan cannot provide
		/// (references, variables, arguments) of procedures that still exist (legacy <c>UpdateProcsPI</c>).
		/// </summary>
		/// <param name="previous">The previous result.</param>
		/// <param name="text">The current script text.</param>
		/// <param name="scriptFile">Path of the script.</param>
		/// <param name="diagnostics">Receives errors and warnings; may be <c>null</c>.</param>
		/// <returns>
		/// A new snapshot: procedures found in the text get their new lines, procedures of this script that disappeared are
		/// removed, new procedures are added, duplicated names are resolved and the result is ordered by position.
		/// </returns>
		public ProgramInfo MergeProcedures(ProgramInfo previous, String text, String? scriptFile, ParseDiagnostics? diagnostics = null)
		{
			String fullPath = GetFullPath(scriptFile);
			IReadOnlyList<ScriptProcedure> update = this.ParseProcedures(text, scriptFile, diagnostics);
			List<ScriptProcedure> merged = previous.Procedures.ToList();

			for (Int32 i = 0; i < merged.Count; i++)
			{
				ScriptProcedure current = merged[i];
				ScriptProcedure? match = update.FirstOrDefault(u => IsSameProcedure(u, current));
				if (match is not null)
				{
					merged[i] = current.WithLocations(match.DeclaredLine, match.DefinedLine, match.StartLine, match.EndLine);
				}
				else if (current.IsDefinedIn(fullPath))
				{
					merged.RemoveAt(i--);
				}
			}

			for (Int32 i = 0; i < update.Count; i++)
			{
				ScriptProcedure added = update[i];
				if (!merged.Any(m => IsSameProcedure(added, m)))
				{
					merged.Insert(Math.Min(i, merged.Count), added);
				}
			}

			ResolveDuplicatedNames(merged, fullPath);

			return previous.WithProcedures(merged.OrderBy(p => p.SortLine));
		}

		/// <summary>
		/// Quickly updates the line numbers of the procedures of the script from the current text
		/// (legacy <c>UpdateProcInfo</c>); procedures are neither added nor removed.
		/// </summary>
		/// <param name="info">The result to update.</param>
		/// <param name="text">The current script text.</param>
		/// <param name="scriptFile">Path of the script.</param>
		/// <returns>A new snapshot.</returns>
		public ProgramInfo RefreshProcedureLocations(ProgramInfo info, String text, String? scriptFile)
		{
			String fullPath = GetFullPath(scriptFile);
			Dictionary<String, ProcedureBlock> blocks = ProcedureScanner.Scan(text, null)
				.GroupBy(b => b.Name, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

			var procedures = new List<ScriptProcedure>(info.Procedures.Count);
			foreach (ScriptProcedure procedure in info.Procedures)
			{
				if (String.Equals(procedure.DeclaredFile, fullPath, StringComparison.OrdinalIgnoreCase)
					&& blocks.TryGetValue(procedure.Name, out ProcedureBlock? block))
				{
					Int32 declared = block.DeclarationLine > 0 ? block.DeclarationLine : block.BeginLine;
					procedures.Add(procedure.WithLocations(declared, block.BeginLine, block.BeginLine, block.EndLine));
				}
				else
				{
					procedures.Add(procedure);
				}
			}

			return info.WithProcedures(procedures);
		}

		/// <summary>
		/// Re-binds everything that belongs to the script to a new file path after "Save As" (legacy <c>UpdatePI</c>).
		/// Only entities declared in the previous path of the script (<see cref="ProgramInfo.ScriptFilePath"/>) are changed.
		/// </summary>
		/// <param name="info">The result to update.</param>
		/// <param name="newFilePath">The new path of the script.</param>
		/// <returns>A new snapshot with <see cref="ProgramInfo.NeedsDataRefresh"/> cleared.</returns>
		public ProgramInfo RebindToFile(ProgramInfo info, String newFilePath)
		{
			String newPath = GetFullPath(newFilePath);
			String oldPath = info.ScriptFilePath ?? String.Empty;

			Boolean IsOld(String? file) => String.Equals(file ?? String.Empty, oldPath, StringComparison.OrdinalIgnoreCase);
			IReadOnlyList<SourceReference> Rebind(IReadOnlyList<SourceReference> references) =>
				references.Select(r => IsOld(r.File) ? r with { File = newPath } : r).ToArray();

			var procedures = new List<ScriptProcedure>(info.Procedures.Count);
			foreach (ScriptProcedure procedure in info.Procedures)
			{
				if (!IsOld(procedure.DeclaredFile))
				{
					procedures.Add(procedure);
					continue;
				}

				ScriptVariable[] locals = procedure.Variables
					.Select(v => v.WithFile(IsOld(v.DeclaredFile) ? newPath : v.DeclaredFile, Rebind(v.References)))
					.ToArray();
				procedures.Add(procedure.WithFile(newPath, Rebind(procedure.References), locals));
			}

			ScriptVariable[] variables = info.Variables
				.Select(v => IsOld(v.DeclaredFile) ? v.WithFile(newPath, Rebind(v.References)) : v)
				.ToArray();

			var macros = new SortedDictionary<String, ScriptMacro>(StringComparer.Ordinal);
			foreach (KeyValuePair<String, ScriptMacro> entry in info.ScriptMacros)
			{
				ScriptMacro macro = entry.Value;
				macros[entry.Key] = IsOld(macro.DeclaredFile)
					? new ScriptMacro(macro.Token, macro.DefinitionName, macro.Code, newPath, macro.DeclaredLine, macro.Description)
					: macro;
			}

			return new ProgramInfo(procedures, variables, macros, info.GlobalMacros, newPath)
				.WithFlags(info.IsParsed, info.HasNativeData, info.HasParseError, needsDataRefresh: false);
		}

		private static Boolean IsSameProcedure(ScriptProcedure a, ScriptProcedure b)
		{
			return String.Equals(a.NormalizedName, b.NormalizedName, StringComparison.Ordinal)
				&& String.Equals(a.DeclaredFile, b.DeclaredFile, StringComparison.OrdinalIgnoreCase);
		}

		private static void ResolveDuplicatedNames(List<ScriptProcedure> procedures, String scriptPath)
		{
			for (Int32 i = 0; i < procedures.Count; i++)
			{
				for (Int32 n = i + 1; n < procedures.Count; n++)
				{
					if (!String.Equals(procedures[i].NormalizedName, procedures[n].NormalizedName, StringComparison.Ordinal))
					{
						continue;
					}

					if (procedures[i].IsDefinedIn(scriptPath))
					{
						procedures[i] = procedures[i].WithName(procedures[i].Name + "_" + procedures[i].DefinedLine);
					}
					else
					{
						procedures.RemoveAt(i--);
					}

					break;
				}
			}
		}

		private static String GetFullPath(String? path)
		{
			if (String.IsNullOrEmpty(path))
			{
				return String.Empty;
			}

			try
			{
				return Path.GetFullPath(path);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return path;
			}
		}
	}
}