namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Runtime.InteropServices;

	/// <summary>
	/// Procedure record filled in by <c>getProc</c> of the native parser. Internal to the interop layer:
	/// the layout must match the native structure exactly.
	/// </summary>
	/// <remarks>
	/// The <c>parser.dll</c> shipped with the sfall compiler 5.x writes 88 bytes: the 72 bytes of the layout used by the legacy
	/// editor followed by four more fields that are not needed here. Declaring only the first 72 bytes made the library
	/// overwrite the memory behind the structure (heap corruption, then a crash). <c>Size</c> reserves room for further growth
	/// as well; every field that is read sits in the first 72 bytes.
	/// </remarks>
#pragma warning disable CS0649, CS0169 // The fields are filled in by native code, so the compiler cannot see them being assigned.
	[StructLayout(LayoutKind.Sequential, Size = 256)]
	internal struct ProcedureData
	{
		/// <summary>Offset of the name in the name table.</summary>
		public Int32 Name;

		/// <summary>Procedure modifiers.</summary>
		public ProcedureFlags Type;

		/// <summary>Value of the <c>timed</c> modifier.</summary>
		public Int32 Time;

		private readonly Int32 _unused0;
		private readonly Int32 _unused1;

		/// <summary>Number of arguments.</summary>
		public Int32 Args;

		/// <summary>Line of the definition (the same as <see cref="Start"/>), -1 when there is no separate forward declaration.</summary>
		public Int32 Defined;

		private readonly Int32 _unused2;

		/// <summary>Number of local variables, including arguments.</summary>
		public Int32 NumVariables;

		private readonly Int32 _unused3;

		/// <summary>Number of references.</summary>
		public Int32 NumRefs;

		private readonly Int32 _unused4;

		/// <summary>Line of the declaration.</summary>
		public Int32 Declared;

		/// <summary>Pointer to the ANSI path of the declaring file.</summary>
		public IntPtr DeclaredFile;

		/// <summary>Line where the body starts.</summary>
		public Int32 Start;

		/// <summary>Pointer to the ANSI path of the file with the body.</summary>
		public IntPtr StartFile;

		/// <summary>Line where the body ends.</summary>
		public Int32 End;

		/// <summary>Pointer to the ANSI path of the file where the body ends.</summary>
		public IntPtr EndFile;
	}
#pragma warning restore CS0649, CS0169
}
