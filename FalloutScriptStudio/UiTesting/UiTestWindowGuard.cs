namespace FalloutScriptStudio.UiTesting
{
	using System;
	using System.Diagnostics;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>
	/// Keeps every window of the program out of sight and passive under the UI tests (<see cref="UiTestMode"/>): two hooks of the user
	/// interface thread move each top-level window (windows, dialogs, popups, menus, tool tips) to a position outside of every monitor
	/// when it is created and whenever the system or WPF moves it, give it the style that stops a click from activating it and removes its
	/// taskbar button, and refuse its activation while another program owns the foreground. Keys and text for the editor arrive in
	/// <c>WM_COPYDATA</c> messages and go to <see cref="UiTestInput"/>, because the program never has the keyboard focus. It is created only
	/// when the mode is on.
	/// </summary>
	public sealed class UiTestWindowGuard : IDisposable
	{
		private readonly UiTestNativeMethods.HookProcedure _createHook;
		private readonly UiTestNativeMethods.HookProcedure _moveHook;
		private readonly Int32 _processId = Environment.ProcessId;
		private IntPtr _createHandle;
		private IntPtr _moveHandle;

		private UiTestWindowGuard()
		{
			this._createHook = this.OnCbt;
			this._moveHook = this.OnCallWindowProcedure;
		}

		/// <summary>Gets a value indicating whether both hooks are installed.</summary>
		public Boolean IsInstalled => this._createHandle != IntPtr.Zero && this._moveHandle != IntPtr.Zero;

		/// <summary>Installs the hooks on the calling (user interface) thread.</summary>
		/// <returns>The guard; disposing it removes the hooks.</returns>
		public static UiTestWindowGuard Install()
		{
			UiTestWindowGuard guard = new();
			UInt32 thread = UiTestNativeMethods.GetCurrentThreadId();
			guard._createHandle = UiTestNativeMethods.SetWindowsHookEx(UiTestNativeMethods.CbtHook, guard._createHook, IntPtr.Zero, thread);
			guard._moveHandle = UiTestNativeMethods.SetWindowsHookEx(UiTestNativeMethods.CallWndProcHook, guard._moveHook, IntPtr.Zero, thread);
			return guard;
		}

		/// <summary>Removes the hooks.</summary>
		public void Dispose()
		{
			if (this._createHandle != IntPtr.Zero)
			{
				UiTestNativeMethods.UnhookWindowsHookEx(this._createHandle);
				this._createHandle = IntPtr.Zero;
			}

			if (this._moveHandle != IntPtr.Zero)
			{
				UiTestNativeMethods.UnhookWindowsHookEx(this._moveHandle);
				this._moveHandle = IntPtr.Zero;
			}
		}

		private static void AdjustNewWindow(IntPtr window, IntPtr lParam)
		{
			UiTestNativeMethods.CbtCreateWindowData data = Marshal.PtrToStructure<UiTestNativeMethods.CbtCreateWindowData>(lParam);
			if (data.CreateStruct == IntPtr.Zero)
			{
				return;
			}

			UiTestNativeMethods.CreateStruct create = Marshal.PtrToStructure<UiTestNativeMethods.CreateStruct>(data.CreateStruct);
			if (OffScreenPlacement.IsChild((UInt32)create.Style) || !OffScreenPlacement.IsWpfWindowClass(ReadClassName(window)))
			{
				return;
			}

			create.Style = (Int32)OffScreenPlacement.AdjustStyle((UInt32)create.Style);
			create.X = OffScreenPlacement.Move(create.X);
			create.Y = OffScreenPlacement.Move(create.Y);
			create.ExtendedStyle = OffScreenPlacement.AdjustExtendedStyle(create.ExtendedStyle);
			Marshal.StructureToPtr(create, data.CreateStruct, false);
		}

		private static void ReceiveCommand(IntPtr lParam)
		{
			// The text is valid during the call of SendMessage only: it is copied before the command is queued.
			UiTestNativeMethods.CopyDataStruct data = Marshal.PtrToStructure<UiTestNativeMethods.CopyDataStruct>(lParam);
			if ((UInt32)data.Identifier == UiTestInput.CommandId && data.Data != IntPtr.Zero && data.Size > 0)
			{
				UiTestInput.Post(Marshal.PtrToStringUni(data.Data, (Int32)data.Size / 2).TrimEnd('\0'));
			}
		}

		private static void MoveOffScreen(IntPtr window)
		{
			// The system does not honor a change of the WINDOWPOS that a hook receives, so the window is moved after the fact: it is
			// invisible while it is created, and a later move (also the restore of a window that was maximized) is corrected inside the
			// same call that made it, before the window is painted at the new place.
			if (OffScreenPlacement.IsChild((UInt32)UiTestNativeMethods.GetWindowLong(window, UiTestNativeMethods.StyleIndex))
				|| UiTestNativeMethods.IsIconic(window)
				|| !UiTestNativeMethods.GetWindowRect(window, out UiTestNativeMethods.Rectangle bounds)
				|| (OffScreenPlacement.IsOffScreen(bounds.Left) && OffScreenPlacement.IsOffScreen(bounds.Top)))
			{
				return;
			}

			UiTestNativeMethods.SetWindowPos(window, IntPtr.Zero, OffScreenPlacement.Move(bounds.Left), OffScreenPlacement.Move(bounds.Top), 0, 0, OffScreenPlacement.MoveOnlyFlags);
		}

		private static String? ReadClassName(IntPtr window)
		{
			StringBuilder name = new(256);
			return UiTestNativeMethods.GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : null;
		}

		private IntPtr OnCbt(Int32 code, IntPtr wParam, IntPtr lParam)
		{
			try
			{
				if (code == UiTestNativeMethods.CbtCreateWindow)
				{
					AdjustNewWindow(wParam, lParam);
				}
				else if (code == UiTestNativeMethods.CbtActivate && this.MustRefuseActivation())
				{
					return new IntPtr(1);
				}
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				// A hook must never throw into the window manager; the window simply keeps the position the program chose.
				Debug.WriteLine("UiTestWindowGuard: " + ex.Message);
			}

			return UiTestNativeMethods.CallNextHookEx(this._createHandle, code, wParam, lParam);
		}

		private IntPtr OnCallWindowProcedure(Int32 code, IntPtr wParam, IntPtr lParam)
		{
			try
			{
				if (code >= 0 && lParam != IntPtr.Zero)
				{
					UiTestNativeMethods.CallWindowProcedureData message = Marshal.PtrToStructure<UiTestNativeMethods.CallWindowProcedureData>(lParam);
					if (message.Message == UiTestNativeMethods.CopyData)
					{
						ReceiveCommand(message.LParam);
					}

					if (OffScreenPlacement.IsPlacementMessage(message.Message))
					{
						MoveOffScreen(message.Window);
					}

					if (message.Message == OffScreenPlacement.SizeMessage && (Int32)message.WParam == OffScreenPlacement.SizeMaximized)
					{
						// A maximized window is pinned to a monitor: it is restored at once (and then moved away like any other window).
						UiTestNativeMethods.PostMessage(message.Window, UiTestNativeMethods.SystemCommand, new IntPtr(UiTestNativeMethods.RestoreCommand), IntPtr.Zero);
					}
				}
			}
			catch (Exception ex) when (ex is not OutOfMemoryException)
			{
				Debug.WriteLine("UiTestWindowGuard: " + ex.Message);
			}

			return UiTestNativeMethods.CallNextHookEx(this._moveHandle, code, wParam, lParam);
		}

		private Boolean MustRefuseActivation()
		{
			IntPtr foreground = UiTestNativeMethods.GetForegroundWindow();
			UInt32 owner = 0;
			if (foreground != IntPtr.Zero)
			{
				UiTestNativeMethods.GetWindowThreadProcessId(foreground, out owner);
			}

			return OffScreenPlacement.ShouldRefuseActivation((Int32)owner, this._processId);
		}
	}
}