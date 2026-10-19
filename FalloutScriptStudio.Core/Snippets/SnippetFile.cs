namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>The content of a snippet file: <c>snippets.json</c> (the user's snippets) or <c>fss-snippets.json</c> (the snippets of a project).</summary>
	public sealed class SnippetFile
	{
		/// <summary>The version of the file layout this program writes.</summary>
		public const Int32 CurrentVersion = 1;

		/// <summary>The name of the file of the snippets of a project, found directly in the project folder.</summary>
		public const String ProjectFileName = "fss-snippets.json";

		/// <summary>The name of the file of the user's snippets, in the settings folder.</summary>
		public const String UserFileName = "snippets.json";

		/// <summary>Gets or sets the version of the layout this file was written with.</summary>
		public Int32 Version { get; set; } = CurrentVersion;

		/// <summary>Gets or sets the snippets.</summary>
		public List<SnippetDefinition> Snippets { get; set; } = [];

		/// <summary>Gets or sets the properties of the file this version does not know (kept when the file is written again).</summary>
		[JsonExtensionData]
		public Dictionary<String, JsonElement>? UnknownProperties { get; set; }

		/// <summary>Creates a copy.</summary>
		/// <returns>The copy.</returns>
		public SnippetFile Clone()
		{
			return new SnippetFile
			{
				Version = this.Version,
				Snippets = this.Snippets.Select(snippet => snippet.Clone()).ToList(),
				UnknownProperties = this.UnknownProperties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal),
			};
		}
	}
}