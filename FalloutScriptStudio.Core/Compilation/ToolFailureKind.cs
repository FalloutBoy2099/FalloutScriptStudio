namespace FalloutScriptStudio.Core.Compilation
{
	/// <summary>
	/// Why an external tool could not be used.
	/// </summary>
	public enum ToolFailureKind
	{
		/// <summary>The program file does not exist.</summary>
		NotFound,

		/// <summary>The program exists but the operating system could not start it.</summary>
		StartFailed,
	}
}