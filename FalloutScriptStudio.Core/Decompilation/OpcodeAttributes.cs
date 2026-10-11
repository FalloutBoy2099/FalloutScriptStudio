namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>How the decompiler writes an opcode.</summary>
	internal sealed class OpcodeAttributes
	{
		public OpcodeAttributes(String name, UInt32 argumentCount, OpcodeAttributeType type, OpcodeAttributeCategory category, UInt32[] procedureArguments)
		{
			this.Name = name;
			this.ArgumentCount = argumentCount;
			this.Type = type;
			this.Category = category;
			this.ProcedureArguments = procedureArguments;
		}

		/// <summary>What is written in the source: the name of the function, or the operator.</summary>
		public String Name { get; }

		/// <summary>How many values the opcode takes from the stack.</summary>
		public UInt32 ArgumentCount { get; }

		public OpcodeAttributeType Type { get; }

		public OpcodeAttributeCategory Category { get; }

		/// <summary>The positions of the arguments that are the names of procedures.</summary>
		public UInt32[] ProcedureArguments { get; }
	}
}
