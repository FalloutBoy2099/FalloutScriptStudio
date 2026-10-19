namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// The menu of standard procedure handlers read from <c>descriptions\prochandlers.ini</c> (legacy <c>HandlerProcedure</c>).
	/// </summary>
	/// <remarks>
	/// File format (one entry per line, <c>//</c> lines and blank lines are ignored):
	/// <c>name&lt;d&gt;description</c> is a handler; <c>&lt;m&gt;title[&lt;d&gt;description]</c> opens a group that holds the handlers
	/// up to the next <c>&lt;m&gt;</c> or <c>&lt;-m&gt;</c>; <c>&lt;-m&gt;</c> closes the open group; <c>&lt;-&gt;</c> is a separator.
	/// A group that is not closed at the end of the file is kept.
	/// </remarks>
	public sealed class ProcedureHandlerCatalog
	{
		private static readonly HashSet<String> _overrideHandlers = new(StringComparer.Ordinal)
		{
			"look_at_p_proc",
			"description_p_proc",
		};

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcedureHandlerCatalog"/> class.
		/// </summary>
		/// <param name="entries">The top level entries of the menu.</param>
		public ProcedureHandlerCatalog(IReadOnlyList<ProcedureHandlerEntry> entries)
		{
			ArgumentNullException.ThrowIfNull(entries);
			this.Entries = entries;
		}

		/// <summary>Gets the top level entries of the menu in file order.</summary>
		public IReadOnlyList<ProcedureHandlerEntry> Entries { get; }

		/// <summary>Gets every handler of the menu, groups flattened, in file order.</summary>
		public IEnumerable<ProcedureHandlerEntry> Handlers => Flatten(this.Entries);

		/// <summary>
		/// Determines whether a created handler gets a <c>script_overrides;</c> statement (legacy <c>CreateProcBlock</c>).
		/// </summary>
		/// <param name="procedureName">The procedure name.</param>
		/// <returns><c>true</c> for <c>look_at_p_proc</c> and <c>description_p_proc</c>.</returns>
		public static Boolean RequiresScriptOverrides(String procedureName)
		{
			ArgumentNullException.ThrowIfNull(procedureName);
			return _overrideHandlers.Contains(procedureName);
		}

		/// <summary>Parses the lines of <c>prochandlers.ini</c>.</summary>
		/// <param name="lines">The lines of the file.</param>
		/// <returns>The catalog.</returns>
		public static ProcedureHandlerCatalog Parse(IEnumerable<String> lines)
		{
			ArgumentNullException.ThrowIfNull(lines);

			var top = new List<ProcedureHandlerEntry>();
			List<ProcedureHandlerEntry>? group = null;
			String groupName = String.Empty;
			String groupDescription = String.Empty;

			void CloseGroup()
			{
				if (group is not null)
				{
					top.Add(new ProcedureHandlerEntry(ProcedureHandlerKind.Group, groupName, groupDescription, group));
					group = null;
				}
			}

			foreach (String rawLine in lines)
			{
				String line = rawLine.Trim();
				if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
				{
					continue;
				}

				if (line.StartsWith("<m>", StringComparison.Ordinal))
				{
					CloseGroup();
					group = [];
					Int32 d = line.IndexOf("<d>", StringComparison.Ordinal);
					groupName = d == -1 ? line.Substring(3) : line.Substring(3, d - 3);
					groupDescription = d == -1 ? String.Empty : line.Substring(d + 3);
					continue;
				}

				if (line == "<-m>")
				{
					CloseGroup();
					continue;
				}

				if (line == "<->")
				{
					top.Add(ProcedureHandlerEntry.CreateSeparator());
					continue;
				}

				Int32 n = line.IndexOf("<d>", StringComparison.Ordinal);
				if (n > 0)
				{
					ProcedureHandlerEntry entry = ProcedureHandlerEntry.CreateHandler(line.Substring(0, n), line.Substring(n + 3));
					if (group is not null)
					{
						group.Add(entry);
					}
					else
					{
						top.Add(entry);
					}
				}
			}

			CloseGroup();
			return new ProcedureHandlerCatalog(top);
		}

		/// <summary>Reads <c>prochandlers.ini</c>.</summary>
		/// <param name="path">The file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The catalog; an empty one when the file does not exist.</returns>
		public static async Task<ProcedureHandlerCatalog> LoadAsync(String path, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			if (!File.Exists(path))
			{
				return new ProcedureHandlerCatalog(Array.Empty<ProcedureHandlerEntry>());
			}

			String[] lines = await File.ReadAllLinesAsync(path, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
			return Parse(lines);
		}

		/// <summary>Finds the description of a handler.</summary>
		/// <param name="procedureName">The procedure name (case-sensitive, without arguments).</param>
		/// <returns>The description, or <c>null</c> when the name is not in the menu.</returns>
		public String? FindDescription(String procedureName)
		{
			ArgumentNullException.ThrowIfNull(procedureName);
			return this.Handlers.FirstOrDefault(h => String.Equals(h.Name, procedureName, StringComparison.Ordinal))?.Description;
		}

		private static IEnumerable<ProcedureHandlerEntry> Flatten(IEnumerable<ProcedureHandlerEntry> entries)
		{
			foreach (ProcedureHandlerEntry entry in entries)
			{
				switch (entry.Kind)
				{
					case ProcedureHandlerKind.Handler:
						yield return entry;
						break;
					case ProcedureHandlerKind.Group:
						foreach (ProcedureHandlerEntry child in Flatten(entry.Children))
						{
							yield return child;
						}

						break;
				}
			}
		}
	}
}