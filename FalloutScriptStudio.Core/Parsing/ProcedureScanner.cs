namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Stateless text scanner that finds the procedures of a script: declarations, <c>begin</c> lines and <c>end</c> lines
	/// (legacy <c>GetNamesProcedures</c> + <c>GetAllProceduresBlock</c>, merged into a single pass).
	/// </summary>
	internal static class ProcedureScanner
	{
		internal const String DiagnosticSource = "Internal Parser";

		/// <summary>Scans a script.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="diagnostics">Receives errors and warnings; may be <c>null</c>.</param>
		/// <returns>One block per procedure name, in order of first appearance. Lines are 1-based.</returns>
		internal static IReadOnlyList<ProcedureBlock> Scan(String text, ParseDiagnostics? diagnostics)
		{
			String[] cleaned = SslSyntax.SplitLines(text);
			var comments = new CommentTracker();
			var entries = new List<Entry>();
			var byName = new Dictionary<String, Entry>(StringComparer.OrdinalIgnoreCase);

			for (Int32 i = 0; i < cleaned.Length; i++)
			{
				String line = cleaned[i].TrimStart();
				Boolean skip = comments.ShouldSkip(ref line);
				cleaned[i] = line;
				if (skip)
				{
					continue;
				}

				line = line.Replace('\t', ' ');
				ProcedureFlags flags = SslSyntax.StripProcedureModifiers(ref line);
				cleaned[i] = line;
				if (!line.StartsWith(SslKeywords.Procedure, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				String name = line.Substring(SslKeywords.ProcedureLength).TrimStart();
				if (name.Length == 0)
				{
					continue;
				}

				name = SslSyntax.RemoveTrailingComment(name, 0);
				Boolean isDeclaration = false;
				Int32 index = name.IndexOf(';');
				if (index > 0)
				{
					name = name.Remove(index);
					isDeclaration = true;
				}
				else
				{
					index = name.IndexOf(" " + SslKeywords.Begin, StringComparison.OrdinalIgnoreCase);
					if (index > 0)
					{
						name = name.Remove(index);
					}
				}

				index = name.IndexOf('(');
				if (index > 0)
				{
					name = name.Remove(index);
				}

				name = name.TrimEnd();
				if (name.Length == 0 || name[0] == '/')
				{
					continue;
				}

				if (!byName.TryGetValue(name, out Entry? entry))
				{
					entry = new Entry(name);
					byName.Add(name, entry);
					entries.Add(entry);
				}

				entry.Flags |= flags;
				if (isDeclaration)
				{
					if (entry.DeclarationLine == -1)
					{
						entry.DeclarationLine = i + 1;
					}
				}
				else
				{
					if (!entry.HasBody)
					{
						entry.HasBody = true;
						entry.LinkedDeclarationLine = entry.DeclarationLine;
					}

					entry.BeginLine = i + 1;
				}
			}

			FindEndLines(entries, cleaned, diagnostics);
			ReportDeclarationsWithoutBody(entries, diagnostics);

			return entries
				.Select(e => new ProcedureBlock(
					e.Name,
					e.HasBody ? e.LinkedDeclarationLine : e.DeclarationLine,
					e.HasBody ? e.BeginLine : -1,
					e.EndLine,
					e.Flags))
				.ToList();
		}

		private static void FindEndLines(List<Entry> entries, String[] cleaned, ParseDiagnostics? diagnostics)
		{
			List<Entry> bodies = entries.Where(e => e.HasBody).OrderBy(e => e.BeginLine).ToList();
			for (Int32 i = 0; i < bodies.Count; i++)
			{
				Entry entry = bodies[i];
				Int32 beginIndex = entry.BeginLine - 1;
				Int32 lastIndex = i == bodies.Count - 1 ? cleaned.Length - 1 : bodies[i + 1].BeginLine - 1;

				for (Int32 j = lastIndex; j > beginIndex; j--)
				{
					String candidate = cleaned[j];
					if (!candidate.StartsWith(SslKeywords.End, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}

					if (candidate.Length > 3 && !Char.IsWhiteSpace(candidate[3]))
					{
						continue;
					}

					entry.EndLine = j + 1;
					break;
				}

				if (entry.EndLine == -1)
				{
					diagnostics?.Add(
						ParseDiagnosticSeverity.Error,
						DiagnosticSource,
						entry.BeginLine,
						"When parsing of procedure '" + entry.Name + "' of the keyword 'end' was not found.");
				}
			}
		}

		private static void ReportDeclarationsWithoutBody(List<Entry> entries, ParseDiagnostics? diagnostics)
		{
			if (diagnostics is null)
			{
				return;
			}

			foreach (Entry entry in entries.Where(e => !e.HasBody && (e.Flags & ProcedureFlags.Import) == ProcedureFlags.None))
			{
				diagnostics.Add(
					ParseDiagnosticSeverity.Warning,
					DiagnosticSource,
					entry.DeclarationLine,
					"When parsing the procedure '" + entry.Name + "' an unexpected error occurred, the construction of the code was not determined.");
			}
		}

		private sealed class Entry
		{
			internal Entry(String name)
			{
				this.Name = name;
			}

			internal String Name { get; }

			internal ProcedureFlags Flags { get; set; }

			internal Int32 DeclarationLine { get; set; } = -1;

			internal Int32 LinkedDeclarationLine { get; set; } = -1;

			internal Boolean HasBody { get; set; }

			internal Int32 BeginLine { get; set; } = -1;

			internal Int32 EndLine { get; set; } = -1;
		}
	}
}