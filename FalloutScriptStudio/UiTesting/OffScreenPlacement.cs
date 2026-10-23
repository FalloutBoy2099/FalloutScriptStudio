namespace FalloutScriptStudio.UiTesting
{
	using System;

	/// <summary>
	/// The rules that keep the windows of the program out of sight under the UI tests (<see cref="UiTestMode"/>): where a window goes, and
	/// when the system may activate one. Pure; <see cref="UiTestWindowGuard"/> applies them to the real windows.
	/// </summary>
	public static class OffScreenPlacement
	{
		/// <summary>The coordinate a window without a position gets: far to the left of and above every monitor.</summary>
		public const Int32 Coordinate = -20000;

		/// <summary>The shift applied to a coordinate that is still on a monitor; the geometry of windows and popups relative to each other is kept.</summary>
		public const Int32 Shift = 25000;

		/// <summary>The coordinate under which a window already counts as off-screen (it is left alone, so the shift is applied once).</summary>
		public const Int32 Threshold = -10000;

		/// <summary>The value of <c>CW_USEDEFAULT</c>: the system chooses the position.</summary>
		public const Int32 UseDefault = Int32.MinValue;

		/// <summary>The window style bit of a child window (<c>WS_CHILD</c>).</summary>
		public const UInt32 ChildStyle = 0x40000000;

		/// <summary>The window style bits of a maximized (<c>WS_MAXIMIZE</c>) and a minimized (<c>WS_MINIMIZE</c>) window: such a window sits on a monitor and cannot be moved out of sight.</summary>
		public const UInt32 StateStyles = 0x01000000 | 0x20000000;

		/// <summary>The message sent when the size of a window changed (<c>WM_SIZE</c>), and the parameter that tells it was maximized (<c>SIZE_MAXIMIZED</c>).</summary>
		public const UInt32 SizeMessage = 0x0005;

		/// <summary>The parameter of <see cref="SizeMessage"/> for a maximized window.</summary>
		public const Int32 SizeMaximized = 2;

		/// <summary>The extended style that stops a click from activating a window and keeps it off the taskbar (<c>WS_EX_NOACTIVATE</c>).</summary>
		public const UInt32 NoActivateExtendedStyle = 0x08000000;

		/// <summary>The extended style that forces a taskbar button (<c>WS_EX_APPWINDOW</c>).</summary>
		public const UInt32 AppWindowExtendedStyle = 0x00040000;

		/// <summary>The position flag that keeps the current position (<c>SWP_NOMOVE</c>).</summary>
		public const UInt32 NoMoveFlag = 0x0002;

		/// <summary>The position flag that stops the window from being activated (<c>SWP_NOACTIVATE</c>).</summary>
		public const UInt32 NoActivateFlag = 0x0010;

		/// <summary>The flags of the move to the off-screen position: neither the size, the order nor the activation change.</summary>
		public const UInt32 MoveOnlyFlags = 0x0001 | 0x0004 | NoActivateFlag;

		/// <summary>Gets the coordinate a window is moved to.</summary>
		/// <param name="value">The coordinate the program or the system chose.</param>
		/// <returns>A coordinate outside of every monitor.</returns>
		public static Int32 Move(Int32 value)
		{
			if (value == UseDefault)
			{
				return Coordinate;
			}

			return value <= Threshold ? value : value - Shift;
		}

		/// <summary>Gets whether a coordinate is outside of every monitor already.</summary>
		/// <param name="value">The coordinate.</param>
		/// <returns><see langword="true"/> when the window cannot be seen.</returns>
		public static Boolean IsOffScreen(Int32 value)
		{
			return value <= Threshold;
		}

		/// <summary>Gets whether a window style belongs to a child window, which is never moved.</summary>
		/// <param name="style">The window style.</param>
		/// <returns><see langword="true"/> for a child window.</returns>
		public static Boolean IsChild(UInt32 style)
		{
			return (style & ChildStyle) != 0;
		}

		/// <summary>Gets whether a message may have moved a window onto a monitor (<c>WM_MOVE</c>, <c>WM_SIZE</c>, <c>WM_SHOWWINDOW</c>, <c>WM_WINDOWPOSCHANGED</c>): the window is then checked and moved away.</summary>
		/// <param name="message">The message.</param>
		/// <returns><see langword="true"/> when the position of the window is checked.</returns>
		public static Boolean IsPlacementMessage(UInt32 message)
		{
			return message is 0x0003 or SizeMessage or 0x0018 or 0x0047;
		}

		/// <summary>Gets the style of a new window: it is never created maximized or minimized.</summary>
		/// <param name="style">The style the program asked for.</param>
		/// <returns>The style to use.</returns>
		public static UInt32 AdjustStyle(UInt32 style)
		{
			return style & ~StateStyles;
		}

		/// <summary>Gets the extended style of a new window: it cannot be activated by a click and has no taskbar button.</summary>
		/// <param name="extendedStyle">The extended style the program asked for.</param>
		/// <returns>The extended style to use.</returns>
		public static UInt32 AdjustExtendedStyle(UInt32 extendedStyle)
		{
			return (extendedStyle | NoActivateExtendedStyle) & ~AppWindowExtendedStyle;
		}

		/// <summary>Gets whether the activation of a window must be refused: it would take the focus from the program the developer works in.</summary>
		/// <param name="foregroundProcessId">The process that owns the foreground window (0 when there is none).</param>
		/// <param name="ownProcessId">This process.</param>
		/// <returns><see langword="true"/> when the activation is refused.</returns>
		public static Boolean ShouldRefuseActivation(Int32 foregroundProcessId, Int32 ownProcessId)
		{
			return foregroundProcessId != ownProcessId;
		}

		/// <summary>Gets whether a window class is one of the windows WPF creates for its windows and popups.</summary>
		/// <param name="className">The class name.</param>
		/// <returns><see langword="true"/> for a WPF window.</returns>
		public static Boolean IsWpfWindowClass(String? className)
		{
			return className is not null && className.StartsWith("HwndWrapper[", StringComparison.Ordinal);
		}
	}
}