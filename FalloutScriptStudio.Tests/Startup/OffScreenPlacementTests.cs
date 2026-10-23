namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using FalloutScriptStudio.UiTesting;
	using Xunit;

	/// <summary>The rules that keep the windows out of sight under the UI test mode.</summary>
	public sealed class OffScreenPlacementTests
	{
		[Fact]
		public void A_window_without_a_position_goes_far_to_the_upper_left()
		{
			Assert.Equal(-20000, OffScreenPlacement.Move(OffScreenPlacement.UseDefault));
		}

		[Theory]
		[InlineData(0)]
		[InlineData(100)]
		[InlineData(1920)]
		[InlineData(-1200)]
		[InlineData(-9999)]
		[InlineData(7680)]
		public void A_coordinate_on_a_monitor_is_shifted_out_of_every_monitor(Int32 value)
		{
			Int32 moved = OffScreenPlacement.Move(value);

			Assert.Equal(value - OffScreenPlacement.Shift, moved);
			Assert.True(OffScreenPlacement.IsOffScreen(moved));
		}

		[Theory]
		[InlineData(-10000)]
		[InlineData(-20000)]
		[InlineData(-24900)]
		public void A_coordinate_that_is_off_screen_is_left_alone_so_that_the_shift_is_applied_once(Int32 value)
		{
			Assert.Equal(value, OffScreenPlacement.Move(value));
			Assert.True(OffScreenPlacement.IsOffScreen(value));
		}

		[Fact]
		public void Moving_twice_is_the_same_as_moving_once()
		{
			Int32 once = OffScreenPlacement.Move(500);

			Assert.Equal(once, OffScreenPlacement.Move(once));
		}

		[Fact]
		public void A_window_is_never_created_maximized_or_minimized_and_other_style_bits_are_kept()
		{
			UInt32 style = 0x00CF0000 | 0x01000000 | 0x20000000 | 0x10000000;

			UInt32 adjusted = OffScreenPlacement.AdjustStyle(style);

			Assert.Equal(0x00CF0000u | 0x10000000u, adjusted);
		}

		[Fact]
		public void A_window_cannot_be_activated_by_a_click_and_has_no_taskbar_button()
		{
			UInt32 adjusted = OffScreenPlacement.AdjustExtendedStyle(OffScreenPlacement.AppWindowExtendedStyle | 0x00000100);

			Assert.Equal(OffScreenPlacement.NoActivateExtendedStyle | 0x00000100u, adjusted);
		}

		[Fact]
		public void A_child_window_is_recognized_by_its_style()
		{
			Assert.True(OffScreenPlacement.IsChild(0x40000000 | 0x10000000));
			Assert.False(OffScreenPlacement.IsChild(0x10000000));
		}

		[Theory]
		[InlineData(0x0003u, true)]
		[InlineData(0x0005u, true)]
		[InlineData(0x0018u, true)]
		[InlineData(0x0047u, true)]
		[InlineData(0x000Fu, false)]
		[InlineData(0x0200u, false)]
		public void Only_the_messages_that_can_move_a_window_are_checked(UInt32 message, Boolean expected)
		{
			Assert.Equal(expected, OffScreenPlacement.IsPlacementMessage(message));
		}

		[Theory]
		[InlineData(0, 100, true)]
		[InlineData(4242, 100, true)]
		[InlineData(100, 100, false)]
		public void The_activation_is_refused_unless_the_program_already_owns_the_foreground(Int32 foreground, Int32 own, Boolean refused)
		{
			Assert.Equal(refused, OffScreenPlacement.ShouldRefuseActivation(foreground, own));
		}

		[Theory]
		[InlineData("HwndWrapper[FalloutScriptStudio.exe;;1234]", true)]
		[InlineData("HwndWrapper[DefaultDomain;;abc]", true)]
		[InlineData("Edit", false)]
		[InlineData("#32768", false)]
		[InlineData(null, false)]
		public void The_windows_of_wpf_are_recognized_by_their_class(String? className, Boolean expected)
		{
			Assert.Equal(expected, OffScreenPlacement.IsWpfWindowClass(className));
		}

		[Fact]
		public void The_move_keeps_the_size_the_order_and_the_activation_state()
		{
			Assert.Equal(0x0001u | 0x0004u | 0x0010u, OffScreenPlacement.MoveOnlyFlags);
		}
	}
}