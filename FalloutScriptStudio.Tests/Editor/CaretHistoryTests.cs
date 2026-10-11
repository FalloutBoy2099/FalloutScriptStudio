namespace FalloutScriptStudio.Tests.Editor
{
	using FalloutScriptStudio.Editor.Navigation;
	using Xunit;

	public sealed class CaretHistoryTests
	{
		[Fact]
		public void Empty_CannotNavigate()
		{
			CaretHistory history = new CaretHistory();

			Assert.False(history.CanGoBack);
			Assert.False(history.CanGoForward);
			Assert.Null(history.GoBack());
		}

		[Fact]
		public void Jumps_AreRecordedAndSmallMovesAreNot()
		{
			CaretHistory history = new CaretHistory();
			history.OnCaretMoved(new CaretLocation(10, 1));
			history.OnCaretMoved(new CaretLocation(11, 1));
			history.OnCaretMoved(new CaretLocation(12, 4));

			Assert.Equal(1, history.Count);
			Assert.False(history.CanGoBack);
		}

		[Fact]
		public void Back_ReturnsToThePreviousJumpTarget()
		{
			CaretHistory history = new CaretHistory();
			history.OnCaretMoved(new CaretLocation(10, 1));
			history.OnCaretMoved(new CaretLocation(50, 3));

			Assert.True(history.CanGoBack);
			Assert.Equal(new CaretLocation(10, 1), history.GoBack());
			Assert.True(history.CanGoForward);
			Assert.Equal(new CaretLocation(50, 3), history.GoForward());
			Assert.False(history.CanGoForward);
		}

		[Fact]
		public void MovesAfterAJump_UpdateTheCurrentEntry()
		{
			CaretHistory history = new CaretHistory();
			history.OnCaretMoved(new CaretLocation(10, 1));
			history.OnCaretMoved(new CaretLocation(50, 1));
			history.OnCaretMoved(new CaretLocation(51, 7));

			Assert.Equal(new CaretLocation(10, 1), history.GoBack());
			Assert.Equal(new CaretLocation(51, 7), history.GoForward());
		}

		[Fact]
		public void NewJumpAfterBack_DropsTheForwardEntries()
		{
			CaretHistory history = new CaretHistory();
			history.OnCaretMoved(new CaretLocation(10, 1));
			history.OnCaretMoved(new CaretLocation(50, 1));
			history.OnCaretMoved(new CaretLocation(90, 1));
			history.GoBack();
			history.OnCaretMoved(new CaretLocation(50, 1), false);
			history.OnCaretMoved(new CaretLocation(70, 1));

			Assert.False(history.CanGoForward);
			Assert.Equal(new CaretLocation(50, 1), history.GoBack());
		}

		[Fact]
		public void UnrecordedMove_DoesNotCreateAnEntry()
		{
			CaretHistory history = new CaretHistory();
			history.OnCaretMoved(new CaretLocation(10, 1));
			history.OnCaretMoved(new CaretLocation(80, 1), false);

			Assert.Equal(1, history.Count);
		}
	}
}
