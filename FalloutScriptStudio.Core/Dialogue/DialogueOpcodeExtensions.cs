namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// Helpers for <see cref="DialogueOpcode"/>.
	/// </summary>
	public static class DialogueOpcodeExtensions
	{
		/// <summary>Determines whether the opcode is a player option (<c>Option</c>, <c>giq_option</c>, <c>gsay_option</c>).</summary>
		/// <param name="opcode">The opcode.</param>
		/// <returns><c>true</c> for option statements.</returns>
		public static Boolean IsOption(this DialogueOpcode opcode)
		{
			return opcode is DialogueOpcode.Option or DialogueOpcode.GiqOption or DialogueOpcode.GsayOption;
		}

		/// <summary>Determines whether the opcode shows an NPC text (a reply or a message).</summary>
		/// <param name="opcode">The opcode.</param>
		/// <returns><c>true</c> for reply and message statements.</returns>
		public static Boolean IsNpcText(this DialogueOpcode opcode)
		{
			return opcode is DialogueOpcode.Reply or DialogueOpcode.GsayReply or DialogueOpcode.Message or DialogueOpcode.GsayMessage;
		}

		/// <summary>Determines whether the opcode moves the dialogue to another node (an option or a call).</summary>
		/// <param name="opcode">The opcode.</param>
		/// <returns><c>true</c> for options and calls.</returns>
		public static Boolean IsTransition(this DialogueOpcode opcode)
		{
			return opcode.IsOption() || opcode is DialogueOpcode.Call or DialogueOpcode.CallMacro;
		}

		/// <summary>Determines whether the opcode carries a message number.</summary>
		/// <param name="opcode">The opcode.</param>
		/// <returns><c>true</c> for options, replies and messages.</returns>
		public static Boolean HasMessage(this DialogueOpcode opcode)
		{
			return opcode.IsOption() || opcode.IsNpcText();
		}

		/// <summary>
		/// Converts the statement name used in the rules file (<c>Option</c>, <c>Reply</c>, <c>Message</c>, <c>Call</c>) to an opcode.
		/// </summary>
		/// <param name="name">The name (case-sensitive, as the legacy file wrote it).</param>
		/// <returns>The opcode (<see cref="DialogueOpcode.CallMacro"/> for <c>Call</c>), or <see cref="DialogueOpcode.None"/> for an unknown name.</returns>
		public static DialogueOpcode ParseRuleKind(String name)
		{
			ArgumentNullException.ThrowIfNull(name);

			return name.Trim() switch
			{
				"Option" => DialogueOpcode.Option,
				"Reply" => DialogueOpcode.Reply,
				"Message" => DialogueOpcode.Message,
				"Call" => DialogueOpcode.CallMacro,
				_ => DialogueOpcode.None,
			};
		}

		/// <summary>Gets the name used for an opcode in the rules file.</summary>
		/// <param name="opcode">The opcode.</param>
		/// <returns>The name; the engine function names for the built-in opcodes.</returns>
		public static String ToRuleKind(this DialogueOpcode opcode)
		{
			return opcode switch
			{
				DialogueOpcode.CallMacro or DialogueOpcode.Call => "Call",
				DialogueOpcode.GiqOption => "giq_option",
				DialogueOpcode.GsayOption => "gsay_option",
				DialogueOpcode.GsayReply => "gsay_reply",
				DialogueOpcode.GsayMessage => "gsay_message",
				_ => opcode.ToString(),
			};
		}
	}
}
