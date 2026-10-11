namespace FalloutScriptStudio.Core.Decompilation
{
	using System;

	/// <summary>
	/// The flags of the type of a procedure, and of a variable.
	/// </summary>
	internal static class ObjectAttributes
	{
		public const UInt32 Timed = 0x01;
		public const UInt32 Conditional = 0x02;
		public const UInt32 Import = 0x04;
		public const UInt32 Export = 0x08;
		public const UInt32 Critical = 0x10;
		public const UInt32 NotImplemented = 0x80000000;
		public const UInt32 Global = 0x00010000;
	}
}
