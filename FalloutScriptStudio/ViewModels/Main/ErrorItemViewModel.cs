namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Globalization;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;

	/// <summary>One row of the Errors list: a parser entry, a build entry or a search result.</summary>
	public sealed class ErrorItemViewModel
	{
		/// <summary>Initializes a row.</summary>
		/// <param name="error">The entry.</param>
		/// <param name="group">The group header ("Parser", "Build", "Search").</param>
		public ErrorItemViewModel(BuildError error, String group)
		{
			ArgumentNullException.ThrowIfNull(error);
			this.Error = error;
			this.Group = group;
		}

		/// <summary>Gets the underlying entry.</summary>
		public BuildError Error { get; }

		/// <summary>Gets the group header.</summary>
		public String Group { get; }

		/// <summary>Gets the kind of the entry.</summary>
		public BuildErrorType Type => this.Error.Type;

		/// <summary>Gets the kind as text.</summary>
		public String TypeText => this.Error.Type switch
		{
			BuildErrorType.None => String.Empty,
			BuildErrorType.Error => Loc.Get("Str.Common.Error"),
			BuildErrorType.Warning => Loc.Get("Str.Common.Warning"),
			BuildErrorType.Message => Loc.Get("Str.Common.Message"),
			BuildErrorType.Search => Loc.Get("Str.Common.Search"),
			BuildErrorType.Parser => Loc.Get("Str.Shell.Output.GroupParser"),
			_ => this.Error.Type.ToString(),
		};

		/// <summary>Gets the message.</summary>
		public String Message => this.Error.Message;

		/// <summary>Gets the file name without directory (empty when the entry has no file).</summary>
		public String FileName => this.HasFile ? Path.GetFileName(this.Error.FileName) : String.Empty;

		/// <summary>Gets the full path as reported (may be relative or empty).</summary>
		public String FullPath => this.HasFile ? this.Error.FileName : String.Empty;

		/// <summary>Gets the 1-based line, or -1.</summary>
		public Int32 Line => this.Error.Line;

		/// <summary>Gets the line as text (empty when unknown).</summary>
		public String LineText => this.Error.Line > 0 ? this.Error.Line.ToString(CultureInfo.InvariantCulture) : String.Empty;

		/// <summary>Gets a value indicating whether the entry points to a place in a file.</summary>
		public Boolean CanNavigate => this.Error.Line > 0;

		private Boolean HasFile => !String.IsNullOrEmpty(this.Error.FileName)
			&& !String.Equals(this.Error.FileName, BuildError.NoFile, StringComparison.OrdinalIgnoreCase);
	}
}
