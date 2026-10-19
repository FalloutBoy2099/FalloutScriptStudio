namespace FalloutScriptStudio.Services.ScriptTools
{
	/// <summary>How the procedure name dialog is used.</summary>
	public enum ProcedureNameDialogMode
	{
		/// <summary>Create a new procedure; the name may carry an argument list.</summary>
		Create,

		/// <summary>Create a procedure whose name comes from the word under the caret.</summary>
		CreateFromWord,

		/// <summary>Rename an existing procedure (or another symbol); only a plain identifier is accepted.</summary>
		Rename,

		/// <summary>Name the procedure that gets the statements extracted from another one; its parameters are fixed by the variables they use and are listed in the subject of the request.</summary>
		Extract,
	}
}