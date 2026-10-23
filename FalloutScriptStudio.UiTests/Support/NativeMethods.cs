namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.InteropServices;
	using System.Text;

	/// <summary>The window functions the smoke tests need: listing the windows of a process, capturing a window that is off-screen, posting keys to one window.</summary>
	internal static class NativeMethods
	{
		public const UInt32 KeyDown = 0x0100;
		public const UInt32 KeyUp = 0x0101;
		public const UInt32 Char = 0x0102;
		public const UInt32 Close = 0x0010;

		private const UInt32 RenderFullContent = 0x2;

		private delegate Boolean EnumerateWindowsCallback(IntPtr window, IntPtr parameter);

		/// <summary>Makes the test process aware of the display scale, so that the rectangles of UI Automation and the size of a capture are in device pixels.</summary>
		public static void MakeDpiAware()
		{
			SetProcessDPIAware();
		}

		/// <summary>Lists the visible top-level windows that belong to a process (windows, dialogs, popups).</summary>
		/// <param name="processId">The process.</param>
		/// <returns>The window handles.</returns>
		public static IReadOnlyList<IntPtr> GetProcessWindows(Int32 processId)
		{
			List<IntPtr> windows = [];
			EnumWindows(
				(window, _) =>
				{
					GetWindowThreadProcessId(window, out UInt32 owner);
					if (owner == (UInt32)processId && IsWindowVisible(window))
					{
						windows.Add(window);
					}

					return true;
				},
				IntPtr.Zero);
			return windows;
		}

		/// <summary>Gets the title of a window.</summary>
		/// <param name="window">The window.</param>
		/// <returns>The title.</returns>
		public static String GetTitle(IntPtr window)
		{
			StringBuilder text = new(512);
			GetWindowText(window, text, text.Capacity);
			return text.ToString();
		}

		/// <summary>Gets the window style bits of a window.</summary>
		/// <param name="window">The window.</param>
		/// <returns>The style.</returns>
		public static UInt32 GetStyle(IntPtr window)
		{
			return (UInt32)GetWindowLong(window, -16);
		}

		/// <summary>Gets the class name of a window.</summary>
		/// <param name="window">The window.</param>
		/// <returns>The class name.</returns>
		public static String GetClass(IntPtr window)
		{
			StringBuilder text = new(256);
			GetClassName(window, text, text.Capacity);
			return text.ToString();
		}

		/// <summary>Gets the bounds of a window in device pixels.</summary>
		/// <param name="window">The window.</param>
		/// <returns>The left, top, width and height.</returns>
		public static (Int32 Left, Int32 Top, Int32 Width, Int32 Height) GetBounds(IntPtr window)
		{
			GetWindowRect(window, out Rect rect);
			return (rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
		}

		/// <summary>Gets whether the window of the foreground belongs to a process.</summary>
		/// <param name="processId">The process.</param>
		/// <returns><see langword="true"/> when the process owns the foreground window.</returns>
		public static Boolean OwnsForeground(Int32 processId)
		{
			IntPtr foreground = GetForegroundWindow();
			if (foreground == IntPtr.Zero)
			{
				return false;
			}

			GetWindowThreadProcessId(foreground, out UInt32 owner);
			return owner == (UInt32)processId;
		}

		/// <summary>Sends a command to the program under test (<c>key:Tab</c>, <c>text:abc</c>): it raises the key or types the text at the editor of the active document, without the keyboard or the focus.</summary>
		/// <param name="window">The main window of the program.</param>
		/// <param name="command">The command.</param>
		public static void SendInputCommand(IntPtr window, String command)
		{
			IntPtr text = Marshal.StringToHGlobalUni(command);
			try
			{
				CopyData data = new() { Identifier = new UIntPtr(0x55AA7E57), Size = (UInt32)(command.Length * 2), Data = text };
				IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<CopyData>());
				try
				{
					Marshal.StructureToPtr(data, buffer, false);
					SendMessageTimeout(window, 0x004A, IntPtr.Zero, buffer, 0x0002, 5000, out _);
				}
				finally
				{
					Marshal.FreeHGlobal(buffer);
				}
			}
			finally
			{
				Marshal.FreeHGlobal(text);
			}
		}

		/// <summary>Posts a message to a window (it never moves the focus).</summary>
		/// <param name="window">The window.</param>
		/// <param name="message">The message.</param>
		/// <param name="wParam">The first parameter.</param>
		/// <param name="lParam">The second parameter.</param>
		/// <returns><see langword="true"/> when the message was queued.</returns>
		public static Boolean Post(IntPtr window, UInt32 message, IntPtr wParam, IntPtr lParam)
		{
			return PostMessage(window, message, wParam, lParam);
		}

		/// <summary>Captures a window, also when it is outside of every monitor, with <c>PrintWindow</c>.</summary>
		/// <param name="window">The window.</param>
		/// <param name="width">Receives the width in pixels.</param>
		/// <param name="height">Receives the height in pixels.</param>
		/// <returns>The pixels as BGRA, or <see langword="null"/> when the window cannot be captured.</returns>
		public static Byte[]? CaptureWindow(IntPtr window, out Int32 width, out Int32 height, UInt32 flags = RenderFullContent)
		{
			(_, _, width, height) = GetBounds(window);
			if (width <= 0 || height <= 0)
			{
				return null;
			}

			BitmapInfo info = new()
			{
				Size = 40,
				Width = width,
				Height = -height,
				Planes = 1,
				BitCount = 32,
				Compression = 0,
			};
			IntPtr screen = GetDC(IntPtr.Zero);
			IntPtr memory = CreateCompatibleDC(screen);
			try
			{
				IntPtr bitmap = CreateDIBSection(memory, ref info, 0, out IntPtr bits, IntPtr.Zero, 0);
				if (bitmap == IntPtr.Zero)
				{
					return null;
				}

				IntPtr previous = SelectObject(memory, bitmap);
				try
				{
					if (!PrintWindow(window, memory, flags))
					{
						return null;
					}

					Byte[] pixels = new Byte[width * height * 4];
					Marshal.Copy(bits, pixels, 0, pixels.Length);
					return pixels;
				}
				finally
				{
					SelectObject(memory, previous);
					DeleteObject(bitmap);
				}
			}
			finally
			{
				DeleteDC(memory);
				ReleaseDC(IntPtr.Zero, screen);
			}
		}

		[DllImport("user32.dll")]
		private static extern Boolean EnumWindows(EnumerateWindowsCallback callback, IntPtr parameter);

		[DllImport("user32.dll")]
		private static extern UInt32 GetWindowThreadProcessId(IntPtr window, out UInt32 processId);

		[DllImport("user32.dll")]
		private static extern Boolean IsWindowVisible(IntPtr window);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern Int32 GetWindowText(IntPtr window, StringBuilder text, Int32 capacity);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		private static extern Int32 GetClassName(IntPtr window, StringBuilder text, Int32 capacity);

		[DllImport("user32.dll")]
		private static extern Boolean GetWindowRect(IntPtr window, out Rect rect);

		[DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
		private static extern Int32 GetWindowLong(IntPtr window, Int32 index);

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessageTimeout(IntPtr window, UInt32 message, IntPtr wParam, IntPtr lParam, UInt32 flags, UInt32 timeout, out IntPtr result);

		[DllImport("user32.dll")]
		private static extern Boolean PostMessage(IntPtr window, UInt32 message, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		private static extern Boolean PrintWindow(IntPtr window, IntPtr context, UInt32 flags);

		[DllImport("user32.dll")]
		private static extern Boolean SetProcessDPIAware();

		[DllImport("user32.dll")]
		private static extern IntPtr GetDC(IntPtr window);

		[DllImport("user32.dll")]
		private static extern Int32 ReleaseDC(IntPtr window, IntPtr context);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateCompatibleDC(IntPtr context);

		[DllImport("gdi32.dll")]
		private static extern Boolean DeleteDC(IntPtr context);

		[DllImport("gdi32.dll")]
		private static extern IntPtr CreateDIBSection(IntPtr context, ref BitmapInfo info, UInt32 usage, out IntPtr bits, IntPtr section, UInt32 offset);

		[DllImport("gdi32.dll")]
		private static extern IntPtr SelectObject(IntPtr context, IntPtr handle);

		[DllImport("gdi32.dll")]
		private static extern Boolean DeleteObject(IntPtr handle);

		[StructLayout(LayoutKind.Sequential)]
		private struct CopyData
		{
			public UIntPtr Identifier;
			public UInt32 Size;
			public IntPtr Data;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct Rect
		{
			public Int32 Left;
			public Int32 Top;
			public Int32 Right;
			public Int32 Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct BitmapInfo
		{
			public UInt32 Size;
			public Int32 Width;
			public Int32 Height;
			public UInt16 Planes;
			public UInt16 BitCount;
			public UInt32 Compression;
			public UInt32 ImageSize;
			public Int32 HorizontalResolution;
			public Int32 VerticalResolution;
			public UInt32 ColorsUsed;
			public UInt32 ColorsImportant;
		}
	}
}