namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>
	/// The kind of dialogue statement found in the code of a node (legacy <c>OpcodeType</c>).
	/// </summary>
	public enum DialogueOpcode
	{
		/// <summary>An ordinary line of code without a dialogue statement.</summary>
		None = 0,

		/// <summary>A player option: <c>NOption(msg, Node, iq)</c> and similar macros.</summary>
		Option,

		/// <summary>The engine function <c>giq_option(iq, msg_list, msg_num, procedure, reaction)</c>.</summary>
		GiqOption,

		/// <summary>The engine function <c>gsay_option(msg_list, msg_num, procedure, reaction)</c>.</summary>
		GsayOption,

		/// <summary>An NPC reply: <c>Reply(msg)</c> and similar macros.</summary>
		Reply,

		/// <summary>The engine function <c>gsay_reply(msg_list, msg_num)</c> (parsed lines report <see cref="Reply"/>).</summary>
		GsayReply,

		/// <summary>An NPC message without options: <c>NMessage(msg)</c> and similar macros.</summary>
		Message,

		/// <summary>The engine function <c>gsay_message(msg_list, msg_num, reaction)</c> (parsed lines report <see cref="Message"/>).</summary>
		GsayMessage,

		/// <summary>A <c>call Node;</c> statement, or a user macro that jumps to a node (parsed lines report this value for both).</summary>
		Call,

		/// <summary>Only used by <see cref="DialogueFunctionRule"/>: a user macro that jumps to a node without a message (legacy <c>Call</c>).</summary>
		CallMacro,
	}
}