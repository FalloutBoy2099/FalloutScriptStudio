namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// Describes the arguments of a dialogue function or macro so the parser knows where the message number, the message file,
	/// the target node and the IQ are (legacy <c>OpcodeTemplate</c>). Argument positions are zero-based; -1 means "not used".
	/// </summary>
	public sealed record DialogueFunctionRule
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueFunctionRule"/> class.
		/// </summary>
		/// <param name="opcode">The kind of statement the function is.</param>
		/// <param name="name">The function or macro name.</param>
		/// <param name="messageArg">Zero-based position of the message number argument.</param>
		/// <param name="messageFileArg">Zero-based position of the message file argument, or -1.</param>
		/// <param name="nodeArg">Zero-based position of the target node argument, or -1.</param>
		/// <param name="iqArg">Zero-based position of the IQ argument, or -1.</param>
		/// <param name="totalArgs">The number of arguments of the function.</param>
		/// <param name="isDefault">Whether the rule is built in (user rules are saved to the rules file).</param>
		public DialogueFunctionRule(DialogueOpcode opcode, String name, Int32 messageArg, Int32 messageFileArg, Int32 nodeArg, Int32 iqArg, Int32 totalArgs, Boolean isDefault)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(name);
			this.Opcode = opcode;
			this.Name = name.Trim();
			this.MessageArg = messageArg;
			this.MessageFileArg = messageFileArg;
			this.NodeArg = nodeArg;
			this.IqArg = iqArg;
			this.TotalArgs = totalArgs;
			this.IsDefault = isDefault;
		}

		/// <summary>Gets the kind of statement the function is.</summary>
		public DialogueOpcode Opcode { get; }

		/// <summary>Gets the function or macro name.</summary>
		public String Name { get; }

		/// <summary>Gets the zero-based position of the message number argument.</summary>
		public Int32 MessageArg { get; }

		/// <summary>Gets the zero-based position of the message file argument, or -1.</summary>
		public Int32 MessageFileArg { get; }

		/// <summary>Gets the zero-based position of the target node argument, or -1.</summary>
		public Int32 NodeArg { get; }

		/// <summary>Gets the zero-based position of the IQ argument, or -1.</summary>
		public Int32 IqArg { get; }

		/// <summary>Gets the number of arguments of the function.</summary>
		public Int32 TotalArgs { get; }

		/// <summary>Gets a value indicating whether the rule is built in.</summary>
		public Boolean IsDefault { get; }

		/// <summary>Returns a copy of the rule that is marked as a user rule (saved to the rules file).</summary>
		/// <returns>The user rule.</returns>
		public DialogueFunctionRule AsUserRule()
		{
			return new DialogueFunctionRule(this.Opcode, this.Name, this.MessageArg, this.MessageFileArg, this.NodeArg, this.IqArg, this.TotalArgs, false);
		}

		/// <summary>
		/// Creates a rule from one-based argument positions, the way the rules dialog and the rules file write them.
		/// </summary>
		/// <param name="opcode">The kind of statement.</param>
		/// <param name="name">The function or macro name.</param>
		/// <param name="messageArg">One-based position of the message number argument.</param>
		/// <param name="messageFileArg">One-based position of the message file argument; 0 when not used.</param>
		/// <param name="nodeArg">One-based position of the target node argument; 0 when not used.</param>
		/// <param name="iqArg">One-based position of the IQ argument; 0 when not used.</param>
		/// <param name="totalArgs">The number of arguments.</param>
		/// <param name="isDefault">Whether the rule is built in.</param>
		/// <returns>The rule.</returns>
		public static DialogueFunctionRule FromOneBased(DialogueOpcode opcode, String name, Int32 messageArg, Int32 messageFileArg = 0, Int32 nodeArg = 0, Int32 iqArg = 0, Int32 totalArgs = 1, Boolean isDefault = false)
		{
			return new DialogueFunctionRule(opcode, name, messageArg - 1, messageFileArg - 1, nodeArg - 1, iqArg - 1, totalArgs, isDefault);
		}
	}
}