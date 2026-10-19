namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using Controls.Diagram.Model;
	using Core.Dialogue;
	using Localization;

	/// <summary>
	/// Converts between the data of the dialogue flowchart (<see cref="FlowchartNodeData"/>, <see cref="FlowchartDocument"/>)
	/// and the items of the diagram canvas. It holds no state.
	/// </summary>
	public static class FlowchartDiagramMapper
	{
		/// <summary>Gets the header text of the start node of a dialogue, in the language of the interface.</summary>
		public static String StartNodeCaption => Loc.Get("Str.Flowchart.Node.StartCaption");

		/// <summary>Gets the header text of a node that no other node links to, in the language of the interface.</summary>
		public static String NoLinksCaption => Loc.Get("Str.Flowchart.Node.NoLinksCaption");

		/// <summary>Gets the header text of a node without any link, in the language of the interface.</summary>
		public static String UnusedCaption => Loc.Get("Str.Flowchart.Node.UnusedCaption");

		/// <summary>
		/// Creates the canvas node for a node of the flowchart. Each line of code becomes one row, in the same order, so that the
		/// 1-based <see cref="FlowchartLink.ContentLine"/> of a link is the index of its origin row plus one.
		/// </summary>
		/// <param name="data">The node.</param>
		/// <param name="showCode">Whether the rows of plain code are shown (the global default).</param>
		/// <returns>The node; it is collapsed when it is unused or empty.</returns>
		public static DiagramNode CreateNode(FlowchartNodeData data, Boolean showCode)
		{
			ArgumentNullException.ThrowIfNull(data);

			DiagramNode node = new(data.Name)
			{
				Kind = ToNodeKind(data.Kind),
				ShowCodeRows = showCode,
				IsCollapsed = data.Kind == DialogueNodeKind.Unused || data.Content.Count == 0,
			};

			foreach (String caption in GetHeaderLines(data))
			{
				node.HeaderLines.Add(caption);
			}

			for (Int32 i = 0; i < data.Content.Count; i++)
			{
				FlowchartContentLine line = data.Content[i];
				FlowchartLink? link = data.LinkedTo.FirstOrDefault(l => l.ContentLine == i + 1);
				DiagramRow row = CreateRow(line, link);
				node.Rows.Add(row);
			}

			return node;
		}

		/// <summary>
		/// Writes the header text of a start, unused or unlinked node again, in the language of the interface (the language changed
		/// after the node was created). Other nodes are left as they are.
		/// </summary>
		/// <param name="node">The node.</param>
		public static void RefreshCaption(DiagramNode node)
		{
			ArgumentNullException.ThrowIfNull(node);

			String? caption = node.Kind switch
			{
				DiagramNodeKind.Start => StartNodeCaption,
				DiagramNodeKind.NoIncomingLinks => NoLinksCaption,
				DiagramNodeKind.Unused => UnusedCaption,
				_ => null,
			};
			if (caption is not null && node.HeaderLines.Count == 1)
			{
				node.HeaderLines[0] = caption;
			}
		}

		/// <summary>Gets the text of a message row of the canvas.</summary>
		/// <param name="line">The line.</param>
		/// <returns>The text; replies are put in quotes like in the legacy editor.</returns>
		public static String GetRowText(FlowchartContentLine line)
		{
			ArgumentNullException.ThrowIfNull(line);

			String text = line.MessageText ?? String.Empty;
			return line.Opcode.IsNpcText() ? "\"" + text + "\"" : text;
		}

		/// <summary>Reads the state of a canvas node, to keep it when the node is created again or to save it.</summary>
		/// <param name="node">The node.</param>
		/// <returns>The state; the collapsed flags of the message rows are numbered like the legacy <c>Content</c> elements.</returns>
		public static FlowchartNodeState CaptureState(DiagramNode node)
		{
			ArgumentNullException.ThrowIfNull(node);

			List<FlowchartContentState> contents = [];
			Int32 block = 0;
			foreach (DiagramRow row in node.Rows)
			{
				if (row.Tag is FlowchartContentLine line && line.Opcode.HasMessage())
				{
					contents.Add(new FlowchartContentState(block.ToString(CultureInfo.InvariantCulture), !row.IsExpanded));
					block++;
				}
			}

			return new FlowchartNodeState(node.Name)
			{
				X = (Single)node.X,
				Y = (Single)node.Y,
				Width = (Single)node.Width,
				Collapsed = node.IsCollapsed,
				Hidden = node.IsHidden,
				ShowCode = node.HasUserShowCodeSetting ? node.ShowCodeRows : null,
				Contents = contents,
			};
		}

		/// <summary>Applies a saved state to a canvas node.</summary>
		/// <param name="node">The node.</param>
		/// <param name="state">The state.</param>
		public static void ApplyState(DiagramNode node, FlowchartNodeState state)
		{
			ArgumentNullException.ThrowIfNull(node);
			ArgumentNullException.ThrowIfNull(state);

			node.X = state.X;
			node.Y = state.Y;
			if (state.Width > 0)
			{
				node.Width = state.Width;
			}

			node.IsCollapsed = state.Collapsed;
			node.IsHidden = state.Hidden;
			if (state.ShowCode is Boolean showCode)
			{
				node.ShowCodeRows = showCode;
				node.HasUserShowCodeSetting = true;
			}

			Dictionary<String, Boolean> collapsed = new(StringComparer.Ordinal);
			foreach (FlowchartContentState content in state.Contents)
			{
				collapsed[content.Line] = content.Collapsed;
			}

			Int32 block = 0;
			foreach (DiagramRow row in node.Rows)
			{
				if (row.Tag is FlowchartContentLine line && line.Opcode.HasMessage())
				{
					if (collapsed.TryGetValue(block.ToString(CultureInfo.InvariantCulture), out Boolean isCollapsed))
					{
						row.IsExpanded = !isCollapsed && row.IsExpandable;
					}

					block++;
				}
			}
		}

		/// <summary>Creates the canvas note for a saved note.</summary>
		/// <param name="state">The saved note.</param>
		/// <returns>The note.</returns>
		public static DiagramNote CreateNote(FlowchartNoteState state)
		{
			ArgumentNullException.ThrowIfNull(state);

			DiagramNote note = new()
			{ Text = state.Text, X = state.X, Y = state.Y };
			if (state.Width > 0)
			{
				note.Width = state.Width;
			}

			if (state.Height > 0)
			{
				note.Height = state.Height;
			}

			return note;
		}

		/// <summary>Reads the state of a canvas note.</summary>
		/// <param name="note">The note.</param>
		/// <returns>The state.</returns>
		public static FlowchartNoteState CaptureNote(DiagramNote note)
		{
			ArgumentNullException.ThrowIfNull(note);

			return new FlowchartNoteState(note.Text)
			{
				X = (Single)note.X,
				Y = (Single)note.Y,
				Width = (Single)note.Width,
				Height = (Single)note.Height,
			};
		}

		/// <summary>
		/// Creates the flowchart file content from the items of the canvas. Like the legacy editor it writes the hidden nodes first,
		/// then the visible ones from back to front, then the notes.
		/// </summary>
		/// <param name="items">The items of the canvas from back to front.</param>
		/// <param name="scriptName">The value for <see cref="FlowchartDocument.ScriptName"/>.</param>
		/// <param name="zoom">The zoom factor of the canvas.</param>
		/// <param name="positionX">The horizontal scroll position.</param>
		/// <param name="positionY">The vertical scroll position.</param>
		/// <returns>The document.</returns>
		public static FlowchartDocument CreateDocument(IEnumerable<DiagramItem> items, String scriptName, Double zoom, Int32 positionX, Int32 positionY)
		{
			ArgumentNullException.ThrowIfNull(items);
			ArgumentNullException.ThrowIfNull(scriptName);

			List<DiagramItem> all = items.ToList();
			List<DiagramNode> nodes = all.OfType<DiagramNode>().ToList();
			return new FlowchartDocument(scriptName)
			{
				Zoom = (Single)zoom,
				PositionX = positionX,
				PositionY = positionY,
				Nodes = nodes.Where(n => n.IsHidden).Concat(nodes.Where(n => !n.IsHidden)).Select(CaptureState).ToList(),
				Notes = all.OfType<DiagramNote>().Select(CaptureNote).ToList(),
			};
		}

		/// <summary>Removes the quotes and spaces around the target of a link.</summary>
		/// <param name="target">The target as the parser reported it.</param>
		/// <returns>The node name.</returns>
		public static String NormalizeTarget(String target)
		{
			ArgumentNullException.ThrowIfNull(target);
			return target.Trim('"', ' ');
		}

		private static DiagramRow CreateRow(FlowchartContentLine line, FlowchartLink? link)
		{
			String code = line.Code.Trim();
			DiagramRow row;
			if (line.Opcode.IsNpcText())
			{
				row = new DiagramRow(DiagramRowKind.Reply, GetRowText(line), code.Length > 0 ? code : null);
			}
			else if (line.Opcode.IsOption())
			{
				row = new DiagramRow(DiagramRowKind.Option, GetRowText(line), code.Length > 0 ? code : null);
			}
			else if (line.Opcode == DialogueOpcode.Call)
			{
				row = new DiagramRow(DiagramRowKind.Call, code);
			}
			else
			{
				row = new DiagramRow(DiagramRowKind.Code, line.Code.TrimEnd());
			}

			row.Tag = line;
			if (link is not null)
			{
				row.LinkTargetName = NormalizeTarget(link.TargetNode);
			}

			return row;
		}

		private static IEnumerable<String> GetHeaderLines(FlowchartNodeData data)
		{
			switch (data.Kind)
			{
				case DialogueNodeKind.DialogStart:
					return new[] { StartNodeCaption };
				case DialogueNodeKind.NoFromLink:
					return new[] { NoLinksCaption };
				case DialogueNodeKind.Unused:
					return new[] { UnusedCaption };
				default:
					return data.LinkedFromColumns.Where(column => column.Length > 0);
			}
		}

		private static DiagramNodeKind ToNodeKind(DialogueNodeKind kind)
		{
			return kind switch
			{
				DialogueNodeKind.DialogStart => DiagramNodeKind.Start,
				DialogueNodeKind.DialogEnd => DiagramNodeKind.End,
				DialogueNodeKind.NoFromLink => DiagramNodeKind.NoIncomingLinks,
				DialogueNodeKind.Unused => DiagramNodeKind.Unused,
				_ => DiagramNodeKind.Default,
			};
		}
	}
}