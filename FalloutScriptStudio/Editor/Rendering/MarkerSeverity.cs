namespace FalloutScriptStudio.Editor.Rendering
{
	/// <summary>How serious a diagnostic mark in the editor is; it selects the color of the underline.</summary>
	public enum MarkerSeverity
	{
		/// <summary>An error (red wave).</summary>
		Error,

		/// <summary>A warning (orange wave).</summary>
		Warning,

		/// <summary>A remark such as a possible typo (green wave).</summary>
		Hint,

		/// <summary>The line of an <c>#include</c> whose header contains an error (blue wave in the light theme, lime in the dark one).</summary>
		IncludeError,

		/// <summary>A local variable that is declared but never used (a faint dotted line under its name).</summary>
		Unused,
	}
}