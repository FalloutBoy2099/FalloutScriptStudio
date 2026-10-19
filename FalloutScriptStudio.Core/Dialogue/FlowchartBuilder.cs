namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Parsing;
	using TextTools;

	/// <summary>
	/// Default <see cref="IFlowchartBuilder"/>.
	/// </summary>
	public sealed class FlowchartBuilder : IFlowchartBuilder
	{
		/// <summary>The text shown for a statement whose message number could not be worked out.</summary>
		public const String NoMessageNumberText = "ParseCode: <Could not get line number of this code>";

		private const String ExitNode = "node999";

		private readonly IDialogueNodeParser _parser;
		private readonly IProcedureTextService _procedures;

		/// <summary>
		/// Initializes a new instance of the <see cref="FlowchartBuilder"/> class.
		/// </summary>
		/// <param name="parser">The node parser.</param>
		/// <param name="procedures">Reads the code of the node procedures.</param>
		public FlowchartBuilder(IDialogueNodeParser parser, IProcedureTextService procedures)
		{
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(procedures);
			this._parser = parser;
			this._procedures = procedures;
		}

		/// <inheritdoc />
		public async Task<FlowchartModel> BuildAsync(String scriptText, ProgramInfo info, IDialogueMessageSource messages, FlowchartBuildOptions options, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(info);
			ArgumentNullException.ThrowIfNull(messages);
			ArgumentNullException.ThrowIfNull(options);

			var parsed = new List<(String Name, IReadOnlyList<DialogueLine> Lines)>();
			var seen = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			IReadOnlyList<String> nodeNames = this._parser.GetNodeProcedureNames(info.Procedures);
			IReadOnlyDictionary<String, String> bodies = this._procedures.GetProcedureCodes(scriptText, nodeNames);
			foreach (String name in nodeNames)
			{
				if (options.ExcludeExitNode && String.Equals(name, ExitNode, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}

				if (!bodies.TryGetValue(name, out String? body) || !seen.Add(name))
				{
					continue;
				}

				parsed.Add((name, this._parser.ParseNodeForDiagram(body, info, !options.ShowCommentedCode)));
			}

			IEnumerable<Int32> files = parsed.SelectMany(p => p.Lines).Where(l => l.UsesOtherMessageFile && l.Opcode.HasMessage()).Select(l => l.MessageFileNumber);
			await messages.EnsureLoadedAsync(files, cancellationToken).ConfigureAwait(false);

			var nodes = new List<FlowchartNodeData>();
			var lines = new Dictionary<String, IReadOnlyList<DialogueLine>>(StringComparer.OrdinalIgnoreCase);
			foreach ((String name, IReadOnlyList<DialogueLine> nodeLines) in parsed)
			{
				lines[name] = nodeLines;
				nodes.Add(CreateNode(name, nodeLines, parsed, messages, options));
			}

			return new FlowchartModel(nodes, lines);
		}

		private static FlowchartNodeData CreateNode(
			String name,
			IReadOnlyList<DialogueLine> lines,
			List<(String Name, IReadOnlyList<DialogueLine> Lines)> all,
			IDialogueMessageSource messages,
			FlowchartBuildOptions options)
		{
			var links = new List<FlowchartLink>();
			var content = new List<FlowchartContentLine>();
			String indent = new String(' ', Math.Max(options.TabSize, 0));

			for (Int32 n = 0; n < lines.Count; n++)
			{
				DialogueLine line = lines[n];
				if (line.IsTransition && line.ToNode is not null)
				{
					links.Add(new FlowchartLink(line.ToNode, n + 1));
				}

				String? text = line.Opcode != DialogueOpcode.None && line.Opcode != DialogueOpcode.Call
					? GetMessageText(messages, line.MessageFileNumber, line.MessageNumber)
					: null;
				String code = indent.Length > 0 && line.Code.StartsWith(indent, StringComparison.Ordinal) ? line.Code.Substring(indent.Length) : line.Code;
				content.Add(new FlowchartContentLine(code, text, line.Opcode, line.MessageNumber, line.MessageFileNumber, n));
			}

			var linkedFrom = new List<String>();
			foreach ((String otherName, IReadOnlyList<DialogueLine> otherLines) in all)
			{
				if (otherLines.Any(l => l.ToNode is not null && String.Equals(l.ToNode, name, StringComparison.OrdinalIgnoreCase)))
				{
					linkedFrom.Add(otherName);
				}
			}

			DialogueNodeKind kind = DialogueNodeKind.Default;
			if (String.Equals(name, "talk_p_proc", StringComparison.OrdinalIgnoreCase))
			{
				kind = DialogueNodeKind.DialogStart;
			}
			else if (links.Count == 0 && linkedFrom.Count > 0)
			{
				kind = DialogueNodeKind.DialogEnd;
			}
			else if (links.Count > 0 && linkedFrom.Count == 0)
			{
				kind = DialogueNodeKind.NoFromLink;
			}
			else if (links.Count == 0 && linkedFrom.Count == 0)
			{
				kind = DialogueNodeKind.Unused;
			}

			return new FlowchartNodeData(name, kind, links, linkedFrom, content);
		}

		private static String GetMessageText(IDialogueMessageSource messages, Int32 messageFile, Int32 messageNumber)
		{
			DialogueMessageLookup lookup = messages.GetMessage(messageFile, messageNumber);
			return lookup.Status == DialogueMessageStatus.NoMessageNumber ? NoMessageNumberText : lookup.Text ?? String.Empty;
		}
	}
}