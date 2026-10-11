namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A node of the dialogue flowchart: its code, its links and its role (legacy <c>DataNode</c>).
	/// </summary>
	public sealed class FlowchartNodeData
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FlowchartNodeData"/> class.
		/// </summary>
		/// <param name="name">The name of the node procedure.</param>
		/// <param name="kind">The role of the node.</param>
		/// <param name="linkedTo">The links from this node to other nodes.</param>
		/// <param name="linkedFrom">The names of the nodes that link to this node.</param>
		/// <param name="content">The lines of the node.</param>
		public FlowchartNodeData(String name, DialogueNodeKind kind, IReadOnlyList<FlowchartLink> linkedTo, IReadOnlyList<String> linkedFrom, IReadOnlyList<FlowchartContentLine> content)
		{
			ArgumentNullException.ThrowIfNull(name);
			this.Name = name;
			this.Kind = kind;
			this.LinkedTo = linkedTo;
			this.LinkedFrom = linkedFrom;
			this.Content = content;
		}

		/// <summary>Gets the name of the node procedure.</summary>
		public String Name { get; }

		/// <summary>Gets the role of the node.</summary>
		public DialogueNodeKind Kind { get; }

		/// <summary>Gets the links from this node to other nodes, in code order.</summary>
		public IReadOnlyList<FlowchartLink> LinkedTo { get; }

		/// <summary>Gets the names of the nodes that link to this node, in script order.</summary>
		public IReadOnlyList<String> LinkedFrom { get; }

		/// <summary>Gets the lines of the node.</summary>
		public IReadOnlyList<FlowchartContentLine> Content { get; }

		/// <summary>
		/// Gets the incoming links laid out the way the legacy node header showed them: the names alternate between two columns,
		/// each column joined with <c> : </c>.
		/// </summary>
		public IReadOnlyList<String> LinkedFromColumns
		{
			get
			{
				var columns = new[] { String.Empty, String.Empty };
				Int32 column = 0;
				foreach (String name in this.LinkedFrom)
				{
					columns[column] += (columns[column].Length > 0 ? " : " : String.Empty) + name;
					column = 1 - column;
				}

				return columns;
			}
		}

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Name;
		}
	}
}
