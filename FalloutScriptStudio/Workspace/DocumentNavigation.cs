namespace FalloutScriptStudio.Workspace
{
	using FalloutScriptStudio.Documents;

	/// <summary>Moves the caret of a document to the position requested by <see cref="OpenDocumentOptions"/>.</summary>
	internal static class DocumentNavigation
	{
		/// <summary>
		/// Goes to the requested line right away when the editor is visible, otherwise stores the request in the view state
		/// so that the editor executes it when it is first shown. Does nothing when no line was requested.
		/// </summary>
		public static void Navigate(DocumentViewModel document, OpenDocumentOptions options)
		{
			if (options.Line <= 0)
			{
				return;
			}

			if (document.Editor is not null && document.ViewState.HasBeenShown)
			{
				document.Editor.GoTo(options.Line, options.Column, options.SelectLength);
			}
			else
			{
				document.ViewState.RequestGoTo(options.Line, options.Column, options.SelectLength);
			}
		}
	}
}
