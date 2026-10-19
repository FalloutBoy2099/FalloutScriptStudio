namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using Parsing;

	/// <summary>
	/// One run of dependency computations (see <see cref="IncludeGraph.BeginScan"/>). The time stamps of the files and the resolution of
	/// the include paths are memorised, so the headers that many scripts share are examined once. Thread safe.
	/// </summary>
	public sealed class IncludeScan
	{
		private readonly IncludeGraph _graph;
		private readonly IncludeResolver _resolver;
		private readonly ConcurrentDictionary<String, FileStamp> _stamps = new ConcurrentDictionary<String, FileStamp>(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<String, String> _resolutions = new ConcurrentDictionary<String, String>(StringComparer.OrdinalIgnoreCase);

		internal IncludeScan(IncludeGraph graph, IncludeResolver resolver)
		{
			this._graph = graph;
			this._resolver = resolver;
		}

		/// <summary>Gets the stamp of a file as it was when the scan first looked at it.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <returns>The stamp.</returns>
		public FileStamp GetStamp(String path)
		{
			ArgumentNullException.ThrowIfNull(path);
			return this._stamps.GetOrAdd(path, static file => FileStamp.Read(file));
		}

		/// <summary>Computes the files a script depends on, directly or through other files. Cycles are followed once.</summary>
		/// <param name="scriptPath">The script.</param>
		/// <returns>The dependencies.</returns>
		public DependencySet GetDependencies(String scriptPath)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);

			String script = Path.GetFullPath(scriptPath);
			HashSet<String> visited = new(StringComparer.OrdinalIgnoreCase) { script };
			List<String> files = [];
			List<String> optionalMissing = [];
			List<UnresolvedInclude> unresolved = [];
			Stack<String> pending = new Stack<String>();
			pending.Push(script);

			while (pending.Count > 0)
			{
				String file = pending.Pop();
				IReadOnlyList<IncludeDirective>? directives = this._graph.GetDirectives(file, this.GetStamp(file));
				if (directives is null)
				{
					unresolved.Add(new UnresolvedInclude(file, 0, null));
					continue;
				}

				String directory = Path.GetDirectoryName(file) ?? String.Empty;
				foreach (IncludeDirective directive in directives)
				{
					if (!directive.IsLiteral)
					{
						unresolved.Add(new UnresolvedInclude(file, directive.Line, null));
						continue;
					}

					String resolved = this.Resolve(directive.Path!, directory);
					if (this.GetStamp(resolved).Exists)
					{
						if (visited.Add(resolved))
						{
							files.Add(resolved);
							pending.Push(resolved);
						}
					}
					else if (directive.Conditional)
					{
						if (!optionalMissing.Exists(item => String.Equals(item, resolved, StringComparison.OrdinalIgnoreCase)))
						{
							optionalMissing.Add(resolved);
						}
					}
					else
					{
						unresolved.Add(new UnresolvedInclude(file, directive.Line, directive.Path));
					}
				}
			}

			files.Sort(StringComparer.OrdinalIgnoreCase);
			optionalMissing.Sort(StringComparer.OrdinalIgnoreCase);
			return new DependencySet(files, optionalMissing, unresolved);
		}

		private String Resolve(String includePath, String directory)
		{
			return this._resolutions.GetOrAdd(directory + "|" + includePath, _ => this._resolver.Resolve(includePath, directory));
		}
	}
}