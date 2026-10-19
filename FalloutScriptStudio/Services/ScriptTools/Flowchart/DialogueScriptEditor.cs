namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using Core.Dialogue;
	using Core.Parsing;
	using Core.TextTools;
	using Documents;
	using Editor.Text;

	/// <summary>
	/// Changes the dialogue nodes (procedures) of a script document: replaces the code of a node, creates, renames and deletes
	/// nodes. Every change is one undoable step of the document and is followed by a new parse of the script.
	/// </summary>
	public sealed class DialogueScriptEditor
	{
		private readonly IProcedureTextService _procedures;
		private readonly IDialogueNodeParser _parser;
		private readonly DialogueNodeFactory _factory;
		private readonly IWorkspace _workspace;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueScriptEditor"/> class.
		/// </summary>
		/// <param name="procedures">Reads and edits procedure text.</param>
		/// <param name="parser">Recognises the node procedures.</param>
		/// <param name="factory">Creates new nodes.</param>
		/// <param name="workspace">Parses the script again after a change and renames symbols.</param>
		public DialogueScriptEditor(IProcedureTextService procedures, IDialogueNodeParser parser, DialogueNodeFactory factory, IWorkspace workspace)
		{
			ArgumentNullException.ThrowIfNull(procedures);
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(factory);
			ArgumentNullException.ThrowIfNull(workspace);
			this._procedures = procedures;
			this._parser = parser;
			this._factory = factory;
			this._workspace = workspace;
		}

		/// <summary>Gets the names of the dialogue node procedures of a script.</summary>
		/// <param name="script">The script.</param>
		/// <returns>The names in script order; empty when the script was not parsed.</returns>
		public IReadOnlyList<String> GetNodeNames(DocumentViewModel script)
		{
			ArgumentNullException.ThrowIfNull(script);
			return script.ProgramInfo is null ? Array.Empty<String>() : this._parser.GetNodeProcedureNames(script.ProgramInfo.Procedures);
		}

		/// <summary>Gets the body of a procedure.</summary>
		/// <param name="script">The script.</param>
		/// <param name="procedureName">The procedure name.</param>
		/// <returns>The code, or <see langword="null"/> when the script has no such procedure body.</returns>
		public String? GetNodeCode(DocumentViewModel script, String procedureName)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(procedureName);
			return this._procedures.GetProcedureCode(script.TextDocument.Text, procedureName);
		}

		/// <summary>Replaces the body of a procedure as one undoable change.</summary>
		/// <param name="script">The script.</param>
		/// <param name="procedureName">The procedure name.</param>
		/// <param name="code">The new body.</param>
		/// <returns><see langword="false"/> when the script has no such procedure body.</returns>
		public async Task<Boolean> ReplaceNodeCodeAsync(DocumentViewModel script, String procedureName, String code)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(procedureName);
			ArgumentNullException.ThrowIfNull(code);

			TextEdit? edit = this._procedures.ReplaceProcedureCode(script.TextDocument.Text, procedureName, code);
			if (edit is null)
			{
				return false;
			}

			DocumentEdits.Apply(script.TextDocument, new[] { edit });
			await this._workspace.RequestParseAsync(script, true);
			return true;
		}

		/// <summary>Appends a new node procedure (and its forward declaration) to the script.</summary>
		/// <param name="script">The script.</param>
		/// <param name="requestedName">The name typed by the user; the word <c>Node</c> is added when missing.</param>
		/// <param name="code">The body of the node.</param>
		/// <returns>The result; the script is changed only when it succeeded.</returns>
		public async Task<NodeCreationResult> CreateNodeAsync(DocumentViewModel script, String requestedName, String code)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(requestedName);
			ArgumentNullException.ThrowIfNull(code);

			if (script.ProgramInfo is null)
			{
				await this._workspace.RequestParseAsync(script, true);
			}

			if (script.ProgramInfo is null)
			{
				return new NodeCreationResult(NodeCreationError.InvalidName, requestedName, null);
			}

			NodeCreationResult result = this._factory.Create(script.TextDocument.Text, script.ProgramInfo, requestedName, code);
			if (result.Succeeded && result.Edit is not null)
			{
				DocumentEdits.Apply(script.TextDocument, result.Edit.Edits);
				await this._workspace.RequestParseAsync(script, true);
			}

			return result;
		}

		/// <summary>Deletes node procedures with their forward declarations as one undoable change.</summary>
		/// <param name="script">The script.</param>
		/// <param name="procedureNames">The procedures to delete.</param>
		/// <returns>The number of edits applied.</returns>
		public async Task<Int32> DeleteNodesAsync(DocumentViewModel script, IEnumerable<String> procedureNames)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(procedureNames);

			ProcedureEditResult result = this._procedures.DeleteProcedures(script.TextDocument.Text, procedureNames);
			Int32 count = DocumentEdits.Apply(script.TextDocument, result.Edits);
			if (count > 0)
			{
				await this._workspace.RequestParseAsync(script, true);
			}

			return count;
		}

		/// <summary>
		/// Renames a node with the rename refactoring of the workspace (all references change). A new name without the word
		/// <c>Node</c> is refused and the rename is undone, because the procedure would no longer be a dialogue node.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="nodeName">The current name of the node.</param>
		/// <returns>The result.</returns>
		public async Task<NodeRenameResult> RenameNodeAsync(DocumentViewModel script, String nodeName)
		{
			ArgumentNullException.ThrowIfNull(script);
			ArgumentNullException.ThrowIfNull(nodeName);

			ScriptProcedure? procedure = script.ProgramInfo?.GetProcedure(nodeName);
			if (procedure is null)
			{
				return new NodeRenameResult(NodeRenameOutcome.NotFound, null);
			}

			HashSet<String> before = new(script.ProgramInfo!.Procedures.Select(p => p.Name), StringComparer.Ordinal);
			await this._workspace.RenameSymbolAsync(script, procedure);
			await this._workspace.RequestParseAsync(script, true);

			String? newName = script.ProgramInfo?.Procedures.Select(p => p.Name).FirstOrDefault(name => !before.Contains(name));
			if (newName is null)
			{
				return new NodeRenameResult(NodeRenameOutcome.Cancelled, null);
			}

			if (!DialogueNodeFactory.IsValidNodeName(newName))
			{
				script.TextDocument.UndoStack.Undo();
				await this._workspace.RequestParseAsync(script, true);
				return new NodeRenameResult(NodeRenameOutcome.Rejected, newName);
			}

			return new NodeRenameResult(NodeRenameOutcome.Renamed, newName);
		}
	}
}