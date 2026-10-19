namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	internal readonly struct ProcedureBodyOffset
	{
		public ProcedureBodyOffset(UInt32 procedureIndex, UInt32 bodyOffset)
		{
			this.ProcedureIndex = procedureIndex;
			this.BodyOffset = bodyOffset;
		}

		public UInt32 ProcedureIndex { get; }
		public UInt32 BodyOffset { get; }
	}
}