namespace FalloutScriptStudio.Core.Compilation
{
	/// <summary>
	/// Selects the program that turns a compiled <c>.int</c> script back into <c>.ssl</c> source.
	/// </summary>
	public enum DecompilerKind
	{
		/// <summary>The decompiler built into the editor (pure C#, nothing else to install).</summary>
		BuiltIn = 0,

		/// <summary>An external <c>int2ssl.exe</c> chosen by the user.</summary>
		ExternalInt2Ssl = 1,
	}
}