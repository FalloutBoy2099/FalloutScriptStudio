namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>A dialogue node as the quality checker sees it: the statements of its body and the edges that leave it.</summary>
	internal sealed class DialogueGraphNode
	{
		/// <summary>Initializes a new instance of the <see cref="DialogueGraphNode"/> class.</summary>
		/// <param name="name">The name of the procedure.</param>
		/// <param name="headerLine">The 1-based line of the name in the procedure header.</param>
		/// <param name="endLine">The 1-based line of the closing <c>end</c>.</param>
		public DialogueGraphNode(String name, Int32 headerLine, Int32 endLine)
		{
			this.Name = name;
			this.HeaderLine = headerLine;
			this.EndLine = endLine;
		}

		/// <summary>Gets the name of the procedure as written in its definition.</summary>
		public String Name { get; }

		/// <summary>Gets the 1-based line of the procedure header.</summary>
		public Int32 HeaderLine { get; }

		/// <summary>Gets the 1-based line of the closing <c>end</c> of the body.</summary>
		public Int32 EndLine { get; }

		/// <summary>Gets the dialogue statements of the body in script order.</summary>
		public List<DialogueStatement> Statements { get; } = [];

		/// <summary>Gets the edges to other nodes: options, calls and every other mention of a node name in the body.</summary>
		public List<DialogueEdge> Edges { get; } = [];

		/// <summary>Gets the lines that hold an <c>else</c>: options on both sides of one are alternatives.</summary>
		public List<Int32> ElseLines { get; } = [];

		/// <summary>Gets the options whose target is not defined anywhere in the script.</summary>
		public List<DialogueEdge> UndefinedTargets { get; } = [];

		/// <summary>Gets or sets a value indicating whether an option or a call leaves the dialogue graph for a procedure that is not a node.</summary>
		public Boolean LeavesToProcedure { get; set; }

		/// <summary>Gets or sets a value indicating whether a target could not be worked out (a macro that is not a plain name): the node is then never reported as a dead end.</summary>
		public Boolean HasUnknownTarget { get; set; }

		/// <summary>Gets or sets a value indicating whether the body calls a function that ends the dialogue.</summary>
		public Boolean CallsExitFunction { get; set; }

		/// <summary>Gets the replies and messages of the node.</summary>
		public IEnumerable<DialogueStatement> Texts => this.Statements.Where(statement => statement.Kind != DialogueStatementKind.Option && statement.Kind != DialogueStatementKind.Call);

		/// <summary>Gets the options of the node.</summary>
		public IEnumerable<DialogueStatement> Options => this.Statements.Where(statement => statement.Kind == DialogueStatementKind.Option);

		/// <summary>Gets a value indicating whether the node shows or asks anything: a reply, a message, an option or a call to a node.</summary>
		public Boolean HasDialogue => this.Statements.Count > 0 || this.Edges.Count > 0;
	}
}