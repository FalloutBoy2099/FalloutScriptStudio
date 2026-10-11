namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Text.Json.Serialization;

	/// <summary>
	/// The procedures of each script that are shown folded in the editor.
	/// </summary>
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public sealed class FoldedProcedures
	{
		/// <summary>Gets the names of the folded procedures by script name.</summary>
		public Dictionary<String, HashSet<String>> Scripts { get; } = new Dictionary<String, HashSet<String>>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Marks a procedure as folded.</summary>
		/// <param name="scriptName">The script name.</param>
		/// <param name="procedure">The procedure name.</param>
		public void SetFolded(String scriptName, String procedure)
		{
			if (!this.Scripts.TryGetValue(scriptName, out HashSet<String>? procedures))
			{
				procedures = new HashSet<String>(StringComparer.Ordinal);
				this.Scripts.Add(scriptName, procedures);
			}

			procedures.Add(procedure);
		}

		/// <summary>Marks a procedure as not folded.</summary>
		/// <param name="scriptName">The script name.</param>
		/// <param name="procedure">The procedure name.</param>
		public void SetUnfolded(String scriptName, String procedure)
		{
			if (this.Scripts.TryGetValue(scriptName, out HashSet<String>? procedures))
			{
				procedures.Remove(procedure);
				if (procedures.Count == 0)
				{
					this.Scripts.Remove(scriptName);
				}
			}
		}

		/// <summary>Determines whether a procedure is folded.</summary>
		/// <param name="scriptName">The script name.</param>
		/// <param name="procedure">The procedure name.</param>
		/// <returns><see langword="true"/> if the procedure is folded.</returns>
		public Boolean IsFolded(String scriptName, String procedure)
		{
			return this.Scripts.TryGetValue(scriptName, out HashSet<String>? procedures) && procedures.Contains(procedure);
		}
	}
}
