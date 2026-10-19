namespace FalloutScriptStudio.Services.ScriptTools.Procedures
{
	using System;

	/// <summary>
	/// The result of validating the text typed into the procedure name dialog.
	/// </summary>
	/// <param name="IsValid">Whether the text can be accepted.</param>
	/// <param name="Error">The reason the text is rejected; <see langword="null"/> when valid or when nothing was typed yet.</param>
	/// <param name="Name">The plain name (without arguments); empty when it could not be determined.</param>
	/// <param name="Declaration">The normalized declaration text (see <see cref="ProcedureNameResult.Declaration"/>); empty when invalid.</param>
	public sealed record ProcedureNameValidation(Boolean IsValid, String? Error, String Name, String Declaration);
}