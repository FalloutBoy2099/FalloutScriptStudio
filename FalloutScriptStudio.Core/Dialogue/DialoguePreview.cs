namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// The state of the dialogue preview window (legacy <c>DialogPreview</c>): the node being shown, the replies, messages and options
	/// of that node with their texts, the navigation history and the "female" message offset.
	/// </summary>
	/// <remarks>
	/// Create one instance per preview window, call <see cref="StartAsync"/>, then bind the window to <see cref="Rows"/>,
	/// <see cref="NodeNames"/> and <see cref="CurrentNode"/>. The instance is not thread-safe; use it from the UI thread: the
	/// asynchronous methods resume on the caller's context, so <see cref="Changed"/> is raised on the thread that called them.
	/// </remarks>
	public sealed class DialoguePreview
	{
		private const String TalkProcedure = "talk_p_proc";

		private readonly IDialogueNodeParser _parser;
		private readonly IProcedureTextService _procedures;
		private readonly List<String> _history = new List<String>();
		private IDialogueMessageSource? _messages;
		private String _scriptText = String.Empty;
		private ProgramInfo? _info;
		private Int32 _historyIndex = -1;
		private Int32 _buildVersion;
		private IReadOnlyList<DialoguePreviewRow> _rows = Array.Empty<DialoguePreviewRow>();
		private IReadOnlyList<String> _nodeNames = Array.Empty<String>();

		/// <summary>
		/// Initializes a new instance of the <see cref="DialoguePreview"/> class.
		/// </summary>
		/// <param name="parser">The node parser.</param>
		/// <param name="procedures">Reads the code of the node procedures.</param>
		public DialoguePreview(IDialogueNodeParser parser, IProcedureTextService procedures)
		{
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(procedures);
			this._parser = parser;
			this._procedures = procedures;
		}

		/// <summary>Occurs after the node, the rows or the history changed.</summary>
		public event EventHandler? Changed;

		/// <summary>Gets a value indicating whether the preview was started on a script that has dialogue nodes.</summary>
		public Boolean IsReady => this._info is not null && this.CurrentNode is not null;

		/// <summary>Gets the names of the dialogue node procedures of the script (the choices of the node list).</summary>
		public IReadOnlyList<String> NodeNames => this._nodeNames;

		/// <summary>Gets the name of the node being shown, or <c>null</c> before <see cref="StartAsync"/>.</summary>
		public String? CurrentNode => this._historyIndex >= 0 ? this._history[this._historyIndex] : null;

		/// <summary>Gets the rows of the current node.</summary>
		public IReadOnlyList<DialoguePreviewRow> Rows => this._rows;

		/// <summary>Gets a value indicating whether the female variant of the messages is shown.</summary>
		public Boolean FemaleMode { get; private set; }

		/// <summary>Gets the number added to message numbers in female mode: female messages follow the male ones with this offset.</summary>
		public Int32 FemaleOffset { get; private set; }

		/// <summary>Gets a value indicating whether there is a node to go back to.</summary>
		public Boolean CanGoBack => this._historyIndex > 0;

		/// <summary>Gets a value indicating whether there is a node to go forward to.</summary>
		public Boolean CanGoForward => this._historyIndex >= 0 && this._historyIndex < this._history.Count - 1;

		/// <summary>
		/// Starts the preview.
		/// </summary>
		/// <param name="scriptText">The text of the script.</param>
		/// <param name="info">The parse result of the script.</param>
		/// <param name="messages">The message files of the script.</param>
		/// <param name="caretLine">The 1-based caret line of the editor: when it is inside a node, that node is shown first.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>false</c> when the script has no dialogue to show (no node at the caret and no <c>talk_p_proc</c>).</returns>
		public async Task<Boolean> StartAsync(String scriptText, ProgramInfo info, IDialogueMessageSource messages, Int32 caretLine, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(info);
			ArgumentNullException.ThrowIfNull(messages);

			this._scriptText = scriptText;
			this._info = info;
			this._messages = messages;
			this._nodeNames = this._parser.GetNodeProcedureNames(info.Procedures);
			this._history.Clear();
			this._historyIndex = -1;
			this._rows = Array.Empty<DialoguePreviewRow>();

			ScriptProcedure? current = info.GetProcedureFromPosition(caretLine);
			String? start = current is not null && this.ContainsNode(current.Name)
				? current.Name
				: info.GetProcedure(TalkProcedure)?.Name;
			if (start is null)
			{
				this.RaiseChanged();
				return false;
			}

			this._history.Add(start);
			this._historyIndex = 0;
			await this.BuildRowsAsync(cancellationToken);
			return true;
		}

		/// <summary>
		/// Shows the node the user picked in the node list; the navigation history starts again from it.
		/// </summary>
		/// <param name="nodeName">The node name.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>false</c> when the node does not exist.</returns>
		public async Task<Boolean> SelectNodeAsync(String nodeName, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(nodeName);

			String? name = this.FindNodeName(nodeName);
			if (name is null)
			{
				return false;
			}

			this._history.Clear();
			this._history.Add(name);
			this._historyIndex = 0;
			await this.BuildRowsAsync(cancellationToken);
			return true;
		}

		/// <summary>
		/// Follows an option or a call: shows its target node and adds it to the history (the part after the current position is dropped).
		/// </summary>
		/// <param name="row">A row of <see cref="Rows"/> whose <see cref="DialoguePreviewRow.CanFollow"/> is set.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>false</c> when the row cannot be followed.</returns>
		public async Task<Boolean> FollowAsync(DialoguePreviewRow row, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(row);

			String? name = row.CanFollow ? this.FindNodeName(row.TargetNode) : null;
			if (name is null)
			{
				return false;
			}

			if (this._historyIndex + 1 < this._history.Count)
			{
				this._history.RemoveRange(this._historyIndex + 1, this._history.Count - this._historyIndex - 1);
			}

			this._history.Add(name);
			this._historyIndex++;
			await this.BuildRowsAsync(cancellationToken);
			return true;
		}

		/// <summary>Shows the previous node of the history.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>false</c> when there is none.</returns>
		public async Task<Boolean> GoBackAsync(CancellationToken cancellationToken = default)
		{
			if (!this.CanGoBack)
			{
				return false;
			}

			this._historyIndex--;
			await this.BuildRowsAsync(cancellationToken);
			return true;
		}

		/// <summary>Shows the next node of the history.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>false</c> when there is none.</returns>
		public async Task<Boolean> GoForwardAsync(CancellationToken cancellationToken = default)
		{
			if (!this.CanGoForward)
			{
				return false;
			}

			this._historyIndex++;
			await this.BuildRowsAsync(cancellationToken);
			return true;
		}

		/// <summary>Turns the female message variant on or off and rebuilds the rows.</summary>
		/// <param name="enabled">Whether the female variant is shown.</param>
		/// <param name="offset">The number added to the message numbers; ignored when <paramref name="enabled"/> is <c>false</c>.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the rows are rebuilt.</returns>
		public Task SetFemaleModeAsync(Boolean enabled, Int32 offset, CancellationToken cancellationToken = default)
		{
			this.FemaleMode = enabled;
			this.FemaleOffset = enabled ? Math.Max(offset, 0) : 0;
			return this.BuildRowsAsync(cancellationToken);
		}

		/// <summary>Rebuilds the rows of the current node, for example after a message was edited.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the rows are rebuilt.</returns>
		public Task RefreshAsync(CancellationToken cancellationToken = default)
		{
			return this.BuildRowsAsync(cancellationToken);
		}

		/// <summary>
		/// Replaces the script after it was parsed again (legacy "update on activation"). The node list is refreshed and the current
		/// node is shown again; when the node no longer exists the rows are cleared.
		/// </summary>
		/// <param name="scriptText">The new text of the script.</param>
		/// <param name="info">The new parse result.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the rows are rebuilt.</returns>
		public Task UpdateScriptAsync(String scriptText, ProgramInfo info, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(scriptText);
			ArgumentNullException.ThrowIfNull(info);

			this._scriptText = scriptText;
			this._info = info;
			this._nodeNames = this._parser.GetNodeProcedureNames(info.Procedures);
			return this.BuildRowsAsync(cancellationToken);
		}

		/// <summary>Gets the place in the script a row jumps to when its node cell is double-clicked: the current node for replies and messages, the target node otherwise.</summary>
		/// <param name="row">A row.</param>
		/// <returns>The location of the body of the node, or <c>null</c> when it is unknown.</returns>
		public SourceReference? GetJumpTarget(DialoguePreviewRow row)
		{
			ArgumentNullException.ThrowIfNull(row);

			String? name = row.Opcode.IsNpcText() ? this.CurrentNode : row.TargetNode;
			return name is null ? null : this._info?.LookupDefinition(name);
		}

		private Boolean ContainsNode(String name)
		{
			return this._nodeNames.Any(n => String.Equals(n, name, StringComparison.OrdinalIgnoreCase));
		}

		private String? FindNodeName(String name)
		{
			String trimmed = name.Trim('"', ' ');
			String? node = this._nodeNames.FirstOrDefault(n => String.Equals(n, trimmed, StringComparison.OrdinalIgnoreCase));
			if (node is not null)
			{
				return node;
			}

			ScriptProcedure? procedure = trimmed.Length == 0 ? null : this._info?.GetProcedure(trimmed);
			return procedure is not null && procedure.StartLine > 0 ? procedure.Name : null;
		}

		private void RaiseChanged()
		{
			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		private async Task BuildRowsAsync(CancellationToken cancellationToken)
		{
			Int32 version = ++this._buildVersion;
			String? node = this.CurrentNode;
			if (node is null || this._info is null || this._messages is null)
			{
				this._rows = Array.Empty<DialoguePreviewRow>();
				this.RaiseChanged();
				return;
			}

			String? body = this._procedures.GetProcedureCode(this._scriptText, node);
			if (body is null)
			{
				this._rows = Array.Empty<DialoguePreviewRow>();
				this.RaiseChanged();
				return;
			}

			IReadOnlyList<DialogueLine> lines = this._parser.ParseNode(body, this._info, new DialogueNodeParseOptions());
			await this._messages.EnsureLoadedAsync(
				lines.Where(l => l.UsesOtherMessageFile && l.Opcode.HasMessage()).Select(l => l.MessageFileNumber),
				cancellationToken);

			if (version != this._buildVersion)
			{
				// A newer request changed the node while the message files were loading: its rows win.
				return;
			}

			var rows = new List<DialoguePreviewRow>();
			foreach (DialogueLine line in lines)
			{
				if (line.Opcode != DialogueOpcode.None)
				{
					rows.Add(this.CreateRow(line));
				}
			}

			this._rows = rows;
			this.RaiseChanged();
		}

		private DialoguePreviewRow CreateRow(DialogueLine line)
		{
			IDialogueMessageSource messages = this._messages!;
			Int32 offset = this.FemaleMode ? this.FemaleOffset : 0;
			String text;
			DialogueMessageStatus status = DialogueMessageStatus.NoMessageNumber;
			String? path = messages.GetMessageFilePath(line.MessageFileNumber);
			Int32 number = line.MessageNumber;

			if (line.MessageNumber > 0)
			{
				DialogueMessageLookup lookup = messages.GetMessage(line.MessageFileNumber, offset + line.MessageNumber);
				if (lookup.Status == DialogueMessageStatus.MessageNotFound && offset > 0)
				{
					lookup = messages.GetMessage(line.MessageFileNumber, line.MessageNumber);
					offset = 0;
				}

				text = lookup.Text ?? String.Empty;
				status = lookup.Status;
				path = lookup.FilePath ?? path;
				number = offset + line.MessageNumber;
			}
			else
			{
				text = "<" + line.ShortCode + ">";
			}

			String target = (line.ToNode ?? String.Empty).Trim('"', ' ');
			Boolean canFollow = line.IsTransition && this.FindNodeName(target) is not null;
			return new DialoguePreviewRow
			{
				Line = line,
				TargetNode = target,
				MessageText = text,
				Iq = line.Iq,
				MessageNumber = number,
				MessageFilePath = path,
				Status = status,
				CanFollow = canFollow,
			};
		}
	}
}
