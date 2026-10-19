namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Parsing;
	using TextTools;

	/// <summary>
	/// Data logic of the node code editor (legacy <c>FlowchartTE</c>): lists the messages the code of a node uses and changes the
	/// message number of a statement.
	/// </summary>
	public sealed class DialogueNodeEditor
	{
		private readonly IDialogueNodeParser _parser;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueNodeEditor"/> class.
		/// </summary>
		/// <param name="parser">The node parser.</param>
		public DialogueNodeEditor(IDialogueNodeParser parser)
		{
			ArgumentNullException.ThrowIfNull(parser);
			this._parser = parser;
		}

		/// <summary>
		/// Lists the statements of a node that have a message number, with their texts.
		/// </summary>
		/// <param name="nodeCode">The code of the node as it is in the editor.</param>
		/// <param name="info">The parse result of the script.</param>
		/// <param name="messages">The message files of the script.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>One row per statement with a message, in the order of the parser; <see cref="NodeMessageRow.CodeLineIndex"/> is the index of the editor line.</returns>
		public async Task<IReadOnlyList<NodeMessageRow>> GetMessageRowsAsync(String nodeCode, ProgramInfo info, IDialogueMessageSource messages, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(nodeCode);
			ArgumentNullException.ThrowIfNull(info);
			ArgumentNullException.ThrowIfNull(messages);

			IReadOnlyList<DialogueLine> lines = this._parser.ParseNode(nodeCode, info, new DialogueNodeParseOptions());
			List<DialogueLine> withMessages = lines.Where(l => l.HasMessageNumber).ToList();
			await messages.EnsureLoadedAsync(withMessages.Where(l => l.UsesOtherMessageFile).Select(l => l.MessageFileNumber), cancellationToken).ConfigureAwait(false);
			return withMessages.Select(l => new NodeMessageRow(l, messages.GetMessage(l.MessageFileNumber, l.MessageNumber))).ToList();
		}

		/// <summary>
		/// Creates the edit that replaces the message number of a statement in the node code
		/// (legacy <c>msgEdit_SendMsgLine</c>, used when the user picks another message in the message editor).
		/// </summary>
		/// <param name="nodeCode">The code of the node as it is in the editor.</param>
		/// <param name="line">A line returned by the parser for that code (parsed with empty lines kept).</param>
		/// <param name="newNumber">The new message number, or a macro name.</param>
		/// <returns>The edit, or <c>null</c> when the number is not written literally in the statement (for example it comes from a macro).</returns>
		public TextEdit? CreateMessageNumberEdit(String nodeCode, DialogueLine line, String newNumber)
		{
			ArgumentNullException.ThrowIfNull(nodeCode);
			ArgumentNullException.ThrowIfNull(line);
			ArgumentNullException.ThrowIfNull(newNumber);

			var map = new TextLineMap(nodeCode);
			Int32 lineNumber = line.CodeLineIndex + 1;
			if (lineNumber < 1 || lineNumber > map.LineCount || line.MessageNumber <= 0)
			{
				return null;
			}

			String text = map.GetLineText(lineNumber);
			Int32 from = line.ShortCode is null ? 0 : Math.Max(text.IndexOf(line.ShortCode, StringComparison.Ordinal), 0);
			String number = line.MessageNumber.ToString(CultureInfo.InvariantCulture);
			Match match = Regex.Match(text.Substring(from), @"(?<![\p{L}\p{N}_])" + Regex.Escape(number) + @"(?![\p{L}\p{N}_])");
			return match.Success ? new TextEdit(map.GetLineStart(lineNumber) + from + match.Index, number.Length, newNumber) : null;
		}
	}
}