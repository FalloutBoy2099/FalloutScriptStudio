namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Diagnostics;
	using System.Runtime.InteropServices;
	using System.Threading.Tasks;

	/// <summary>
	/// A Windows job object around an external tool. Every process the tool starts (cmd.exe running a batch file that runs mcpp, wcc or
	/// sslc...) belongs to the job too, so closing the job ends the whole tree even when an intermediate process is already gone.
	/// </summary>
	internal sealed class ProcessTreeJob : IDisposable
	{
		private const Int32 BasicAccountingInformation = 1;
		private const Int32 ExtendedLimitInformation = 9;
		private const UInt32 KillOnJobClose = 0x00002000;
		private const UInt32 DieOnUnhandledException = 0x00000400;

		private IntPtr _handle;

		private ProcessTreeJob(IntPtr handle)
		{
			this._handle = handle;
		}

		/// <summary>
		/// Puts a freshly started process into a new job that kills the tree when it is closed. Failures are ignored: the job is an
		/// additional safety net on top of <see cref="Process.Kill(Boolean)"/>.
		/// </summary>
		/// <param name="process">The started process.</param>
		/// <returns>The job, or <see langword="null"/> when it could not be created.</returns>
		public static ProcessTreeJob? TryCreate(Process process)
		{
			ArgumentNullException.ThrowIfNull(process);
			if (!OperatingSystem.IsWindows())
			{
				return null;
			}

			IntPtr handle = CreateJobObject(IntPtr.Zero, null);
			if (handle == IntPtr.Zero)
			{
				return null;
			}

			try
			{
				if (!SetLimits(handle, KillOnJobClose | DieOnUnhandledException) || AssignProcessToJobObject(handle, process.Handle) == 0)
				{
					_ = CloseHandle(handle);
					return null;
				}
			}
			catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
			{
				_ = CloseHandle(handle);
				return null;
			}

			return new ProcessTreeJob(handle);
		}

		/// <summary>Ends every process of the job now.</summary>
		public void Terminate()
		{
			if (this._handle != IntPtr.Zero)
			{
				_ = TerminateJobObject(this._handle, 1);
			}
		}

		/// <summary>Waits until no process of the job is running any more (the processes ended by <see cref="Terminate"/> need a moment to be gone).</summary>
		/// <param name="timeout">The longest time to wait.</param>
		/// <returns>A task that completes when the job is empty or the time is over.</returns>
		public async Task WaitUntilEmptyAsync(TimeSpan timeout)
		{
			Stopwatch watch = Stopwatch.StartNew();
			while (this._handle != IntPtr.Zero && watch.Elapsed < timeout && this.GetActiveProcessCount() > 0)
			{
				await Task.Delay(10).ConfigureAwait(false);
			}
		}

		/// <summary>Lets the processes of the job outlive it: used when the tool ended normally, so that a program it left running on purpose is not killed.</summary>
		public void Release()
		{
			if (this._handle != IntPtr.Zero)
			{
				_ = SetLimits(this._handle, 0);
			}
		}

		/// <summary>Closes the job, which ends the tree unless <see cref="Release"/> was called.</summary>
		public void Dispose()
		{
			IntPtr handle = this._handle;
			this._handle = IntPtr.Zero;
			if (handle != IntPtr.Zero)
			{
				_ = CloseHandle(handle);
			}
		}

		private Int32 GetActiveProcessCount()
		{
			BasicAccounting accounting = default;
			Boolean read = QueryInformationJobObject(this._handle, BasicAccountingInformation, ref accounting, (UInt32)Marshal.SizeOf<BasicAccounting>(), IntPtr.Zero) != 0;
			return read ? (Int32)accounting.ActiveProcesses : 0;
		}

		private static Boolean SetLimits(IntPtr job, UInt32 flags)
		{
			ExtendedLimits limits = new ExtendedLimits { Basic = new BasicLimits { LimitFlags = flags } };
			Int32 size = Marshal.SizeOf<ExtendedLimits>();
			IntPtr buffer = Marshal.AllocHGlobal(size);
			try
			{
				Marshal.StructureToPtr(limits, buffer, false);
				return SetInformationJobObject(job, ExtendedLimitInformation, buffer, (UInt32)size) != 0;
			}
			finally
			{
				Marshal.FreeHGlobal(buffer);
			}
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		private static extern IntPtr CreateJobObject(IntPtr attributes, String? name);

		[DllImport("kernel32.dll")]
		private static extern Int32 SetInformationJobObject(IntPtr job, Int32 informationClass, IntPtr information, UInt32 length);

		[DllImport("kernel32.dll")]
		private static extern Int32 QueryInformationJobObject(IntPtr job, Int32 informationClass, ref BasicAccounting information, UInt32 length, IntPtr returnLength);

		[DllImport("kernel32.dll")]
		private static extern Int32 AssignProcessToJobObject(IntPtr job, IntPtr process);

		[DllImport("kernel32.dll")]
		private static extern Int32 TerminateJobObject(IntPtr job, UInt32 exitCode);

		[DllImport("kernel32.dll")]
		private static extern Int32 CloseHandle(IntPtr handle);

		[StructLayout(LayoutKind.Sequential)]
		private struct BasicAccounting
		{
			public Int64 TotalUserTime;
			public Int64 TotalKernelTime;
			public Int64 ThisPeriodTotalUserTime;
			public Int64 ThisPeriodTotalKernelTime;
			public UInt32 TotalPageFaultCount;
			public UInt32 TotalProcesses;
			public UInt32 ActiveProcesses;
			public UInt32 TotalTerminatedProcesses;
		}

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
