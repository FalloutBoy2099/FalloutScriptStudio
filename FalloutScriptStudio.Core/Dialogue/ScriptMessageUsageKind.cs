namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>What a message number of a script is used for.</summary>
	public enum ScriptMessageUsageKind
	{
		/// <summary>An NPC text of a dialogue: <c>Reply</c>, <c>NMessage</c>, <c>gsay_reply</c>, <c>gsay_message</c> and the user rules of that kind.</summary>
		NpcText = 0,

		/// <summary>A player option: <c>NOption</c>, <c>gsay_option</c>, <c>giq_option</c> and the user rules of that kind.</summary>
		Option,

		/// <summary>A text shown by other code: <c>message_str(file, n)</c> and the macros that expand to it (<c>mstr(n)</c>, <c>floater(n)</c>...).</summary>
		Display,
	}
}