namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Thread-safe in-memory <see cref="ICollapsedProcedureStore"/> with import/export so the application can persist it.
	/// </summary>
	public sealed class InMemoryCollapsedProcedureStore : ICollapsedProcedureStore
	{
		private readonly Dictionary<String, HashSet<String>> _scripts = new(StringComparer.OrdinalIgnoreCase);
		private readonly Object _gate = new Object();

		/// <inheritdoc />
		public Boolean IsCollapsed(String scriptName, String procedureName)
		{
			lock (this._gate)
			{
				return this._scripts.TryGetValue(scriptName, out HashSet<String>? procedures) && procedures.Contains(procedureName.Trim());
			}
		}

		/// <inheritdoc />
		public void SetCollapsed(String scriptName, String procedureName, Boolean collapsed)
		{
			lock (this._gate)
			{
				if (collapsed)
				{
					if (!this._scripts.TryGetValue(scriptName, out HashSet<String>? procedures))
					{
						procedures = new(StringComparer.OrdinalIgnoreCase);
						this._scripts.Add(scriptName, procedures);
					}

					procedures.Add(procedureName.Trim());
				}
				else if (this._scripts.TryGetValue(scriptName, out HashSet<String>? existing))
				{
					existing.Remove(procedureName.Trim());
				}
			}
		}

		/// <summary>Exports the remembered state.</summary>
		/// <returns>For every script with at least one collapsed procedure, the names of those procedures.</returns>
		public IReadOnlyDictionary<String, IReadOnlyList<String>> Export()
		{
			lock (this._gate)
			{
				return this._scripts
					.Where(s => s.Value.Count > 0)
					.ToDictionary(s => s.Key, s => (IReadOnlyList<String>)s.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
			}
		}

		/// <summary>Replaces the remembered state.</summary>
		/// <param name="state">The state, as produced by <see cref="Export"/>.</param>
		public void Import(IEnumerable<KeyValuePair<String, IReadOnlyList<String>>> state)
		{
			lock (this._gate)
			{
				this._scripts.Clear();
				foreach (KeyValuePair<String, IReadOnlyList<String>> script in state)
				{
					this._scripts[script.Key] = new HashSet<String>(script.Value.Select(p => p.Trim()), StringComparer.OrdinalIgnoreCase);
				}
			}
		}
	}
}