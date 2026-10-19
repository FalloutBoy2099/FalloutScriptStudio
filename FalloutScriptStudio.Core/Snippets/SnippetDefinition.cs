namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;

	/// <summary>A snippet as it is stored in a snippet file.</summary>
	public sealed class SnippetDefinition
	{
		/// <summary>Gets or sets the trigger word.</summary>
		public String Trigger { get; set; } = String.Empty;

		/// <summary>Gets or sets the description.</summary>
		public String Description { get; set; } = String.Empty;

		/// <summary>Gets or sets the body (stored as a list of lines).</summary>
		[JsonConverter(typeof(SnippetBodyConverter))]
		public String Body { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the snippet is available; a disabled one hides the snippets of the same trigger of the earlier sources.</summary>
		public Boolean Enabled { get; set; } = true;

		/// <summary>Gets or sets the properties of the entry this version does not know (kept when the file is written again).</summary>
		[JsonExtensionData]
		public Dictionary<String, JsonElement>? UnknownProperties { get; set; }

		/// <summary>Creates a definition from a snippet.</summary>
		/// <param name="snippet">The snippet.</param>
		/// <returns>The definition.</returns>
		public static SnippetDefinition From(Snippet snippet)
		{
			ArgumentNullException.ThrowIfNull(snippet);
			return new SnippetDefinition { Trigger = snippet.Trigger, Description = snippet.Description, Body = snippet.Body, Enabled = snippet.IsEnabled };
		}

		/// <summary>Creates the snippet of this definition.</summary>
		/// <param name="source">Where the file it comes from belongs.</param>
		/// <returns>The snippet.</returns>
		public Snippet ToSnippet(SnippetSource source)
		{
			return new Snippet(this.Trigger.Trim(), this.Description ?? String.Empty, this.Body ?? String.Empty) { IsEnabled = this.Enabled, Source = source };
		}

		/// <summary>Creates a copy.</summary>
		/// <returns>The copy.</returns>
		public SnippetDefinition Clone()
		{
			SnippetDefinition copy = (SnippetDefinition)this.MemberwiseClone();
			copy.UnknownProperties = this.UnknownProperties?.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
			return copy;
		}
	}
}