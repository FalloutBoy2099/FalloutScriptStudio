namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>What a <see cref="DialogueQualityFinding"/> reports. The codes are stable: the localized texts and the documentation use them.</summary>
	public enum DialogueQualityCode
	{
		/// <summary>DLG001: no option or call of the entry node (<c>talk_p_proc</c>), of a node reachable from it or of other code of the script leads to the node (warning).</summary>
		UnreachableNode = 1,

		/// <summary>DLG002: the node shows an NPC reply but has no option, no call and no exit, so the player would be stuck (warning).</summary>
		DeadEnd = 2,

		/// <summary>DLG003: an option jumps to a procedure that the script does not define (error).</summary>
		UndefinedTarget = 3,

		/// <summary>DLG004: nodes that only lead to each other, with no way out of the group and no exit in any of them (warning).</summary>
		ExitlessCycle = 4,

		/// <summary>DLG005: the node has options but shows no NPC text (information).</summary>
		NodeWithoutText = 5,

		/// <summary>DLG006: two options of the node jump to the same node with the same intelligence requirement (information).</summary>
		DuplicateTarget = 6,

		/// <summary>DLG007: the node uses message numbers that the message file lacks (information; the numbers are listed by Check Messages).</summary>
		MissingMessages = 7,

		/// <summary>DLG008: the node has more options than the dialogue window shows comfortably (information).</summary>
		TooManyOptions = 8,

		/// <summary>DLG009: two options of the node use the same message number (warning).</summary>
		DuplicateMessageNumber = 9,
	}
}