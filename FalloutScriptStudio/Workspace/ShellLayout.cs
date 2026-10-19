namespace FalloutScriptStudio.Workspace
{
	using System;
	using Services;

	/// <summary>View state of the main window that is not part of the core settings model.</summary>
	public sealed class ShellLayout
	{
		/// <summary>Gets or sets a value indicating whether the right browser (procedures, variables…) is shown.</summary>
		public Boolean ShowRightBrowser { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the function tree is shown on the left.</summary>
		public Boolean ShowFunctionTree { get; set; }

		/// <summary>Gets or sets the width of the left function tree in device independent pixels.</summary>
		public Double FunctionTreeWidth { get; set; } = 240;

		/// <summary>Gets or sets a value indicating whether the line numbers are shown.</summary>
		public Boolean ShowLineNumbers { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether parser errors are underlined.</summary>
		public Boolean HighlightParseErrors { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the output panel shows only its tab headers.</summary>
		public Boolean LogMinimized { get; set; }

		/// <summary>Gets or sets the index of the selected tab of the right browser.</summary>
		public Int32 RightTabIndex { get; set; }

		/// <summary>Gets or sets the color mode of the application.</summary>
		public AppTheme Theme { get; set; } = AppTheme.System;

		/// <summary>Gets or sets the code of the interface language; <see langword="null"/> follows the language of Windows.</summary>
		public String? Language { get; set; }
	}
}