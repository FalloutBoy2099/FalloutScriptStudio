namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>
	/// What a node is, when it is not just an opcode.
	/// </summary>
	internal enum NodeType
	{
		Normal,
		BeginOfBlock,
		EndOfBlock,
		OmittedArgument,
		Break,
		Continue,
		ForLoop,
		ConditionalExpression
	}
}