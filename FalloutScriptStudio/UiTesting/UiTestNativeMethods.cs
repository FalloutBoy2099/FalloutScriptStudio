namespace FalloutScriptStudio.UiTesting
{
	using System;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>The window hook functions of the user interface test mode.</summary>
	internal static class UiTestNativeMethods
	{
		public const Int32 CallWndProcHook = 4;
		public const Int32 CbtHook = 5;
		public const Int32 CbtCreateWindow = 3;
		public const Int32 CbtActivate = 5;
		public const Int32 StyleIndex = -16;
		public const UInt32 CopyData = 0x004A;
		public const UInt32 SystemCommand = 0x0112;
		public const Int32 RestoreCommand = 0xF120;

		/// <summary>The signature of a hook procedure.</summary>
		/// <param name="code">The hook code.</param>
		/// <param name="wParam">The first parameter.</param>
		/// <param name="lParam">The second parameter.</param>
		/// <returns>The result of the hook.</returns>
		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		public delegate IntPtr HookProcedure(Int32 code, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr SetWindowsHookEx(Int32 hookId, HookProcedure callback, IntPtr module, UInt32 threadId);

		[DllImport("user32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern Boolean UnhookWindowsHookEx(IntPtr hook);

		[DllImport("user32.dll")]
		public static extern IntPtr CallNextHookEx(IntPtr hook, Int32 code, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		public static extern UInt32 GetWindowThreadProcessId(IntPtr window, out UInt32 processId);

		[DllImport("kernel32.dll")]
		public static extern UInt32 GetCurrentThreadId();

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern Int32 GetClassName(IntPtr window, StringBuilder name, Int32 capacity);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern Boolean SetWindowPos(IntPtr window, IntPtr insertAfter, Int32 x, Int32 y, Int32 width, Int32 height, UInt32 flags);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern Boolean GetWindowRect(IntPtr window, out Rectangle bounds);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern Boolean IsIconic(IntPtr window);

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern Boolean PostMessage(IntPtr window, UInt32 message, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
		public static extern Int32 GetWindowLong(IntPtr window, Int32 index);

		[StructLayout(LayoutKind.Sequential)]
		public struct CopyDataStruct
		{
			public UIntPtr Identifier;
			public UInt32 Size;
			public IntPtr Data;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Rectangle
		{
			public Int32 Left;
			public Int32 Top;
			public Int32 Right;
			public Int32 Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct CbtCreateWindowData
		{
			public IntPtr CreateStruct;
			public IntPtr InsertAfter;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct CreateStruct
		{
			public IntPtr CreateParams;
			public IntPtr Instance;
			public IntPtr Menu;
			public IntPtr Parent;
			public Int32 Height;
			public Int32 Width;
			public Int32 Y;
			public Int32 X;
			public Int32 Style;
			public IntPtr Name;
			public IntPtr ClassName;
			public UInt32 ExtendedStyle;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct CallWindowProcedureData
		{
			public IntPtr LParam;
			public IntPtr WParam;
			public UInt32 Message;
			public IntPtr Window;
		}
	}
}