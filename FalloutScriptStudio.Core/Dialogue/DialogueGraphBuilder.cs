namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Analysis;
	using Parsing;

	/// <summary>
	/// Builds the <see cref="DialogueGraph"/> of a script: the node procedures are found with the tokens of the text, their bodies are
	/// read by the dialogue parser (the one of the flowchart and the preview), and every other place where a node name appears (a macro
	/// that jumps to a node, a name passed to a function) is kept as an edge too, so that a statement the parser does not understand never
	/// makes a node look unreachable or a dead end.
	/// </summary>
	internal static class DialogueGraphBuilder
	{
		private static readonly DialogueNodeParseOptions _parseOptions = new() { IncludePlainCode = false, ExcludeComments = true };

		/// <summary>Builds the graph of a script.</summary>
		/// <param name="text">The text of the script.</param>
		/// <param name="info">The parse result of the script (its procedures, variables and macros).</param>
		/// <param name="parser">The dialogue parser.</param>
		/// <param name="options">The conventions.</param>
		/// <returns>The graph.</returns>
		public static DialogueGraph Build(String text, ProgramInfo info, IDialogueNodeParser parser, DialogueQualityOptions options)
		{
			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			SslScriptModel model = SslScriptModel.Analyze(tokens);

			List<DialogueGraphNode> nodes = [];
			Dictionary<Int32, DialogueGraphNode> byBegin = [];
			Dictionary<String, DialogueGraphNode> byName = new(StringComparer.OrdinalIgnoreCase);
			List<SslProcedureModel> nodeProcedures = [];
			foreach (SslProcedureModel procedure in model.Procedures)
			{
				if (!DialogueNodeRule.IsNode(procedure.Name) || byName.ContainsKey(procedure.Name))
				{
					continue;
				}

				Int32 endLine = procedure.IsClosed ? tokens[procedure.EndToken].Line : LastLine(tokens);
				DialogueGraphNode node = new(procedure.Name, tokens[procedure.NameToken].Line, endLine);
				nodes.Add(node);
				byName[procedure.Name] = node;
				byBegin[procedure.BeginToken] = node;
				nodeProcedures.Add(procedure);
			}

			TargetResolver resolver = new(info, byName, CollectDefinedNames(tokens, model, info), model.Declarations.Select(declaration => declaration.Name));
			foreach (SslProcedureModel procedure in nodeProcedures)
			{
				ReadBody(text, tokens, procedure, byBegin[procedure.BeginToken], parser, resolver, options);
			}

			HashSet<String> entries = new(StringComparer.OrdinalIgnoreCase);
			ScanTokens(tokens, model, byName, byBegin, entries, options);
			String? entryNode = byName.TryGetValue(DialogueNodeRule.TalkProcedure, out DialogueGraphNode? talk) ? talk.Name : null;
			if (entryNode is not null)
			{
				entries.Add(entryNode);
			}

			return new DialogueGraph(nodes, entries, entryNode);
		}

		private static Int32 LastLine(SslTokenStream tokens)
		{
			return tokens.Count == 0 ? 1 : tokens[tokens.Count - 1].Line;
		}

		private static HashSet<String> CollectDefinedNames(SslTokenStream tokens, SslScriptModel model, ProgramInfo info)
		{
			HashSet<String> names = new(StringComparer.OrdinalIgnoreCase);
			foreach (SslProcedureModel procedure in model.Procedures)
			{
				names.Add(procedure.Name);
			}

			foreach (ScriptProcedure procedure in info.Procedures)
			{
				names.Add(procedure.Name);
			}

			for (Int32 i = 0; i + 1 < tokens.Count; i++)
			{
				if (!tokens[i].InDirective && tokens.IsKeyword(i, "procedure") && tokens[i + 1].Kind == SslTokenKind.Identifier)
				{
					names.Add(tokens.GetText(i + 1));
				}
			}

			return names;
		}

		private static void ReadBody(String text, SslTokenStream tokens, SslProcedureModel procedure, DialogueGraphNode node, IDialogueNodeParser parser, TargetResolver resolver, DialogueQualityOptions options)
		{
			SslToken begin = tokens[procedure.BeginToken];
			Int32 end = procedure.IsClosed ? tokens[procedure.EndToken].Offset : text.Length;
			String body = end > begin.End ? text.Substring(begin.End, end - begin.End) : String.Empty;
			if (body.Length == 0)
			{
				return;
			}

			foreach (DialogueLine line in parser.ParseNode(body, resolver.Info, _parseOptions))
			{
				Int32 scriptLine = begin.Line + line.CodeLineIndex;
				if (line.IsNpcText)
				{
					DialogueStatementKind kind = line.Opcode == DialogueOpcode.Message ? DialogueStatementKind.Message : DialogueStatementKind.Reply;
					node.Statements.Add(new DialogueStatement(kind, scriptLine, line.MessageNumber, line.MessageFileNumber, null, null, null));
				}
				else if (line.IsOption)
				{
					String? resolved = AddTarget(node, resolver, line.ToNode, scriptLine, DialogueEdgeKind.Option, true, options);
					node.Statements.Add(new DialogueStatement(DialogueStatementKind.Option, scriptLine, line.MessageNumber, line.MessageFileNumber, line.ToNode, resolved, line.Iq));
				}
				else if (line.Opcode == DialogueOpcode.Call)
				{
					AddTarget(node, resolver, line.ToNode, scriptLine, DialogueEdgeKind.Call, false, options);
				}
			}
		}

		private static String? AddTarget(DialogueGraphNode node, TargetResolver resolver, String? raw, Int32 line, DialogueEdgeKind kind, Boolean isOption, DialogueQualityOptions options)
		{
			if (raw is null)
			{
				node.HasUnknownTarget = true;
				return null;
			}

			(TargetKind targetKind, String name) = resolver.Resolve(raw);
			switch (targetKind)
			{
				case TargetKind.Node:
					node.Edges.Add(new DialogueEdge(name, line, kind));
					if (kind == DialogueEdgeKind.Call)
					{
						node.Statements.Add(new DialogueStatement(DialogueStatementKind.Call, line, 0, -1, raw, name, null));
					}

					return name;
				case TargetKind.Procedure:
					node.LeavesToProcedure |= isOption;
					break;
				case TargetKind.Undefined:
					node.HasUnknownTarget = true;
					if (isOption && options.ReportUndefinedTargets)
					{
						node.UndefinedTargets.Add(new DialogueEdge(name, line, kind));
					}

					break;
				default:
					node.HasUnknownTarget = true;
					break;
			}

			return null;
		}

		private static void ScanTokens(
			SslTokenStream tokens,
			SslScriptModel model,
			Dictionary<String, DialogueGraphNode> byName,
			Dictionary<Int32, DialogueGraphNode> byBegin,
			HashSet<String> entries,
			DialogueQualityOptions options)
		{
			Dictionary<String, DialogueGraphNode>.AlternateLookup<ReadOnlySpan<Char>> nodeLookup = byName.GetAlternateLookup<ReadOnlySpan<Char>>();
			HashSet<String> exitFunctions = new(options.ExitFunctionNames, StringComparer.OrdinalIgnoreCase);
			HashSet<String>.AlternateLookup<ReadOnlySpan<Char>> exitLookup = exitFunctions.GetAlternateLookup<ReadOnlySpan<Char>>();

			for (Int32 i = 0; i < tokens.Count; i++)
			{
				SslToken token = tokens[i];
				if (token.Kind != SslTokenKind.Identifier)
				{
					continue;
				}

				ReadOnlySpan<Char> word = tokens.Span(i);
				Boolean isElse = word.Equals("else", StringComparison.OrdinalIgnoreCase);
				Boolean isExit = exitLookup.Contains(word);
				Boolean isNode = nodeLookup.TryGetValue(word, out DialogueGraphNode? target);
				if (!isElse && !isExit && !isNode)
				{
					continue;
				}

				SslProcedureModel? container = token.InDirective ? null : model.FindProcedureAt(i);
				DialogueGraphNode? owner = container is not null && i > container.BeginToken && byBegin.TryGetValue(container.BeginToken, out DialogueGraphNode? found) ? found : null;
				if (isNode && target is not null && !(i > 0 && tokens.IsKeyword(i - 1, "procedure") && !tokens[i - 1].InDirective))
				{
					if (owner is null)
					{
						entries.Add(target.Name);
					}
					else if (!owner.Edges.Any(edge => edge.Line == token.Line && String.Equals(edge.Target, target.Name, StringComparison.OrdinalIgnoreCase)))
					{
						owner.Edges.Add(new DialogueEdge(target.Name, token.Line, DialogueEdgeKind.Mention));
					}
				}

				if (owner is null)
				{
					continue;
				}

				if (isElse)
				{
					owner.ElseLines.Add(token.Line);
				}
				else if (isExit)
				{
					owner.CallsExitFunction = true;
				}
			}
		}
	}
}