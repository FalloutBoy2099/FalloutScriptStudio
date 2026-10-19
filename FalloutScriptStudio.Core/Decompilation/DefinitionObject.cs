namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>
	/// What a name of the namespace stands for: a procedure (<see cref="Data"/> is its number) or a variable (its value).
	/// </summary>
	internal sealed class DefinitionObject
	{
		public DefinitionObject(DefinitionObjectType type, UInt32 attributes, UInt32 data)
		{
			this.Type = type;
			this.Attributes = attributes;
			this.Data = data;
		}

		public DefinitionObjectType Type { get; }
		public UInt32 Attributes { get; }
		public UInt32 Data { get; }
	}
}