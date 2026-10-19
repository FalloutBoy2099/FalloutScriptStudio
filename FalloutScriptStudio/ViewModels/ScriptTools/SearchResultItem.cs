namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Search;

	/// <summary>
	/// A row of the result list of the advanced search window.
	/// </summary>
	/// <param name="Hit">The matching line.</param>
	public sealed record SearchResultItem(SearchHit Hit)
	{
		/// <summary>Gets the file name shown in the list.</summary>
		public String FileName => this.Hit.FilePath == BuildError.NoFile ? Loc.Get("Str.ScriptTools.SearchUnsavedScript") : Path.GetFileName(this.Hit.FilePath);

		/// <summary>Gets the full path of the file, shown as a tooltip.</summary>
		public String FullPath => this.Hit.FilePath == BuildError.NoFile ? Loc.Get("Str.ScriptTools.SearchNeverSaved") : this.Hit.FilePath;

		/// <summary>Gets the 1-based line number.</summary>
		public Int32 Line => this.Hit.Line;

		/// <summary>Gets the text of the line.</summary>
		public String Text => this.Hit.Message;
	}
}