namespace FalloutScriptStudio.Workspace
{
	/// <summary>The long operations the user can watch in the status bar and cancel.</summary>
	public enum OperationKind
	{
		/// <summary>Compiling one script.</summary>
		Compile = 0,

		/// <summary>Compiling every open script.</summary>
		CompileAll = 1,

		/// <summary>Preprocessing one script.</summary>
		Preprocess = 2,

		/// <summary>Compiling a script and decompiling the result.</summary>
		RoundTrip = 3,

		/// <summary>Compiling all scripts of a folder in a batch.</summary>
		MassCompile = 4,

		/// <summary>Decompiling a compiled script.</summary>
		Decompile = 5,

		/// <summary>Looking at or formatting the files of a project ("Format Project").</summary>
		FormatProject = 20,
	}
}