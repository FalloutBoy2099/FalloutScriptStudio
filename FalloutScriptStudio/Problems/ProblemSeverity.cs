namespace FalloutScriptStudio.Problems
{
	/// <summary>How serious a <see cref="Problem"/> is.</summary>
	public enum ProblemSeverity
	{
		/// <summary>Something failed or cannot work.</summary>
		Error = 0,

		/// <summary>Something works, but probably not as intended.</summary>
		Warning = 1,

		/// <summary>An explanation or a note; it is not counted as a problem in the status bar.</summary>
		Info = 2,
	}
}
