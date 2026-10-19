namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class OperationTrackerTests
	{
		[Fact]
		public void Only_one_operation_runs_at_a_time_and_the_next_one_is_possible_after_it_ends()
		{
			OperationTracker tracker = new OperationTracker();

			IOperation? first = tracker.TryBegin(OperationKind.Compile, "a.ssl");
			Assert.NotNull(first);
			Assert.True(tracker.IsRunning);
			Assert.Null(tracker.TryBegin(OperationKind.Decompile, "b.int"));

			first.Dispose();

			Assert.False(tracker.IsRunning);
			Assert.Null(tracker.Current);
			using IOperation? second = tracker.TryBegin(OperationKind.Decompile, "b.int");
			Assert.NotNull(second);
		}

		[Fact]
		public void Cancel_cancels_the_token_marks_the_state_and_reports_only_once()
		{
			OperationTracker tracker = new OperationTracker();
			using IOperation operation = tracker.TryBegin(OperationKind.MassCompile, String.Empty, 10)!;

			Assert.False(operation.Token.IsCancellationRequested);
			Assert.True(tracker.Cancel());

			Assert.True(operation.Token.IsCancellationRequested);
			Assert.True(tracker.Current!.IsCancelling);
			Assert.False(tracker.Cancel());
			Assert.True(tracker.IsRunning);
		}

		[Fact]
		public void Cancel_without_an_operation_does_nothing()
		{
			Assert.False(new OperationTracker().Cancel());
		}

		[Fact]
		public void Reporting_updates_the_subject_and_the_progress_and_raises_the_event()
		{
			OperationTracker tracker = new OperationTracker();
			List<OperationInfo?> seen = [];
			tracker.Changed += (_, _) => seen.Add(tracker.Current);
			using IOperation operation = tracker.TryBegin(OperationKind.CompileAll, String.Empty, 4)!;

			operation.Report("b.ssl", 2, 4);

			OperationInfo current = tracker.Current!;
			Assert.Equal(OperationKind.CompileAll, current.Kind);
			Assert.Equal("b.ssl", current.Subject);
			Assert.Equal(2, current.Index);
			Assert.Equal(4, current.Total);
			Assert.Equal(0.5, current.Fraction);
			Assert.Equal(2, seen.Count);
		}

		[Fact]
		public void An_operation_without_a_count_has_an_unknown_progress()
		{
			OperationTracker tracker = new OperationTracker();
			using IOperation operation = tracker.TryBegin(OperationKind.Compile, "a.ssl")!;

			Assert.Equal(-1, tracker.Current!.Fraction);
			operation.Report("a.ssl", 3, 0);
			Assert.Equal(-1, tracker.Current.Fraction);
		}

		[Fact]
		public void Ending_an_operation_twice_or_cancelling_after_the_end_is_harmless()
		{
			OperationTracker tracker = new OperationTracker();
			IOperation operation = tracker.TryBegin(OperationKind.Compile, "a.ssl")!;
			Int32 raised = 0;
			tracker.Changed += (_, _) => raised++;

			operation.Dispose();
			operation.Dispose();
			operation.Report("late", 1, 1);

			Assert.Equal(1, raised);
			Assert.False(tracker.Cancel());
			Assert.False(tracker.IsRunning);
		}

		[Fact]
		public void The_fraction_stays_between_zero_and_one()
		{
			Assert.Equal(1, new OperationInfo(OperationKind.CompileAll, "a", 9, 4, false).Fraction);
			Assert.Equal(0, new OperationInfo(OperationKind.CompileAll, "a", 0, 4, false).Fraction);
		}
	}
}