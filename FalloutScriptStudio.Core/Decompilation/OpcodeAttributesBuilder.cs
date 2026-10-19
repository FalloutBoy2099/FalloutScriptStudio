namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using System.Collections.Generic;

	/// <summary>Fills a table of opcodes: a line for each opcode, named by its kind.</summary>
	internal sealed class OpcodeAttributesBuilder
	{
		public Dictionary<Opcode, OpcodeAttributes> Attributes { get; } = [];

		/// <summary>An opcode that is a line of its own.</summary>
		public void Statement(Opcode opcode, String name, UInt32 argumentCount, params UInt32[] procedureArguments)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.Statement, OpcodeAttributeCategory.Prefix, procedureArguments);
		}

		/// <summary>An opcode that is a value.</summary>
		public void Expression(Opcode opcode, String name, UInt32 argumentCount, params UInt32[] procedureArguments)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.Expression, OpcodeAttributeCategory.Prefix, procedureArguments);
		}

		/// <summary>An operator written between its two values.</summary>
		public void Infix(Opcode opcode, String name, UInt32 argumentCount)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.Expression, OpcodeAttributeCategory.Infix, []);
		}

		/// <summary>An opcode that is a value, or a line when nothing uses its value.</summary>
		public void ExpressionStatement(Opcode opcode, String name, UInt32 argumentCount)
		{
			Add(opcode, name, argumentCount, OpcodeAttributeType.ExpressionStatement, OpcodeAttributeCategory.Prefix, []);
		}

		private void Add(Opcode opcode, String name, UInt32 argumentCount, OpcodeAttributeType type, OpcodeAttributeCategory category, UInt32[] procedureArguments)
		{
			Attributes[opcode] = new OpcodeAttributes(name, argumentCount, type, category, procedureArguments);
		}
	}
}