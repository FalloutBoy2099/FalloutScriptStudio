namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Replace;

	/// <summary>
	/// A node of the tree of the "Replace in Files" preview: a file, or one replacement in it. A file is ticked when all its replacements are
	/// ticked, unticked when none is, and undetermined otherwise; ticking it ticks every replacement below it.
	/// </summary>
	public sealed partial class ReplaceNodeViewModel : ObservableObject
	{
		private readonly List<ReplaceNodeViewModel> _children = [];
		private Boolean _updating;

		private ReplaceNodeViewModel(ReplaceFilePlan file, ReplaceMatch? match, ReplaceNodeViewModel? parent)
		{
			this.File = file;
			this.Match = match;
			this.Parent = parent;
			this.IsExpanded = match is null;
			this.IsChecked = true;
		}

		/// <summary>Gets the file the node belongs to.</summary>
		public ReplaceFilePlan File { get; }

		/// <summary>Gets the replacement of a replacement node, otherwise <see langword="null"/>.</summary>
		public ReplaceMatch? Match { get; }

		/// <summary>Gets the file node a replacement belongs to, or <see langword="null"/> for a file node.</summary>
		public ReplaceNodeViewModel? Parent { get; }

		/// <summary>Gets a value indicating whether the node is a file.</summary>
		public Boolean IsFile => this.Match is null;

		/// <summary>Gets the nodes below this one.</summary>
		public IReadOnlyList<ReplaceNodeViewModel> Children => this._children;

		/// <summary>Gets the text of the node: the name of the file, or the line number of a replacement.</summary>
		public String Header => this.Match is null
			? (this.File.Path == BuildError.NoFile ? Loc.Get("Str.ScriptTools.SearchUnsavedScript") : System.IO.Path.GetFileName(this.File.Path))
			: Loc.Format("Str.ScriptTools.MacroLine", this.Match.Line);

		/// <summary>Gets the folder of a file node, otherwise an empty text.</summary>
		public String Detail => this.Match is null && this.File.Path != BuildError.NoFile ? System.IO.Path.GetDirectoryName(this.File.Path) ?? String.Empty : String.Empty;

		/// <summary>Gets the full path of the file, shown as a tool tip.</summary>
		public String FullPath => this.File.Path == BuildError.NoFile ? Loc.Get("Str.ScriptTools.SearchNeverSaved") : this.File.Path;

		/// <summary>Gets the number of replacements below a file node, or 1 for a replacement.</summary>
		public Int32 MatchCount => this.Match is null ? this.File.Matches.Count : 1;

		/// <summary>Gets the caption of the number of replacements of a file ("3 replacements").</summary>
		public String CountText => this.Match is not null
			? String.Empty
			: Loc.Format(this.MatchCount == 1 ? "Str.ScriptTools.ReplCountOne" : "Str.ScriptTools.ReplCountMany", this.MatchCount);

		/// <summary>Gets the part of the line before the match (a replacement node).</summary>
		public String Prefix => this.Match?.Prefix ?? String.Empty;

		/// <summary>Gets the matched text, which is replaced (a replacement node).</summary>
		public String Matched => this.Match?.Matched ?? String.Empty;

		/// <summary>Gets the text that replaces it (a replacement node).</summary>
		public String Replacement => this.Match?.Replacement ?? String.Empty;

		/// <summary>Gets the part of the line after the match (a replacement node).</summary>
		public String Suffix => this.Match?.Suffix ?? String.Empty;

		/// <summary>Gets the name a screen reader reads for the node.</summary>
		public String AccessibleName => this.Match is null
			? this.Header + ", " + this.CountText
			: Loc.Format("Str.ScriptTools.ReplMatchName", this.Match.Line, this.Match.Before, this.Match.After);

		/// <summary>Gets or sets whether the node is ticked; <see langword="null"/> means that only some replacements below it are ticked.</summary>
		[ObservableProperty]
		public partial Boolean? IsChecked { get; set; }

		/// <summary>Gets or sets whether the node is expanded in the tree.</summary>
		[ObservableProperty]
		public partial Boolean IsExpanded { get; set; }

		/// <summary>Creates a file node with a replacement node for each of its replacements.</summary>
		/// <param name="file">The file plan.</param>
		/// <returns>The file node.</returns>
		public static ReplaceNodeViewModel CreateFile(ReplaceFilePlan file)
		{
			ArgumentNullException.ThrowIfNull(file);

			ReplaceNodeViewModel node = new(file, null, null);
			foreach (ReplaceMatch match in file.Matches)
			{
				node._children.Add(new ReplaceNodeViewModel(file, match, node));
			}

			return node;
		}

		/// <summary>Gets the replacement nodes below this node, or the node itself when it is one.</summary>
		/// <returns>The replacement nodes in file order.</returns>
		public IEnumerable<ReplaceNodeViewModel> EnumerateMatches()
		{
			if (this.Match is not null)
			{
				yield return this;
				yield break;
			}

			foreach (ReplaceNodeViewModel child in this._children)
			{
				yield return child;
			}
		}

		/// <summary>Raises the change notifications of the texts that depend on the language.</summary>
		public void RefreshTexts()
		{
			this.OnPropertyChanged(nameof(this.Header));
			this.OnPropertyChanged(nameof(this.CountText));
			this.OnPropertyChanged(nameof(this.FullPath));
			this.OnPropertyChanged(nameof(this.AccessibleName));
			foreach (ReplaceNodeViewModel child in this._children)
			{
				child.RefreshTexts();
			}
		}

		partial void OnIsCheckedChanged(Boolean? value)
		{
			if (this._updating)
			{
				return;
			}

			if (value.HasValue)
			{
				foreach (ReplaceNodeViewModel child in this._children)
				{
					child._updating = true;
					try
					{
						child.IsChecked = value.Value;
					}
					finally
					{
						child._updating = false;
					}
				}
			}

			this.Parent?.RefreshFromChildren();
		}

		private void RefreshFromChildren()
		{
			Boolean any = false;
			Boolean all = true;
			foreach (ReplaceNodeViewModel child in this._children)
			{
				any |= child.IsChecked != false;
				all &= child.IsChecked == true;
			}

			this._updating = true;
			try
			{
				this.IsChecked = all ? true : any ? null : false;
			}
			finally
			{
				this._updating = false;
			}
		}
	}
}