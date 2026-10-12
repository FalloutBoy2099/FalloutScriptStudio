namespace FalloutScriptStudio.Tests.Problems
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Problems;
	using Xunit;

	public sealed class ProblemServiceTests
	{
		private static Problem Make(String text, ProblemSource source = ProblemSource.Tool, ProblemSeverity severity = ProblemSeverity.Error, Object? owner = null, String? origin = null, String? file = null, Int32 line = -1)
		{
			return new Problem { Severity = severity, Source = source, Text = text, Owner = owner, Origin = origin, File = file, Line = line };
		}

		[Fact]
		public void Reporting_the_same_problem_again_replaces_it_instead_of_listing_it_twice()
		{
			ProblemService service = new ProblemService();
			Int32 raised = 0;
			service.Changed += (_, _) => raised++;

			service.Report(Make("compile.exe is missing"));
			service.Report(Make("compile.exe is missing"));
			service.Report(Make("another problem"));

			Assert.Equal(new[] { "compile.exe is missing", "another problem" }, service.Problems.Select(problem => problem.Message));
			Assert.Equal(3, raised);
		}

		[Fact]
		public void The_same_text_for_two_owners_is_two_problems()
		{
			ProblemService service = new ProblemService();
			Object first = new Object();
			Object second = new Object();

			service.Report(Make("same", owner: first));
			service.Report(Make("same", owner: second));

			Assert.Equal(2, service.Problems.Count);
		}

		[Fact]
		public void Clearing_an_owner_drops_only_its_problems_and_can_be_limited_to_sources()
		{
			ProblemService service = new ProblemService();
			Object document = new Object();
			Object other = new Object();
			service.Report(Make("parse", ProblemSource.Parse, owner: document));
			service.Report(Make("build", ProblemSource.Build, owner: document));
			service.Report(Make("file", ProblemSource.File, owner: document));
			service.Report(Make("other", ProblemSource.Build, owner: other));
			service.Report(Make("global"));

			Int32 parse = service.ClearOwner(document, ProblemSource.Parse);
			Int32 buildAndFile = service.ClearOwner(document, ProblemSource.Build, ProblemSource.File);

			Assert.Equal(1, parse);
			Assert.Equal(2, buildAndFile);
			Assert.Equal(new[] { "other", "global" }, service.Problems.Select(problem => problem.Message));
		}

		[Fact]
		public void Clearing_an_owner_without_sources_drops_everything_of_the_owner()
		{
			ProblemService service = new ProblemService();
			Object document = new Object();
			service.Report(Make("parse", ProblemSource.Parse, owner: document));
			service.Report(Make("file", ProblemSource.File, owner: document));
			service.Report(Make("global"));

			Assert.Equal(2, service.ClearOwner(document));

			Assert.Equal("global", Assert.Single(service.Problems).Message);
		}

		[Fact]
		public void Clearing_an_origin_drops_what_that_producer_reported()
		{
			ProblemService service = new ProblemService();
			service.Report(Make("a", origin: ProblemOrigins.BuildTool));
			service.Report(Make("b", origin: ProblemOrigins.NativeParser));
			service.Report(Make("c"));

			Assert.Equal(1, service.ClearOrigin(ProblemOrigins.BuildTool));

			Assert.Equal(new[] { "b", "c" }, service.Problems.Select(problem => problem.Message));
		}

		[Fact]
		public void Nothing_is_announced_when_nothing_was_removed()
		{
			ProblemService service = new ProblemService();
			service.Report(Make("a"));
			Int32 raised = 0;
			service.Changed += (_, _) => raised++;

			Assert.Equal(0, service.ClearOrigin("unknown"));
			Assert.Equal(0, service.ClearOwner(new Object()));

			Assert.Equal(0, raised);
			service.Clear();
			Assert.Equal(1, raised);
			Assert.Empty(service.Problems);
		}

		[Fact]
		public void A_range_is_announced_once()
		{
			ProblemService service = new ProblemService();
			Int32 raised = 0;
			service.Changed += (_, _) => raised++;

			service.ReportRange(Enumerable.Range(0, 50).Select(index => Make("problem " + index)));

			Assert.Equal(1, raised);
			Assert.Equal(50, service.Problems.Count);
		}

		[Fact]
		public void The_list_is_limited_and_the_oldest_problems_go_first()
		{
			ProblemService service = new ProblemService();

			service.ReportRange(Enumerable.Range(0, ProblemService.MaxProblems + 25).Select(index => Make("problem " + index)));

			Assert.Equal(ProblemService.MaxProblems, service.Problems.Count);
			Assert.Equal("problem 25", service.Problems[0].Message);
		}

		[Fact]
		public void The_snapshot_does_not_change_when_the_list_does()
		{
			ProblemService service = new ProblemService();
			service.Report(Make("a"));
			System.Collections.Generic.IReadOnlyList<Problem> snapshot = service.Problems;

			service.Clear();

			Assert.Single(snapshot);
		}
	}
}
