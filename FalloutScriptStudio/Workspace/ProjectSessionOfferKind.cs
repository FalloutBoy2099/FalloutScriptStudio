namespace FalloutScriptStudio.Workspace
{
	/// <summary>What happened when a project was opened and its remembered documents were considered.</summary>
	public enum ProjectSessionOfferKind
	{
		/// <summary>Nothing: the option is off, or no document is remembered.</summary>
		None,

		/// <summary>The remembered documents were opened (nothing else was open).</summary>
		Restored,

		/// <summary>Other documents are open: a hint with a "Restore open files" link was reported.</summary>
		Offered,
	}
}
