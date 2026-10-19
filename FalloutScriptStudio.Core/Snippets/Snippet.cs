namespace FalloutScriptStudio.Core.Snippets
{
	using System;

	/// <summary>
	/// A text snippet: a word that is typed (the trigger) and the body that replaces it. The body uses the syntax described in
	/// <see cref="SnippetTemplate"/>.
	/// </summary>
	/// <param name="Trigger">The word that expands to the snippet when Tab is pressed after it, and that the completion list offers.</param>
	/// <param name="Description">What the snippet inserts, in a few words.</param>
	/// <param name="Body">The body.</param>
	public sealed record Snippet(String Trigger, String Description, String Body)
	{
		/// <summary>Gets the localization key of the description of a built-in snippet (the application shows its text instead of <see cref="Description"/>), or <see langword="null"/>.</summary>
		public String? DescriptionKey { get; init; }

		/// <summary>Gets a value indicating whether the snippet is available; a disabled snippet hides the snippets of the same trigger of the earlier sources.</summary>
		public Boolean IsEnabled { get; init; } = true;

		/// <summary>Gets where the snippet comes from.</summary>
		public SnippetSource Source { get; init; }

		/// <summary>Gets a value indicating whether the body uses the selected text, so that the snippet can surround code.</summary>
		public Boolean SurroundsSelection => SnippetTemplate.Parse(this.Body).UsesSelection;
	}
}