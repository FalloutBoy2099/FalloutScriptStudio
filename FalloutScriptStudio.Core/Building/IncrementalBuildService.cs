namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using Compilation;
	using Parsing;

	/// <summary>
	/// <see cref="IIncrementalBuildService"/> on top of the include graph and the build cache file. The cache of a file is loaded once and
	/// loaded again when another program changed the file, or when it could not be read before.
	/// </summary>
	public sealed class IncrementalBuildService : IIncrementalBuildService
	{
		private readonly IncludeGraph _graph = new IncludeGraph();
		private readonly ConcurrentDictionary<String, BuildCache> _caches = new ConcurrentDictionary<String, BuildCache>(StringComparer.OrdinalIgnoreCase);
		private readonly Object _loadSync = new Object();
		private readonly ILogger? _logger;

		/// <summary>Initializes a new instance of the <see cref="IncrementalBuildService"/> class.</summary>
		/// <param name="logger">The logger, or <see langword="null"/>.</param>
		public IncrementalBuildService(ILogger<IncrementalBuildService>? logger = null)
		{
			this._logger = logger;
		}

		/// <summary>Gets the include graph (its cache of the directives of the files).</summary>
		public IncludeGraph Graph => this._graph;

		/// <inheritdoc/>
		public BuildPlan Plan(IReadOnlyList<BuildCandidate> candidates, CompilerOptions options, String cacheFile, Boolean force = false, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(candidates);
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrEmpty(cacheFile);

			BuildCache cache = this.GetCache(cacheFile);
			BuildFingerprint fingerprint = BuildFingerprint.Create(options);
			IncludeScan scan = this._graph.BeginScan(CreateResolver(options));
			CompilerPaths paths = new CompilerPaths(options);
			BuildDecision[] decisions = new BuildDecision[candidates.Count];

			ParallelOptions parallel = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount) };
			Parallel.For(0, decisions.Length, parallel, index =>
			{
				decisions[index] = Evaluate(candidates[index], options, cache, fingerprint, scan, paths, force);
			});

			foreach (BuildDecision decision in decisions)
			{
				if (decision.Reason == StaleReason.SourceMissing && decision.OutputPath is not null)
				{
					cache.Remove(decision.ScriptPath, decision.OutputPath);
				}
			}

			BuildPlan plan = new BuildPlan(decisions, cache.Status);
			this._logger?.Planned(decisions.Length, plan.Stale.Count, plan.UpToDate.Count);
			return plan;
		}

		/// <inheritdoc/>
		public BuildTicket Begin(String scriptPath, CompilerOptions options, Boolean shortCircuit, String cacheFile)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(options);
			ArgumentException.ThrowIfNullOrEmpty(cacheFile);

			BuildCache cache = this.GetCache(cacheFile);
			BuildFingerprint fingerprint = BuildFingerprint.Create(options);
			String script = Path.GetFullPath(scriptPath);
			Boolean effectiveShortCircuit = options.ShortCircuit || shortCircuit;
			String? output = TryGetOutput(new CompilerPaths(options), script);
			if (output is null || options.UseUserCommand)
			{
				return new BuildTicket(cache, script, options.UseUserCommand ? null : output, fingerprint, effectiveShortCircuit, FileStamp.Missing, null);
			}

			IncludeScan scan = this._graph.BeginScan(CreateResolver(options));
			FileStamp source = scan.GetStamp(script);
			DependencySet dependencies = scan.GetDependencies(script);
			return new BuildTicket(cache, script, output, fingerprint, effectiveShortCircuit, source, dependencies.IsComplete ? Merge(dependencies, scan) : null);
		}

		/// <inheritdoc/>
		public Boolean Flush(String cacheFile)
		{
			ArgumentException.ThrowIfNullOrEmpty(cacheFile);
			return this._caches.TryGetValue(Path.GetFullPath(cacheFile), out BuildCache? cache) ? cache.Save() : true;
		}

		/// <summary>Gets the cache of a file: the one in memory, unless another program changed the file or it could not be read.</summary>
		/// <param name="cacheFile">The cache file.</param>
		/// <returns>The cache.</returns>
		public BuildCache GetCache(String cacheFile)
		{
			ArgumentException.ThrowIfNullOrEmpty(cacheFile);

			String key = Path.GetFullPath(cacheFile);
			lock (this._loadSync)
			{
				if (this._caches.TryGetValue(key, out BuildCache? existing))
				{
					Boolean outdated = !existing.IsDirty && (existing.Status == BuildCacheStatus.Unreadable || FileStamp.Read(key) != existing.LoadedStamp);
					if (!outdated)
					{
						return existing;
					}
				}

				BuildCache loaded = BuildCache.Load(key, this._logger);
				this._caches[key] = loaded;
				return loaded;
			}
		}

		private static IncludeResolver CreateResolver(CompilerOptions options)
		{
			return new IncludeResolver(options.SearchesIncludes ? options.HeadersDirectory : null, options.ProgramDirectory);
		}

		private static String? TryGetOutput(CompilerPaths paths, String script)
		{
			try
			{
				return paths.GetOutputPath(script);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		/// <summary>The files a script depends on with their stamps, in the alphabetical order of their paths (an include that does not exist is part of it, with a missing stamp).</summary>
		private static List<DependencyStamp> Merge(DependencySet dependencies, IncludeScan scan)
		{
			List<DependencyStamp> merged = new(dependencies.Files.Count + dependencies.OptionalMissing.Count);
			Int32 existing = 0;
			Int32 missing = 0;
			while (existing < dependencies.Files.Count || missing < dependencies.OptionalMissing.Count)
			{
				Boolean takeExisting = missing >= dependencies.OptionalMissing.Count
					|| (existing < dependencies.Files.Count && StringComparer.OrdinalIgnoreCase.Compare(dependencies.Files[existing], dependencies.OptionalMissing[missing]) <= 0);
				if (takeExisting)
				{
					String file = dependencies.Files[existing++];
					merged.Add(new DependencyStamp(file, scan.GetStamp(file)));
				}
				else
				{
					merged.Add(new DependencyStamp(dependencies.OptionalMissing[missing++], FileStamp.Missing));
				}
			}

			return merged;
		}

		private static BuildDecision Evaluate(BuildCandidate candidate, CompilerOptions options, BuildCache cache, BuildFingerprint fingerprint, IncludeScan scan, CompilerPaths paths, Boolean force)
		{
			String script;
			try
			{
				script = Path.GetFullPath(candidate.ScriptPath);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return new BuildDecision(candidate.ScriptPath, null, StaleReason.SourceMissing);
			}

			String? output = TryGetOutput(paths, script);
			if (force)
			{
				return new BuildDecision(script, output, StaleReason.Forced);
			}

			if (options.UseUserCommand)
			{
				return new BuildDecision(script, null, StaleReason.NotCacheable);
			}

			if (output is null)
			{
				return new BuildDecision(script, null, StaleReason.OutputFolderNotSet);
			}

			FileStamp source = scan.GetStamp(script);
			if (!source.Exists)
			{
				return new BuildDecision(script, output, StaleReason.SourceMissing);
			}

			FileStamp compiled = scan.GetStamp(output);
			if (!compiled.Exists)
			{
				return new BuildDecision(script, output, StaleReason.OutputMissing);
			}

			if (!cache.TryGet(script, output, out BuildRecord? record) || record is null)
			{
				return new BuildDecision(script, output, StaleReason.NotRecorded);
			}

			if (!String.Equals(record.Fingerprint.Options, fingerprint.Options, StringComparison.Ordinal) || record.ShortCircuit != (options.ShortCircuit || candidate.ShortCircuit))
			{
				return new BuildDecision(script, output, StaleReason.OptionsChanged);
			}

			if (!String.Equals(record.Fingerprint.Tools, fingerprint.Tools, StringComparison.Ordinal))
			{
				return new BuildDecision(script, output, StaleReason.CompilerChanged);
			}

			if (record.OutputStamp != compiled)
			{
				return new BuildDecision(script, output, StaleReason.OutputChanged);
			}

			if (record.Source != source)
			{
				return new BuildDecision(script, output, StaleReason.SourceChanged);
			}

			DependencySet dependencies = scan.GetDependencies(script);
			if (!dependencies.IsComplete)
			{
				UnresolvedInclude first = dependencies.Unresolved[0];
				return new BuildDecision(script, output, StaleReason.UnresolvedInclude, first.Include ?? first.FromFile);
			}

			List<DependencyStamp> current = Merge(dependencies, scan);
			if (current.Count != record.Dependencies.Count)
			{
				return new BuildDecision(script, output, StaleReason.DependenciesChanged);
			}

			for (Int32 index = 0; index < current.Count; index++)
			{
				if (!String.Equals(current[index].Path, record.Dependencies[index].Path, StringComparison.OrdinalIgnoreCase))
				{
					return new BuildDecision(script, output, StaleReason.DependenciesChanged, current[index].Path);
				}
			}

			for (Int32 index = 0; index < current.Count; index++)
			{
				if (current[index].Stamp != record.Dependencies[index].Stamp)
				{
					return new BuildDecision(script, output, StaleReason.DependencyChanged, current[index].Path);
				}
			}

			return new BuildDecision(script, output, StaleReason.UpToDate);
		}
	}
}