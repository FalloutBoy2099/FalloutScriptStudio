namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Finds the symbols of one file: the procedures (with the text based procedure parser of the editor), the macros (with the macro
	/// collector, so that the parameters and the line match what the tooltips show) and the script level variables.
	/// </summary>
	internal static class SymbolFileScanner
	{
		private static readonly InternalParser _procedureParser = new InternalParser();
		private static readonly MacroCollector _macroCollector = new MacroCollector(new IncludeResolver(null, null));

		/// <summary>Scans a text.</summary>
		/// <param name="path">The full path of the file the text comes from.</param>
		/// <param name="text">The text.</param>
		/// <param name="symbols">Receives the symbols, in file order within each kind.</param>
		/// <param name="includes">Receives the paths of the <c>#include</c> directives as written.</param>
		internal static void Scan(String path, String text, List<IndexedSymbol> symbols, List<String> includes)
		{
			if (text.Contains("procedure", StringComparison.OrdinalIgnoreCase))
			{
				foreach (ScriptProcedure procedure in _procedureParser.ParseProcedures(text, path))
				{
					Boolean hasBody = procedure.StartLine > 0;
					Int32 line = hasBody ? procedure.StartLine : procedure.DeclaredLine;
					if (line <= 0)
					{
						continue;
					}

					symbols.Add(new IndexedSymbol(procedure.Name, IndexedSymbolKind.Procedure, path, line)
					{
						DeclarationLine = procedure.DeclaredLine > 0 ? procedure.DeclaredLine : line,
						HasBody = hasBody,
						Flags = procedure.Flags,
					});
				}
			}

			if (text.Contains("#define", StringComparison.Ordinal))
			{
				foreach (ScriptMacro macro in _macroCollector.CollectFromText(text, path, false).Values)
				{
					symbols.Add(new IndexedSymbol(macro.Token, IndexedSymbolKind.Macro, path, macro.DeclaredLine)
					{
						Detail = String.Equals(macro.DefinitionName, macro.Token, StringComparison.Ordinal) ? null : macro.DefinitionName,
					});
				}
			}

			Boolean hasVariables = text.Contains("variable", StringComparison.OrdinalIgnoreCase);
			if (hasVariables || text.Contains("#include", StringComparison.Ordinal))
			{
				SslTokenStream tokens = SslTokenizer.Tokenize(text);
				ScanIncludes(tokens, includes);
				if (hasVariables)
				{
					foreach (VariableDeclaration declaration in SslScriptModel.Analyze(tokens).Declarations)
					{
						if (declaration.Kind is VariableDeclarationKind.Parameter or VariableDeclarationKind.Local)
						{
							continue;
						}

						symbols.Add(new IndexedSymbol(declaration.Name, IndexedSymbolKind.Variable, path, declaration.Line)
						{
							Scope = declaration.Kind switch
							{
								VariableDeclarationKind.Import => VariableScope.Import,
								VariableDeclarationKind.Export => VariableScope.Export,
								_ => VariableScope.Global,
							},
						});
					}
				}
			}
		}

		private static void ScanIncludes(SslTokenStream tokens, List<String> includes)
		{
			for (Int32 i = 0; i + 1 < tokens.Count; i++)
			{
				if (tokens[i].Kind == SslTokenKind.Directive && tokens[i + 1].Kind == SslTokenKind.String && tokens.GetDirectiveName(i) == "include")
				{
					String quoted = tokens.GetText(i + 1);
					if (quoted.Length >= 2 && quoted[quoted.Length - 1] == '"')
					{
						includes.Add(quoted.Substring(1, quoted.Length - 2));
					}
				}
			}
		}
	}
}
