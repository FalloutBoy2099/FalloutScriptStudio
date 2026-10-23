namespace FalloutScriptStudio.Core.Globals
{
	/// <summary>How serious a finding about a global variable is.</summary>
	public enum GlobalFindingSeverity
	{
		/// <summary>Worth knowing, often intended.</summary>
		Info = 0,

		/// <summary>Probably a mistake.</summary>
		Warning = 1,
	}
}