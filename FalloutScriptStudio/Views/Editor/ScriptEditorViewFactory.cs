namespace FalloutScriptStudio.Views.Editor
{
	using Documents;
	using FalloutScriptStudio.Editor;

	/// <summary>
	/// Creates <see cref="ScriptEditorView"/> instances with their dependencies. Register it with
	/// <c>services.AddScriptEditor()</c> and call <see cref="Create"/> from the tab content template code
	/// (a XAML <c>DataTemplate</c> cannot pass constructor arguments).
	/// </summary>
	public sealed class ScriptEditorViewFactory
	{
		private readonly ScriptEditorServices _services;

		public ScriptEditorViewFactory(ScriptEditorServices services)
		{
			this._services = services;
		}

		/// <summary>Creates an editor view for a document.</summary>
		/// <param name="document">The document; it becomes the <c>DataContext</c> of the view.</param>
		/// <returns>The view; the editor starts when it is loaded into a visual tree.</returns>
		public ScriptEditorView Create(DocumentViewModel document)
		{
			return new ScriptEditorView(this._services) { DataContext = document };
		}
	}
}