namespace FalloutScriptStudio.Core.Snippets
{
	/// <summary>Where a snippet comes from. A snippet of a later source replaces the one of an earlier source that has the same trigger.</summary>
	public enum SnippetSource
	{
		/// <summary>Shipped with the editor.</summary>
		BuiltIn = 0,

		/// <summary>The user's own snippets (<c>snippets.json</c> in the settings folder).</summary>
		User = 1,

		/// <summary>The snippets of the project (<c>fss-snippets.json</c> in the project folder).</summary>
		Project = 2,
	}
}