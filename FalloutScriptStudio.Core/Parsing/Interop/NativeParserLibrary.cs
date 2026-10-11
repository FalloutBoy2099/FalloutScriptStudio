namespace FalloutScriptStudio.Core.Parsing.Interop
{
	using System;
	using System.Runtime.InteropServices;

	/// <summary>
	/// A loaded <c>parser.dll</c>: the exported functions bound to delegates (the legacy <c>[DllImport]</c> block).
	/// </summary>
	/// <remarks>
	/// The library is loaded from an explicit path with <see cref="NativeLibrary.Load(String)"/> instead of a static
	/// <c>DllImport</c> so that the location comes from the options and no static state is needed. Strings are marshalled
	/// as ANSI. The native code keeps global state: callers must serialize every use of an instance (and of all instances).
	/// </remarks>
	internal sealed class NativeParserLibrary : IDisposable
	{
		private readonly IntPtr _handle;

		private readonly ParseMainDelegate _parseMain;
		private readonly NumberDelegate _numProcs;
		private readonly GetProcDelegate _getProc;
		private readonly IndexedSizeDelegate _getProcNamespaceSize;
		private readonly IndexedBytesDelegate _getProcNamespace;
		private readonly NumberDelegate _numVars;
		private readonly GetVarDelegate _getVar;
		private readonly GetProcVarDelegate _getProcVar;
		private readonly NumberDelegate _namespaceSize;
		private readonly BytesDelegate _getNamespace;
		private readonly NumberDelegate _stringspaceSize;
		private readonly BytesDelegate _getStringspace;
		private readonly IndexedIntsDelegate _getProcRefs;
		private readonly IndexedIntsDelegate _getVarRefs;
		private readonly GetProcVarRefsDelegate _getProcVarRefs;

		private Boolean _disposed;

		private NativeParserLibrary(IntPtr handle)
		{
			this._handle = handle;
			this._parseMain = Bind<ParseMainDelegate>(handle, "parse_main", 24);
			this._numProcs = Bind<NumberDelegate>(handle, "numProcs", 0);
			this._getProc = Bind<GetProcDelegate>(handle, "getProc", 8);
			this._getProcNamespaceSize = Bind<IndexedSizeDelegate>(handle, "getProcNamespaceSize", 4);
			this._getProcNamespace = Bind<IndexedBytesDelegate>(handle, "getProcNamespace", 8);
			this._numVars = Bind<NumberDelegate>(handle, "numVars", 0);
			this._getVar = Bind<GetVarDelegate>(handle, "getVar", 8);
			this._getProcVar = Bind<GetProcVarDelegate>(handle, "getProcVar", 12);
			this._namespaceSize = Bind<NumberDelegate>(handle, "namespaceSize", 0);
			this._getNamespace = Bind<BytesDelegate>(handle, "getNamespace", 4);
			this._stringspaceSize = Bind<NumberDelegate>(handle, "stringspaceSize", 0);
			this._getStringspace = Bind<BytesDelegate>(handle, "getStringspace", 4);
			this._getProcRefs = Bind<IndexedIntsDelegate>(handle, "getProcRefs", 8);
			this._getVarRefs = Bind<IndexedIntsDelegate>(handle, "getVarRefs", 8);
			this._getProcVarRefs = Bind<GetProcVarRefsDelegate>(handle, "getProcVarRefs", 12);
		}

		[UnmanagedFunctionPointer(CallingConvention.Winapi, CharSet = CharSet.Ansi)]
		private delegate Int32 ParseMainDelegate(String file, String original, String directory, String? definitions, String? includePath, Int32 backwardMode);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate Int32 NumberDelegate();

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void GetProcDelegate(Int32 index, out ProcedureData data);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate Int32 IndexedSizeDelegate(Int32 index);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void IndexedBytesDelegate(Int32 index, [Out] Byte[] buffer);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void GetVarDelegate(Int32 index, out VariableData data);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void GetProcVarDelegate(Int32 procedure, Int32 variable, out VariableData data);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void BytesDelegate([Out] Byte[] buffer);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void IndexedIntsDelegate(Int32 index, [Out] Int32[] buffer);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		private delegate void GetProcVarRefsDelegate(Int32 procedure, Int32 variable, [Out] Int32[] buffer);

		/// <summary>Loads the library and binds every export.</summary>
		/// <param name="path">Full path of <c>parser.dll</c>.</param>
		/// <param name="failure">Explains why the library could not be used.</param>
		/// <returns>The library, or <c>null</c> when it cannot be loaded (wrong bitness, missing file or exports, ...).</returns>
		internal static NativeParserLibrary? TryLoad(String path, out String? failure)
		{
			IntPtr handle = IntPtr.Zero;
			try
			{
				handle = NativeLibrary.Load(path);
				failure = null;
				return new NativeParserLibrary(handle);
			}
			catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException or EntryPointNotFoundException or ArgumentException or MissingMethodException)
			{
				if (handle != IntPtr.Zero)
				{
					NativeLibrary.Free(handle);
				}

				failure = ex.GetType().Name + ": " + ex.Message;
				return null;
			}
		}

		internal Int32 ParseMain(String file, String original, String directory, String? definitions, String? includePath, Int32 backwardMode)
		{
			return this._parseMain(file, original, directory, definitions, includePath, backwardMode);
		}

		internal Int32 NumProcs() => this._numProcs();

		internal void GetProc(Int32 index, out ProcedureData data) => this._getProc(index, out data);

		internal Int32 GetProcNamespaceSize(Int32 index) => this._getProcNamespaceSize(index);

		internal void GetProcNamespace(Int32 index, Byte[] buffer) => this._getProcNamespace(index, buffer);

		internal Int32 NumVars() => this._numVars();

		internal void GetVar(Int32 index, out VariableData data) => this._getVar(index, out data);

		internal void GetProcVar(Int32 procedure, Int32 variable, out VariableData data) => this._getProcVar(procedure, variable, out data);

		internal Int32 NamespaceSize() => this._namespaceSize();

		internal void GetNamespace(Byte[] buffer) => this._getNamespace(buffer);

		internal Int32 StringspaceSize() => this._stringspaceSize();

		internal void GetStringspace(Byte[] buffer) => this._getStringspace(buffer);

		internal void GetProcRefs(Int32 procedure, Int32[] buffer) => this._getProcRefs(procedure, buffer);

		internal void GetVarRefs(Int32 variable, Int32[] buffer) => this._getVarRefs(variable, buffer);

		internal void GetProcVarRefs(Int32 procedure, Int32 variable, Int32[] buffer) => this._getProcVarRefs(procedure, variable, buffer);

		/// <inheritdoc />
		public void Dispose()
		{
			if (!this._disposed)
			{
				this._disposed = true;
				NativeLibrary.Free(this._handle);
			}
		}

		private static T Bind<T>(IntPtr handle, String name, Int32 argumentBytes)
			where T : Delegate
		{
			if (!NativeLibrary.TryGetExport(handle, name, out IntPtr address)
				&& !NativeLibrary.TryGetExport(handle, "_" + name + "@" + argumentBytes, out address)
				&& !NativeLibrary.TryGetExport(handle, name + "@" + argumentBytes, out address))
			{
				throw new EntryPointNotFoundException("The native parser does not export '" + name + "'.");
			}

			return Marshal.GetDelegateForFunctionPointer<T>(address);
		}
	}
}
