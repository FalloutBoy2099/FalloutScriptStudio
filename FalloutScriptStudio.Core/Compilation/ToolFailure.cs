namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.IO;

	/// <summary>
	/// An external tool (compiler, preprocessor, batch file, decompiler) that could not be run. It is reported next to the text of the
	/// build log so that the application can explain the failure in its own language and offer a fix.
	/// </summary>
	/// <param name="Kind">What went wrong.</param>
	/// <param name="ToolPath">The full path of the program.</param>
	/// <param name="Detail">The reason given by the operating system for <see cref="ToolFailureKind.StartFailed"/>, otherwise <see langword="null"/>.</param>
	public sealed record ToolFailure(ToolFailureKind Kind, String ToolPath, String? Detail = null)
	{
		/// <summary>Gets the message the build log contains for this failure (the text of the matching log entry).</summary>
		public String LogMessage => this.Kind == ToolFailureKind.NotFound
			? "Required tool not found: " + this.ToolPath
			: "Failed to start " + this.ToolPath + ": " + this.Detail;

		/// <summary>Gets the name of the program file without its folder.</summary>
		public String ToolName => Path.GetFileName(this.ToolPath);
	}
}
