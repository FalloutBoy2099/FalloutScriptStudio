namespace FalloutScriptStudio.ParserHost
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;

	/// <summary>
	/// Makes a crash of the native library end the process at once, silently, with the exception code as exit code.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <c>parser.dll</c> has bugs that raise access violations on perfectly ordinary half-typed scripts (for example a procedure
	/// declared twice). The runtime reacts to such a crash with a fatal error report and then hangs in the crash reporting of the
	/// operating system; when Windows Error Reporting is disabled, Windows shows the old modal box "Exception Processing Message
	/// 0xc0000005 - Unexpected parameters" and the process stays alive until somebody clicks it away.
	/// </para>
	/// <para>
	/// A vectored exception handler that runs before the runtime's own calls <c>TerminateProcess</c> for hardware exceptions raised
	/// by native code while a parse is in progress (see <see cref="EnterNativeRegion"/>). Exceptions in managed code (the runtime turns
	/// its access violations into <c>NullReferenceException</c>, the JIT-compiled code is not part of any module) and in the runtime's
	/// own modules are left alone. The process-wide unhandled exception filter and the error mode are set as well, and the application
	/// starts the host inside a job object that has the same effect (see <c>ProcessParserHost</c>).
	/// </para>
	/// </remarks>
	internal static unsafe class CrashSilencer
	{
		// SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX
		private const UInt32 SilentErrorMode = 0x0001 | 0x0002 | 0x8000;

		private const Int32 ExceptionContinueSearch = 0;
		private const UInt32 GetModuleHandleFromAddress = 0x00000004 | 0x00000002; // FROM_ADDRESS | UNCHANGED_REFCOUNT
		private const Int32 MaxPath = 520;

		private static Int32 s_nativeRegion;

		/// <summary>Installs the handlers and the error mode of the process.</summary>
		public static void Install()
		{
			_ = SetErrorMode(SilentErrorMode);
			_ = AddVectoredExceptionHandler(1, (IntPtr)(delegate* unmanaged[Stdcall]<void*, Int32>)&OnVectoredException);
			_ = SetUnhandledExceptionFilter((IntPtr)(delegate* unmanaged[Stdcall]<void*, Int32>)&OnUnhandledException);
		}

		/// <summary>Marks the start of a call into the native library: from now on a hardware exception in native code kills the process.</summary>
		public static void EnterNativeRegion()
		{
			Volatile.Write(ref s_nativeRegion, 1);
		}

		/// <summary>Marks the end of the call into the native library.</summary>
		public static void ExitNativeRegion()
		{
			Volatile.Write(ref s_nativeRegion, 0);
		}

		[DllImport("kernel32.dll")]
		private static extern UInt32 SetErrorMode(UInt32 mode);

		[DllImport("kernel32.dll")]
		private static extern IntPtr SetUnhandledExceptionFilter(IntPtr filter);

		[DllImport("kernel32.dll")]
		private static extern IntPtr AddVectoredExceptionHandler(UInt32 first, IntPtr handler);

		[DllImport("kernel32.dll")]
		private static extern IntPtr GetCurrentProcess();

		[DllImport("kernel32.dll")]
		private static extern Int32 TerminateProcess(IntPtr process, UInt32 exitCode);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern Int32 GetModuleHandleExW(UInt32 flags, void* address, out IntPtr module);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern UInt32 GetModuleFileNameW(IntPtr module, Char* fileName, UInt32 size);

		private static Boolean IsFatalHardwareException(UInt32 code)
		{
			return code is 0xC0000005 // access violation
				or 0xC000001D // illegal instruction
				or 0xC0000094 // integer divide by zero
				or 0xC0000096 // privileged instruction
				or 0xC000008C; // array bounds exceeded
		}

		/// <summary>Tells whether an address belongs to a module of the runtime or to managed code, where the runtime handles the exception itself.</summary>
		private static Boolean BelongsToTheRuntimeOrManagedCode(void* address)
		{
			if (GetModuleHandleExW(GetModuleHandleFromAddress, address, out IntPtr module) == 0)
			{
				// Not inside any module: JIT-compiled code or a runtime stub.
				return true;
			}

			Char* buffer = stackalloc Char[MaxPath];
			UInt32 length = GetModuleFileNameW(module, buffer, MaxPath);
			if (length == 0)
			{
				return true;
			}

			ReadOnlySpan<Char> path = new ReadOnlySpan<Char>(buffer, (Int32)length);
			ReadOnlySpan<Char> name = path.Slice(path.LastIndexOf('\\') + 1);
			return name.StartsWith("coreclr", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("clr", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("System.", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("FalloutScriptStudio.", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("hostpolicy", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("hostfxr", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>The vectored handler: <c>EXCEPTION_POINTERS</c> starts with a pointer to an <see cref="ExceptionRecord"/>.</summary>
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		private static Int32 OnVectoredException(void* exceptionPointers)
		{
			ExceptionRecord* record = exceptionPointers is null ? null : *(ExceptionRecord**)exceptionPointers;
			if (record is not null
				&& Volatile.Read(ref s_nativeRegion) == 1
				&& IsFatalHardwareException(record->Code)
				&& !BelongsToTheRuntimeOrManagedCode(record->Address))
			{
				_ = TerminateProcess(GetCurrentProcess(), record->Code);
			}

			return ExceptionContinueSearch;
		}

		/// <summary>The unhandled exception filter: whatever reaches it ends the process at once.</summary>
		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
		private static Int32 OnUnhandledException(void* exceptionPointers)
		{
			ExceptionRecord* record = exceptionPointers is null ? null : *(ExceptionRecord**)exceptionPointers;
			_ = TerminateProcess(GetCurrentProcess(), record is null ? 0xC0000005 : record->Code);
			return 1; // EXCEPTION_EXECUTE_HANDLER, in case TerminateProcess returned
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ExceptionRecord
		{
			public UInt32 Code;
			public UInt32 Flags;
			public ExceptionRecord* Next;
			public void* Address;
		}
	}
}