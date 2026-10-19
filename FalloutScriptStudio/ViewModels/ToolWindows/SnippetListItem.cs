namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Editor.Snippets;
	using FalloutScriptStudio.Localization;

	/// <summary>A snippet in the list of "Insert Snippet": what to show for it (the texts follow a language change).</summary>
	public sealed class SnippetListItem
	{
		/// <summary>Initializes a new instance of the <see cref="SnippetListItem"/> class.</summary>
		/// <param name="snippet">The snippet.</param>
		public SnippetListItem(Snippet snippet)
		{
			ArgumentNullException.ThrowIfNull(snippet);
			this.Snippet = snippet;
		}

		/// <summary>Gets the snippet.</summary>
		public Snippet Snippet { get; }

		/// <summary>Gets the trigger word.</summary>
		public String Trigger => this.Snippet.Trigger;

		/// <summary>Gets what the snippet inserts, in the current language.</summary>
		public String Description => SnippetDescriptions.Describe(this.Snippet);

		/// <summary>Gets the name of the source of the snippet in the current language.</summary>
		public String SourceText => GetSourceText(this.Snippet.Source);

		/// <summary>Gets the first lines of the body, as a single line, for the second line of the row.</summary>
		public String Preview => GetPreview(this.Snippet.Body);

		/// <summary>Gets the name a screen reader reads for the row: the trigger, what it inserts and where it comes from.</summary>
		public String AccessibleName => Loc.Format("Str.ToolWindows.Snippets.Item", this.Trigger, this.Description, this.SourceText);

		/// <summary>Gets the name of a source in the current language.</summary>
		/// <param name="source">The source.</param>
		/// <returns>The name.</returns>
		public static String GetSourceText(SnippetSource source)
		{
			return Loc.Get(source switch
			{
				SnippetSource.User => "Str.ToolWindows.Snippets.SourceUser",
				SnippetSource.Project => "Str.ToolWindows.Snippets.SourceProject",
				_ => "Str.ToolWindows.Snippets.SourceBuiltIn",
			});
		}

		private static String GetPreview(String body)
		{
			String flat = body.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
			while (flat.Contains("  ", StringComparison.Ordinal))
			{
				flat = flat.Replace("  ", " ", StringComparison.Ordinal);
			}

			flat = flat.Trim();
			return flat.Length > 120 ? String.Concat(flat.AsSpan(0, 120), "...") : flat;
		}
	}
}