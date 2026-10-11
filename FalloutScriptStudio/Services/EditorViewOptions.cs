namespace FalloutScriptStudio.Services
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>
	/// View toggles of the editors that are not part of the persisted <c>AppSettings</c> (line numbers, parse error underlines).
	/// The main window changes them; every editor view may bind to or observe this singleton.
	/// </summary>
	public sealed partial class EditorViewOptions : ObservableObject
	{
		/// <summary>True when the line number margin is shown.</summary>
		[ObservableProperty]
		public partial Boolean ShowLineNumbers { get; set; } = true;

		/// <summary>True when parser errors are underlined in the text.</summary>
		[ObservableProperty]
		public partial Boolean HighlightParseErrors { get; set; } = true;
	}
}
