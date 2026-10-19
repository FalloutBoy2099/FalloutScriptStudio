namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.IO;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>
	/// A script that failed to compile in a batch run, with the output of the compiler.
	/// </summary>
	public sealed partial class BatchCompileFailureItem : ObservableObject
	{
		[ObservableProperty]
		private String _output = String.Empty;

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchCompileFailureItem"/> class.
		/// </summary>
		/// <param name="scriptPath">The script that failed.</param>
		/// <param name="output">The compiler output, or an empty string while it is not known yet.</param>
		public BatchCompileFailureItem(String scriptPath, String output)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			this.ScriptPath = scriptPath;
			this._output = output ?? String.Empty;
		}

		/// <summary>Gets the full path of the script.</summary>
		public String ScriptPath { get; }

		/// <summary>Gets the file name of the script.</summary>
		public String FileName => Path.GetFileName(this.ScriptPath);
	}
}