namespace FalloutScriptStudio.Core.Tests.Building
{
	using System;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public sealed class IncludeGraphTests : IDisposable
	{
		private readonly BuildWorkspace _w = new BuildWorkspace();
		private readonly IncludeGraph _graph = new IncludeGraph();

		public void Dispose()
		{
			this._w.Dispose();
		}

		private IncludeResolver Resolver => new IncludeResolver(this._w.Headers, this._w.Root);

		private DependencySet Dependencies(String script)
		{
			return this._graph.BeginScan(this.Resolver).GetDependencies(script);
		}

		[Fact]
		public void The_dependencies_are_transitive_and_sorted()
		{
			this._w.Write(@"headers\z.h", "#define Z 1\r\n");
			this._w.Write(@"headers\b.h", "#include \"z.h\"\r\n");
			this._w.Write(@"headers\a.h", "#include \"b.h\"\r\n#include \"z.h\"\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"a.h\"\r\n");

			DependencySet set = this.Dependencies(script);

			Assert.True(set.IsComplete);
			Assert.Equal(new[] { "a.h", "b.h", "z.h" }, set.Files.Select(Path.GetFileName));
			Assert.Empty(set.OptionalMissing);
		}

		[Fact]
		public void A_cycle_is_followed_once_and_the_script_is_not_its_own_dependency()
		{
			this._w.Write(@"headers\a.h", "#include \"b.h\"\r\n");
			this._w.Write(@"headers\b.h", "#include \"a.h\"\r\n#include \"..\\scripts\\s.ssl\"\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"a.h\"\r\n");

			DependencySet set = this.Dependencies(script);

			Assert.True(set.IsComplete);
			Assert.Equal(new[] { "a.h", "b.h" }, set.Files.Select(Path.GetFileName));
		}

		[Fact]
		public void Relative_includes_are_relative_to_the_including_file()
		{
			this._w.Write(@"scripts\sub\deep.h", "#define DEEP 1\r\n");
			this._w.Write(@"scripts\sub\mid.h", "#include \"deep.h\"\r\n#include \"..\\up.h\"\r\n");
			this._w.Write(@"scripts\up.h", "#define UP 1\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"sub\\mid.h\"\r\n");

			DependencySet set = this.Dependencies(script);

			Assert.True(set.IsComplete);
			Assert.Equal(new[] { "deep.h", "mid.h", "up.h" }, set.Files.Select(Path.GetFileName));
		}

		[Fact]
		public void Paths_that_differ_only_in_case_are_one_file()
		{
			this._w.Write(@"headers\Lib.H", "#define LIB 1\r\n");
			this._w.Write(@"headers\other.h", "#include \"LIB.h\"\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"lib.h\"\r\n#include \"other.h\"\r\n");

			DependencySet set = this.Dependencies(script);

			Assert.True(set.IsComplete);
			Assert.Equal(2, set.Files.Count);
		}

		[Fact]
		public void The_headers_folder_is_searched_only_when_the_resolver_has_one()
		{
			this._w.Write(@"headers\lib.h", "#define LIB 1\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"lib.h\"\r\n");

			DependencySet without = this._graph.BeginScan(new IncludeResolver(null, null)).GetDependencies(script);
			DependencySet with = this.Dependencies(script);

			Assert.False(without.IsComplete);
			Assert.Equal("lib.h", Assert.Single(without.Unresolved).Include);
			Assert.True(with.IsComplete);
		}

		[Fact]
		public void Missing_includes_are_reported_with_the_file_and_line_that_asks_for_them()
		{
			this._w.Write(@"headers\a.h", "// nothing\r\n#include \"gone.h\"\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"a.h\"\r\n");

			DependencySet set = this.Dependencies(script);

			UnresolvedInclude missing = Assert.Single(set.Unresolved);
			Assert.Equal("gone.h", missing.Include);
			Assert.Equal(2, missing.Line);
			Assert.Equal(Path.Combine(this._w.Headers, "a.h"), missing.FromFile);
			Assert.False(set.IsComplete);
		}

		[Fact]
		public void An_unreadable_or_missing_script_is_unresolved()
		{
			DependencySet set = this.Dependencies(Path.Combine(this._w.Scripts, "gone.ssl"));

			Assert.False(set.IsComplete);
		}

		[Fact]
		public void Files_are_read_again_only_when_their_stamp_changed()
		{
			// Both texts have the same size (16 bytes).
			this._w.Write(@"headers\lib.h", "//  xxxxxxxxxx\r\n");
			this._w.Write(@"headers\x.h", String.Empty);
			String script = this._w.Write(@"scripts\s.ssl", "#include \"lib.h\"\r\n");
			Assert.Single(this.Dependencies(script).Files);
			Assert.Equal(2, this._graph.CachedFileCount);

			// New content with the same size and time is not noticed (the stamp is the identity), a new time is.
			String header = Path.Combine(this._w.Headers, "lib.h");
			File.WriteAllText(header, "#include \"x.h\"\r\n");
			this._w.Touch(header, BuildWorkspace.Past);
			Assert.Single(this.Dependencies(script).Files);

			this._w.Touch(header, BuildWorkspace.Past.AddHours(1));
			Assert.Equal(2, this.Dependencies(script).Files.Count);
		}

		[Fact]
		public void One_scan_looks_at_the_file_system_once()
		{
			this._w.Write(@"headers\lib.h", "#define LIB 1\r\n");
			String script = this._w.Write(@"scripts\s.ssl", "#include \"lib.h\"\r\n");
			IncludeScan scan = this._graph.BeginScan(this.Resolver);
			Assert.Single(scan.GetDependencies(script).Files);

			// The scan describes one moment: a later change is for the next scan.
			File.Delete(Path.Combine(this._w.Headers, "lib.h"));

			Assert.Single(scan.GetDependencies(script).Files);
			Assert.False(this._graph.BeginScan(this.Resolver).GetDependencies(script).IsComplete);
		}
	}
}