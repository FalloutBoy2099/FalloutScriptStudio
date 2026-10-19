namespace FalloutScriptStudio.Core.Tests.Building
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Tests.Compilation;

	/// <summary>
	/// A scratch installation (<c>resources</c>, <c>scrTemp</c>, <c>scripts</c>, <c>headers</c>) for the tests of the incremental build, with
	/// stand-ins for the compiler and a helper that "compiles" a script the way <see cref="BatchCompileService"/> does (ticket, output file, outcome).
	/// </summary>
	internal sealed class BuildWorkspace : IDisposable
	{
		/// <summary>An old, fixed time for the sources.</summary>
		public static readonly DateTime Past = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

		private readonly TempWorkspace _workspace = new TempWorkspace();

		public BuildWorkspace()
		{
			Directory.CreateDirectory(this.Headers);
			this._workspace.CreateTools("compile.exe", "mcpp.bat", "mcpp.exe");
			this.Service = new IncrementalBuildService();
		}

		public IncrementalBuildService Service { get; private set; }

		public String Root => this._workspace.Root;

		public String Scripts => this._workspace.Scripts;

		public String Headers => Path.Combine(this._workspace.Root, "headers");

		public String Resources => this._workspace.Resources;

		public String CacheFile => Path.Combine(this._workspace.Root, "fss-build.json");

		/// <summary>The options: the built-in preprocessor, includes searched in the headers folder, output next to the sources.</summary>
		public CompilerOptions Options => this._workspace.CreateOptions() with
		{
			Preprocessor = PreprocessorKind.Builtin,
			SearchIncludePath = true,
			ScriptsHeaderFile = Path.Combine(this.Headers, "scripts.h"),
			HeadersDirectory = this.Headers,
		};

		/// <summary>Writes a file below the root with a fixed old modification time and returns its full path.</summary>
		public String Write(String relativePath, String content, DateTime? modified = null)
		{
			String path = this._workspace.CreateFile(relativePath, content);
			File.SetLastWriteTimeUtc(path, modified ?? Past);
			return path;
		}

		/// <summary>Changes the modification time of a file.</summary>
		public void Touch(String path, DateTime modified)
		{
			File.SetLastWriteTimeUtc(path, modified);
		}

		/// <summary>Replaces the service by a new one that knows nothing of this process (as after a restart of the program).</summary>
		public void Restart()
		{
			this.Service = new IncrementalBuildService();
		}

		/// <summary>The compiled file of a script.</summary>
		public String Output(String script, CompilerOptions? options = null)
		{
			return new CompilerPaths(options ?? this.Options).GetOutputPath(script);
		}

		/// <summary>"Compiles" a script: ticket, output file, success.</summary>
		public BuildTicket Build(String script, CompilerOptions? options = null, Boolean shortCircuit = false)
		{
			CompilerOptions used = options ?? this.Options;
			BuildTicket ticket = this.Service.Begin(script, used, shortCircuit, this.CacheFile);
			File.WriteAllText(this.Output(script, used), "int " + Guid.NewGuid().ToString("N"));
			ticket.Succeeded();
			return ticket;
		}

		public BuildDecision Check(String script, CompilerOptions? options = null, Boolean force = false)
		{
			BuildPlan plan = this.Service.Plan(new[] { new BuildCandidate(script) }, options ?? this.Options, this.CacheFile, force);
			return Assert_Single(plan.Decisions);
		}

		public BuildPlan Plan(IReadOnlyList<String> scripts, CompilerOptions? options = null)
		{
			List<BuildCandidate> candidates = [];
			foreach (String script in scripts)
			{
				candidates.Add(new BuildCandidate(script));
			}

			return this.Service.Plan(candidates, options ?? this.Options, this.CacheFile);
		}

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private static BuildDecision Assert_Single(IReadOnlyList<BuildDecision> decisions)
		{
			if (decisions.Count != 1)
			{
				throw new InvalidOperationException("Expected one decision, got " + decisions.Count);
			}

			return decisions[0];
		}
	}
}