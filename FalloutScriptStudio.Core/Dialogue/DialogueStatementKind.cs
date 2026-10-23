namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>The kind of a <see cref="DialogueStatement"/>.</summary>
	internal enum DialogueStatementKind
	{
		/// <summary>An NPC reply that waits for an option (<c>Reply</c>, <c>gsay_reply</c>).</summary>
		Reply,

		/// <summary>An NPC message that needs no answer (<c>NMessage</c>, <c>gsay_message</c>).</summary>
		Message,

		/// <summary>A player option.</summary>
		Option,

		/// <summary>A <c>call</c> of a node or a macro that jumps to one.</summary>
		Call,
	}
}