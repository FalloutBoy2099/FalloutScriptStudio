namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Text.Json.Serialization;

	/// <summary>
	/// The last caret line of scripts, keyed by script file name.
	/// </summary>
	[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
	public sealed class ScriptPositions
	{
		/// <summary>Lines below this number are not worth remembering.</summary>
		public const Int32 MinRememberedLine = 10;

		/// <summary>Gets the stored lines by script file name (compared ignoring case).</summary>
		public Dictionary<String, Int32> Lines { get; } = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Remembers the caret line of a script. Lines near the top of the file are ignored.
		/// </summary>
		/// <param name="scriptFileName">The script file name.</param>
		/// <param name="line">The caret line.</param>
		public void Set(String scriptFileName, Int32 line)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptFileName);
			if (line < MinRememberedLine)
			{
				return;
			}

			this.Lines[scriptFileName] = Math.Min(line, UInt16.MaxValue);
		}

		/// <summary>
		/// Gets the remembered caret line of a script.
		/// </summary>
		/// <param name="scriptFileName">The script file name.</param>
		/// <returns>The line, or 0 if none is stored.</returns>
		public Int32 Get(String scriptFileName)
		{
			return this.Lines.TryGetValue(scriptFileName, out Int32 line) ? line : 0;
		}

		/// <summary>Forgets the caret line of a script.</summary>
		/// <param name="scriptFileName">The script file name.</param>
		public void Remove(String scriptFileName)
		{
			this.Lines.Remove(scriptFileName);
		}
	}
}