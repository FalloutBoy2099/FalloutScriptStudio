namespace FalloutScriptStudio.Documents
{
	/// <summary>The kind of file shown in an editor tab.</summary>
	public enum DocumentKind
	{
		/// <summary>An SSL script (<c>.ssl</c>); parsed, folded, compiled.</summary>
		Script,

		/// <summary>An SSL header (<c>.h</c>); highlighted and parsed for macros like a script but never compiled.</summary>
		Header,

		/// <summary>A Fallout message file (<c>.msg</c>) edited as text.</summary>
		Message,

		/// <summary>Any other text file (<c>.ini</c>, <c>.txt</c>, <c>.cfg</c>, <c>.xshd</c>…).</summary>
		Other,
	}
}