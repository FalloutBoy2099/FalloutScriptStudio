namespace FalloutScriptStudio.Core.Indexing
{
	/// <summary>The step the project index is in.</summary>
	public enum ProjectIndexPhase
	{
		/// <summary>Nothing runs.</summary>
		Idle = 0,

		/// <summary>The folders are being listed.</summary>
		Scanning = 1,

		/// <summary>The files are being read and indexed.</summary>
		Indexing = 2,
	}
}
