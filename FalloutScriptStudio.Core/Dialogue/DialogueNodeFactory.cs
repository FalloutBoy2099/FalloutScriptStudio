namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using Parsing;
	using TextTools;

	/// <summary>
	/// Creates new dialogue nodes: checks the name and builds the script change that adds the procedure
	/// (legacy <c>NodeDiagram.CreateClick</c>).
	/// </summary>
	public sealed class DialogueNodeFactory
	{
		private const String NodePrefix = "Node";

		private readonly IProcedureTextService _procedures;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueNodeFactory"/> class.
		/// </summary>
		/// <param name="procedures">Inserts procedures into the script text.</param>
		public DialogueNodeFactory(IProcedureTextService procedures)
		{
			ArgumentNullException.ThrowIfNull(procedures);
			this._procedures = procedures;
		}

		/// <summary>
		/// Determines whether a procedure name keeps a procedure a dialogue node: it must contain the word <c>Node</c> (any case).
		/// Renaming a node to a name that does not is refused (legacy check after the rename dialog).
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns><c>true</c> when the name contains <c>node</c>.</returns>
		public static Boolean IsValidNodeName(String name)
		{
			ArgumentNullException.ThrowIfNull(name);
			return name.Contains(NodePrefix, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Checks the name typed for a new node. A name without the word <c>Node</c> gets the prefix <c>Node</c>.
		/// </summary>
		/// <param name="requestedName">The name typed by the user.</param>
		/// <param name="info">The parse result of the script, used to find clashes.</param>
		/// <returns>The final name and the problem with it, if any.</returns>
		public (String Name, NodeCreationError Error) CheckName(String requestedName, ProgramInfo info)
		{
			ArgumentNullException.ThrowIfNull(requestedName);
			ArgumentNullException.ThrowIfNull(info);

			String name = requestedName.Trim();
			if (name.Length == 0)
			{
				return (name, NodeCreationError.EmptyName);
			}

			if (name.Contains('#', StringComparison.Ordinal))
			{
				return (name, NodeCreationError.PlaceholderInName);
			}

			if (!IsValidNodeName(name))
			{
				name = NodePrefix + name;
			}

			if (!ProcedureNameParser.IsIdentifier(name))
			{
				return (name, NodeCreationError.InvalidName);
			}

			return info.NameExists(name, SymbolKind.Procedure) ? (name, NodeCreationError.NameExists) : (name, NodeCreationError.None);
		}

		/// <summary>
		/// Builds the change that appends a node procedure (and its forward declaration) to the script.
		/// </summary>
		/// <param name="scriptText">The text of the script.</param>
		/// <param name="info">The parse result of the script.</param>
		/// <param name="requestedName">The name typed by the user.</param>
		/// <param name="nodeCode">The body of the node, for example the code of a <see cref="DialogueNodeTemplate"/>.</param>
		/// <returns>The result; the edit is in the coordinates of <paramref name="scriptText"/>.</returns>
		public NodeCreationResult Create(String scriptText, ProgramInfo info, String requestedName, String nodeCode)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(nodeCode);

			(String name, NodeCreationError error) = this.CheckName(requestedName, info);
			if (error != NodeCreationError.None)
			{
				return new NodeCreationResult(error, name, null);
			}

			ProcedureEditResult? edit = this._procedures.InsertProcedure(scriptText, new ProcedureInsertRequest(name) { Placement = InsertPlacement.End, Body = nodeCode });
			return edit is null
				? new NodeCreationResult(NodeCreationError.NameExists, name, null)
				: new NodeCreationResult(NodeCreationError.None, name, edit);
		}
	}
}