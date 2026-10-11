namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Diagnostics;
	using System.Runtime.InteropServices;

	/// <summary>
	/// A Windows job object around the parser host. It makes the operating system kill the host silently when it has an unhandled
	/// exception (no "has stopped working" box, no hang in crash reporting) and when the application disappears.
	/// </summary>
	internal sealed class HostJob : IDisposable
	{
		private const Int32 ExtendedLimitInformation = 9;
		private const UInt32 DieOnUnhandledException = 0x00000400;
		private const UInt32 KillOnJobClose = 0x00002000;

		private IntPtr _handle;

		private HostJob(IntPtr handle)
		{
			this._handle = handle;
		}

		/// <summary>
		/// Puts a process into a new job. Failures are ignored: the job is an additional safety net, the host works without it.
		/// </summary>
		/// <param name="process">The freshly started host.</param>
		/// <returns>The job, or <c>null</c> when it could not be created (other platform, restricted environment).</returns>
		public static HostJob? TryCreate(Process process)
		{
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}

			IntPtr handle = CreateJobObject(IntPtr.Zero, null);
			if (handle == IntPtr.Zero)
			{
				return null;
			}

			var limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = DieOnUnhandledException | KillOnJobClose } };
			Int32 size = Marshal.SizeOf<ExtendedLimits>();
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.StructureToPtr(limits, buffer, false);
				if (SetInformationJobObject(handle, ExtendedLimitInformation, buffer, (UInt32)size) == 0
					|| AssignProcessToJobObject(handle, process.Handle) == 0)
				{
					CloseHandle(handle);
					return null;
				}
			}
			catch (InvalidOperationException)
			{
				CloseHandle(handle);
				return null;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}

			return new HostJob(handle);
		}

		/// <summary>Closes the job, which kills the host if it is still running.</summary>
		public void Dispose()
		{
			IntPtr handle = this._handle;
			this._handle = IntPtr.Zero;
			if (handle != IntPtr.Zero)
			{
				CloseHandle(handle);
			}
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr CreateJobObject(IntPtr attributes, String? name);

		[DllImport("kernel32.dll")]
		private static extern Int32 SetInformationJobObject(IntPtr job, Int32 informationClass, IntPtr information, UInt32 length);

		[DllImport("kernel32.dll")]
		private static extern Int32 AssignProcessToJobObject(IntPtr job, IntPtr process);

		[DllImport("kernel32.dll")]
		private static extern Int32 CloseHandle(IntPtr handle);

		[StructLayout(LayoutKind.Sequential)]
		private struct BasicLimits
		{
			public Int64 PerProcessUserTimeLimit;
			public Int64 PerJobUserTimeLimit;
			public UInt32 LimitFlags;
			public UIntPtr MinimumWorkingSetSize;
			public UIntPtr MaximumWorkingSetSize;
			public UInt32 ActiveProcessLimit;
			public UIntPtr Affinity;
			public UInt32 PriorityClass;
			public UInt32 SchedulingClass;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct IoCounters
		{
			public UInt64 ReadOperationCount;
			public UInt64 WriteOperationCount;
			public UInt64 OtherOperationCount;
			public UInt64 ReadTransferCount;
			public UInt64 WriteTransferCount;
			public UInt64 OtherTransferCount;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct ExtendedLimits
		{
			public BasicLimits Basic;
			public IoCounters Io;
			public UIntPtr ProcessMemoryLimit;
			public UIntPtr JobMemoryLimit;
			public UIntPtr PeakProcessMemoryUsed;
			public UIntPtr PeakJobMemoryUsed;
		}
	}
}
