namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Text;

	/// <summary>The result of decompiling a compiled script (<c>.int</c>).</summary>
	/// <param name="Text">The decompiled source text.</param>
	/// <param name="Encoding">The encoding of the decompiler output.</param>
	/// <param name="OutputFilePath">The file the decompiler wrote.</param>
	/// <param name="TargetPath">The file the user chose to save the source to, or <see langword="null"/> (temporary decompilation or cancelled dialog).</param>
	public sealed record DecompiledScript(String Text, Encoding Encoding, String OutputFilePath, String? TargetPath);
}