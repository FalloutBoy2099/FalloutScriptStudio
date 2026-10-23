namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using Parsing;

	/// <summary>
	/// Checks the structure of the dialogue of a script: the nodes (procedures whose name contains <c>node</c>, and <c>talk_p_proc</c>), the
	/// options and calls that lead from one to another, and the texts they show. The check is pure: it reads the script text, its parse
	/// result and the dialogue parser (the same one the flowchart and the preview use, so the same option macros are understood).
	/// </summary>
	/// <remarks>
	/// <para>Rules (the codes are those of <see cref="DialogueQualityCode"/>):</para>
	/// <list type="bullet">
	/// <item><description>DLG001, unreachable node: the nodes that can be reached are those reachable from <c>talk_p_proc</c> or from a node that code outside the nodes (a handler, a macro) names, following options, <c>call</c> statements and every other mention of a node name in a body. An empty node is never reported (the exit nodes are often declared and not used). Nothing is reported when the script has no entry at all.</description></item>
	/// <item><description>DLG002, dead end: a node that shows a <c>Reply</c> and has no option, no call, no exit and no target it could not work out. An exit is: a conventional exit node name (<c>Node998</c>, <c>Node999</c>, <c>Node1000</c>), a node that has no dialogue statement at all (a code-only node such as the combat node), a message without an answer (<c>NMessage</c>, <c>gsay_message</c>) when there is no option, a call of a function that ends the dialogue (<c>end_dialogue</c>, <c>gsay_end</c>, barter, attack) and an option that jumps to a procedure that is not a node.</description></item>
	/// <item><description>DLG003, undefined target: an option names a procedure that is not defined, declared, imported, nor a macro or a variable (macros that stand for a plain name are followed).</description></item>
	/// <item><description>DLG004, exitless cycle: a group of nodes (a strongly connected component) with no edge to a node outside the group and no exit in any of them. A group none of whose nodes can be reached is left to DLG001.</description></item>
	/// <item><description>DLG005, node without text: a node with options but no reply and no message.</description></item>
	/// <item><description>DLG006, duplicate targets: two options of one node jump to the same node (not an exit node) with the same intelligence requirement. Options on the two sides of an <c>else</c> are alternatives and never duplicates, and neither are a normal and a low intelligence option.</description></item>
	/// <item><description>DLG007, missing messages: the node uses message numbers that the report of <see cref="ScriptMessageConsistency"/> found missing (it only links the two checks: one note per node, the numbers are listed by Check Messages).</description></item>
	/// <item><description>DLG008, too many options: more than <see cref="DialogueQualityOptions.MaxOptionsPerNode"/> option statements in a node.</description></item>
	/// <item><description>DLG009, duplicate message number: two options of a node show the same message (alternatives on the two sides of an <c>else</c> are ignored).</description></item>
	/// </list>
	/// <para>The check is conservative: a node name that appears anywhere in a body counts as an edge even when the parser did not understand the statement, an option with a target that cannot be worked out is never a dead end, and a script without any dialogue statement has no findings. Long chains of single-option nodes are not reported (there is no objective limit).</para>
	/// </remarks>
	public static class DialogueQualityChecker
	{
		/// <summary>Checks the dialogue of a script.</summary>
		/// <param name="scriptText">The text of the script.</param>
		/// <param name="info">The parse result of the script (procedures, variables and macros, headers included).</param>
		/// <param name="parser">The dialogue parser.</param>
		/// <param name="options">The conventions; the defaults when <see langword="null"/>.</param>
		/// <param name="messages">The report of the check of the script against its message file, or <see langword="null"/> to skip DLG007.</param>
		/// <returns>The report.</returns>
		public static DialogueQualityReport Check(String scriptText, ProgramInfo info, IDialogueNodeParser parser, DialogueQualityOptions? options = null, ScriptMessageReport? messages = null)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(info);
			ArgumentNullException.ThrowIfNull(parser);

			options ??= new DialogueQualityOptions();
			DialogueGraph graph = DialogueGraphBuilder.Build(scriptText, info, parser, options);
			Boolean hasDialogue = graph.Nodes.Any(node => node.Statements.Count > 0);
			if (!hasDialogue)
			{
				return new DialogueQualityReport(Array.Empty<DialogueQualityFinding>(), graph.Nodes.Count, graph.EntryNode, false);
			}

			HashSet<String> exitNames = new(options.ExitNodeNames, StringComparer.OrdinalIgnoreCase);
			HashSet<String> reachable = Reach(graph);
			Boolean knowsEntries = graph.Entries.Count > 0;
			List<DialogueQualityFinding> findings = [];

			foreach (DialogueGraphNode node in graph.Nodes)
			{
				if (knowsEntries && node.HasDialogue && !reachable.Contains(node.Name))
				{
					findings.Add(Create(DialogueQualityCode.UnreachableNode, DialogueQualitySeverity.Warning, node, node.HeaderLine));
				}

				foreach (DialogueEdge undefined in node.UndefinedTargets.DistinctBy(edge => (edge.Line, edge.Target.ToLowerInvariant())))
				{
					findings.Add(Create(DialogueQualityCode.UndefinedTarget, DialogueQualitySeverity.Error, node, undefined.Line, undefined.Target));
				}

				if (IsDeadEnd(node, exitNames))
				{
					Int32 line = node.Statements.First(statement => statement.Kind == DialogueStatementKind.Reply).Line;
					findings.Add(Create(DialogueQualityCode.DeadEnd, DialogueQualitySeverity.Warning, node, line));
				}

				AddNodeChecks(graph, node, exitNames, options, findings);
			}

			AddCycles(graph, exitNames, reachable, knowsEntries, findings);
			if (messages is not null)
			{
				AddMissingMessages(graph, reachable, knowsEntries, messages, findings);
			}

			List<DialogueQualityFinding> ordered = findings.OrderBy(finding => finding.Line).ThenBy(finding => finding.Code).ToList();
			return new DialogueQualityReport(ordered, graph.Nodes.Count, graph.EntryNode, true);
		}

		private static DialogueQualityFinding Create(DialogueQualityCode code, DialogueQualitySeverity severity, DialogueGraphNode node, Int32 line, String? related = null, Int32 count = 0, IReadOnlyList<Int32>? numbers = null, IReadOnlyList<String>? affected = null)
		{
			return new DialogueQualityFinding(code, severity, node.Name, line, related, count, numbers ?? Array.Empty<Int32>(), affected ?? new[] { node.Name });
		}

		private static HashSet<String> Reach(DialogueGraph graph)
		{
			HashSet<String> reached = new(StringComparer.OrdinalIgnoreCase);
			Stack<DialogueGraphNode> pending = new();
			foreach (String entry in graph.Entries)
			{
				if (graph.ByName.TryGetValue(entry, out DialogueGraphNode? node) && reached.Add(node.Name))
				{
					pending.Push(node);
				}
			}

			while (pending.Count > 0)
			{
				DialogueGraphNode current = pending.Pop();
				foreach (DialogueEdge edge in current.Edges)
				{
					if (graph.ByName.TryGetValue(edge.Target, out DialogueGraphNode? next) && reached.Add(next.Name))
					{
						pending.Push(next);
					}
				}
			}

			return reached;
		}

		/// <summary>Determines whether the dialogue ends in a node (see the remarks of the class).</summary>
		private static Boolean IsExit(DialogueGraphNode node, HashSet<String> exitNames)
		{
			if (exitNames.Contains(node.Name) || node.CallsExitFunction || node.LeavesToProcedure)
			{
				return true;
			}

			if (node.Edges.Count > 0)
			{
				return false;
			}

			Boolean hasOption = node.Statements.Any(statement => statement.Kind == DialogueStatementKind.Option);
			Boolean hasMessage = node.Statements.Any(statement => statement.Kind == DialogueStatementKind.Message);
			return !node.HasDialogue || (hasMessage && !hasOption);
		}

		private static Boolean IsDeadEnd(DialogueGraphNode node, HashSet<String> exitNames)
		{
			return node.Edges.Count == 0
				&& !node.HasUnknownTarget
				&& !IsExit(node, exitNames)
				&& node.Statements.Any(statement => statement.Kind == DialogueStatementKind.Reply);
		}

		private static void AddNodeChecks(DialogueGraph graph, DialogueGraphNode node, HashSet<String> exitNames, DialogueQualityOptions options, List<DialogueQualityFinding> findings)
		{
			List<DialogueStatement> optionStatements = node.Options.ToList();
			if (optionStatements.Count > 0 && !node.Texts.Any())
			{
				findings.Add(Create(DialogueQualityCode.NodeWithoutText, DialogueQualitySeverity.Info, node, node.HeaderLine, count: optionStatements.Count));
			}

			if (optionStatements.Count > options.MaxOptionsPerNode)
			{
				findings.Add(Create(DialogueQualityCode.TooManyOptions, DialogueQualitySeverity.Info, node, node.HeaderLine, count: optionStatements.Count));
			}

			AddDuplicateTargets(graph, node, exitNames, optionStatements, findings);
			AddDuplicateNumbers(node, optionStatements, findings);
		}

		private static void AddDuplicateTargets(DialogueGraph graph, DialogueGraphNode node, HashSet<String> exitNames, List<DialogueStatement> options, List<DialogueQualityFinding> findings)
		{
			Dictionary<(String Target, String Iq), List<DialogueStatement>> seen = [];
			HashSet<(String Target, String Iq)> reported = [];
			foreach (DialogueStatement option in options)
			{
				if (option.Resolved is null || !graph.ByName.TryGetValue(option.Resolved, out DialogueGraphNode? target) || IsExit(target, exitNames))
				{
					continue;
				}

				(String, String) key = (target.Name.ToLowerInvariant(), option.Iq ?? String.Empty);
				if (!seen.TryGetValue(key, out List<DialogueStatement>? earlier))
				{
					seen[key] = [option];
					continue;
				}

				if (earlier.Any(other => !AreAlternatives(node, other.Line, option.Line)) && reported.Add(key))
				{
					findings.Add(Create(DialogueQualityCode.DuplicateTarget, DialogueQualitySeverity.Info, node, option.Line, target.Name, earlier.Count + 1));
				}

				earlier.Add(option);
			}
		}

		private static void AddDuplicateNumbers(DialogueGraphNode node, List<DialogueStatement> options, List<DialogueQualityFinding> findings)
		{
			Dictionary<(Int32 File, Int32 Number), List<DialogueStatement>> seen = [];
			HashSet<(Int32, Int32)> reported = [];
			foreach (DialogueStatement option in options)
			{
				if (option.MessageNumber <= 0)
				{
					continue;
				}

				(Int32, Int32) key = (option.MessageFile, option.MessageNumber);
				if (!seen.TryGetValue(key, out List<DialogueStatement>? earlier))
				{
					seen[key] = [option];
					continue;
				}

				if (earlier.Any(other => !AreAlternatives(node, other.Line, option.Line)) && reported.Add(key))
				{
					findings.Add(Create(DialogueQualityCode.DuplicateMessageNumber, DialogueQualitySeverity.Warning, node, option.Line, numbers: new[] { option.MessageNumber }));
				}

				earlier.Add(option);
			}
		}

		/// <summary>Two statements are alternatives when an <c>else</c> lies between their lines.</summary>
		private static Boolean AreAlternatives(DialogueGraphNode node, Int32 first, Int32 second)
		{
			Int32 low = Math.Min(first, second);
			Int32 high = Math.Max(first, second);
			return node.ElseLines.Any(line => line >= low && line <= high);
		}

		private static void AddMissingMessages(DialogueGraph graph, HashSet<String> reachable, Boolean knowsEntries, ScriptMessageReport messages, List<DialogueQualityFinding> findings)
		{
			List<ScriptMessageFinding> missing = messages.Of(ScriptMessageFindingKind.MissingMessage).Where(finding => finding.InScript && finding.Line > 0).ToList();
			if (missing.Count == 0)
			{
				return;
			}

			foreach (DialogueGraphNode node in graph.Nodes)
			{
				if (knowsEntries && !reachable.Contains(node.Name))
				{
					continue;
				}

				List<Int32> numbers = missing
					.Where(finding => finding.Line >= node.HeaderLine && finding.Line <= node.EndLine)
					.Select(finding => finding.Number)
					.Distinct()
					.Order()
					.ToList();
				if (numbers.Count > 0)
				{
					Int32 line = missing.First(finding => finding.Line >= node.HeaderLine && finding.Line <= node.EndLine).Line;
					findings.Add(Create(DialogueQualityCode.MissingMessages, DialogueQualitySeverity.Info, node, line, count: numbers.Count, numbers: numbers));
				}
			}
		}

		private static void AddCycles(DialogueGraph graph, HashSet<String> exitNames, HashSet<String> reachable, Boolean knowsEntries, List<DialogueQualityFinding> findings)
		{
			foreach (List<DialogueGraphNode> group in StronglyConnectedComponents(graph))
			{
				HashSet<String> members = new(group.Select(node => node.Name), StringComparer.OrdinalIgnoreCase);
				Boolean isCycle = group.Count > 1 || group[0].Edges.Any(edge => members.Contains(edge.Target));
				if (!isCycle || (knowsEntries && !group.Any(node => reachable.Contains(node.Name))))
				{
					continue;
				}

				Boolean leaves = group.Any(node => node.HasUnknownTarget || IsExit(node, exitNames) || node.Edges.Any(edge => !members.Contains(edge.Target)));
				if (leaves)
				{
					continue;
				}

				List<DialogueGraphNode> ordered = graph.Nodes.Where(node => members.Contains(node.Name)).ToList();
				DialogueGraphNode first = ordered[0];
				String[] names = ordered.Select(node => node.Name).ToArray();
				findings.Add(Create(DialogueQualityCode.ExitlessCycle, DialogueQualitySeverity.Warning, first, first.HeaderLine, names.Length > 1 ? names[1] : null, names.Length, null, names));
			}
		}

		/// <summary>Tarjan's algorithm without recursion (a dialogue can have thousands of nodes).</summary>
		private static List<List<DialogueGraphNode>> StronglyConnectedComponents(DialogueGraph graph)
		{
			Int32 count = graph.Nodes.Count;
			Dictionary<String, Int32> indexOf = new(StringComparer.OrdinalIgnoreCase);
			for (Int32 i = 0; i < count; i++)
			{
				indexOf.TryAdd(graph.Nodes[i].Name, i);
			}

			Int32[][] successors = new Int32[count][];
			for (Int32 i = 0; i < count; i++)
			{
				successors[i] = graph.Nodes[i].Edges
					.Select(edge => indexOf.TryGetValue(edge.Target, out Int32 target) ? target : -1)
					.Where(target => target >= 0)
					.Distinct()
					.ToArray();
			}

			Int32[] order = new Int32[count];
			Int32[] low = new Int32[count];
			Boolean[] onStack = new Boolean[count];
			Array.Fill(order, -1);
			Stack<Int32> stack = new();
			Stack<(Int32 Node, Int32 Next)> work = new();
			List<List<DialogueGraphNode>> result = [];
			Int32 counter = 0;

			for (Int32 root = 0; root < count; root++)
			{
				if (order[root] >= 0)
				{
					continue;
				}

				work.Push((root, 0));
				while (work.Count > 0)
				{
					(Int32 node, Int32 next) = work.Pop();
					if (next == 0)
					{
						order[node] = low[node] = counter++;
						stack.Push(node);
						onStack[node] = true;
					}

					Boolean descended = false;
					for (Int32 k = next; k < successors[node].Length; k++)
					{
						Int32 target = successors[node][k];
						if (order[target] < 0)
						{
							work.Push((node, k + 1));
							work.Push((target, 0));
							descended = true;
							break;
						}

						if (onStack[target])
						{
							low[node] = Math.Min(low[node], order[target]);
						}
					}

					if (descended)
					{
						continue;
					}

					if (low[node] == order[node])
					{
						List<DialogueGraphNode> group = [];
						Int32 member;
						do
						{
							member = stack.Pop();
							onStack[member] = false;
							group.Add(graph.Nodes[member]);
						}
						while (member != node);
						result.Add(group);
					}

					if (work.Count > 0)
					{
						(Int32 parent, _) = work.Peek();
						low[parent] = Math.Min(low[parent], low[node]);
					}
				}
			}

			return result;
		}
	}
}