namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Runtime.InteropServices;

	/// <summary>
	/// Variable record filled in by <c>getVar</c> / <c>getProcVar</c> of the native parser. Internal to the interop layer:
	/// the layout must match the native structure exactly.
	/// </summary>
	/// <remarks>The native record is 44 bytes; <c>Size</c> keeps room in case a newer library appends fields.</remarks>
#pragma warning disable CS0649 // The fields are filled in by native code, so the compiler cannot see them being assigned.
	[StructLayout(LayoutKind.Explicit, Size = 128)]
	internal struct VariableData
	{
		/// <summary>Offset of the name in the name table.</summary>
		[FieldOffset(0)]
		public Int32 Name;

		/// <summary>Number of references.</summary>
		[FieldOffset(8)]
		public Int32 NumRefs;

		/// <summary>Kind of the initial value.</summary>
		[FieldOffset(12)]
		public ValueKind InitialType;

		/// <summary>Initial integer value, or the offset of the initial string in the string table.</summary>
		[FieldOffset(16)]
		public Int32 IntValue;

		/// <summary>Initial floating point value (shares storage with <see cref="IntValue"/>).</summary>
		[FieldOffset(16)]
		public Single FloatValue;

		/// <summary>Scope of the variable.</summary>
		[FieldOffset(20)]
		public VariableScope Type;

		/// <summary>Array length.</summary>
		[FieldOffset(24)]
		public Int32 ArrayLength;

		/// <summary>Line of the declaration.</summary>
		[FieldOffset(28)]
		public Int32 Declared;

		/// <summary>Pointer to the ANSI path of the declaring file.</summary>
		[FieldOffset(32)]
		public IntPtr DeclaredFile;

		/// <summary>Non-zero when the variable has an initial value.</summary>
		[FieldOffset(40)]
		public Int32 Initialized;
	}
#pragma warning restore CS0649
}
