namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Folders and files the user configured.
	/// </summary>
	public sealed class PathSettings
	{
		/// <summary>Gets or sets the folder for compiled scripts (the game scripts folder), or <see langword="null"/> if not set.</summary>
		public String? OutputDirectory { get; set; }

		/// <summary>Gets or sets the path of the <c>scripts.h</c> header file, or <see langword="null"/> if not set.</summary>
		public String? ScriptsHeaderFile { get; set; }

		/// <summary>Gets or sets the folder with the header files, or <see langword="null"/> if not set.</summary>
		public String? HeadersDirectory { get; set; }

		/// <summary>Gets or sets the folder last used for a mass compilation.</summary>
		public String? LastMassCompileDirectory { get; set; }

		/// <summary>Gets or sets the folder last used for a search in files.</summary>
		public String? LastSearchDirectory { get; set; }

		/// <summary>Gets or sets the path of the external text editor, or <see langword="null"/> if not chosen yet.</summary>
		public String? ExternalEditorPath { get; set; }

		/// <summary>Gets or sets the project (solution) folder.</summary>
		public String? SolutionFolder { get; set; }

		/// <summary>Gets or sets the additional folders searched for message files.</summary>
		public List<String> MessageSearchDirectories { get; set; } = new List<String>();

		/// <summary>Creates a copy that can be changed without touching this instance.</summary>
		/// <returns>The copy.</returns>
		internal PathSettings Copy()
		{
			PathSettings copy = (PathSettings)this.MemberwiseClone();
			copy.MessageSearchDirectories = new List<String>(this.MessageSearchDirectories ?? new List<String>());
			return copy;
		}
	}
}
