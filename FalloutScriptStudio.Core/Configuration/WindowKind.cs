namespace FalloutScriptStudio.Core.Configuration
{
	/// <summary>
	/// The windows whose placement is remembered.
	/// </summary>
	public enum WindowKind
	{
		/// <summary>The main editor window.</summary>
		Main = 0,

		/// <summary>The headers (include files) window.</summary>
		Headers,

		/// <summary>The Message Editor window.</summary>
		MessageEditor,

		/// <summary>The flowchart (dialog node diagram) window.</summary>
		Flowchart,

		/// <summary>The Search &amp; Replace window.</summary>
		Search,
	}
}