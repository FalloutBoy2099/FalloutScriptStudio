namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// A node of the macro rename preview tree: a group of files, a file, or one match. A group or a file is ticked when all its
	/// matches are ticked, unticked when none is, and undetermined otherwise; ticking it ticks every match below it.
	/// </summary>
	public sealed partial class MacroRenameNodeViewModel : ObservableObject
	{
		private readonly List<MacroRenameNodeViewModel> _children = [];
		private Boolean _updating;

		private MacroRenameNodeViewModel(MacroRenameNodeKind kind, String header, String detail, MacroRenameNodeViewModel? parent)
		{
			this.Kind = kind;
			this.Header = header;
			this.Detail = detail;
			this.Parent = parent;
			this.IsExpanded = kind != MacroRenameNodeKind.Match;
			this.IsChecked = true;
		}

		/// <summary>Gets the level of the node.</summary>
		public MacroRenameNodeKind Kind { get; }

		/// <summary>Gets the text of the node: the name of the group, the file name, or the line caption of a match.</summary>
		public String Header { get; }

		/// <summary>Gets the secondary text: the folder of a file, otherwise an empty text.</summary>
		public String Detail { get; }

		/// <summary>Gets the full path of the file for a file or a match node, otherwise an empty text.</summary>
		public String FilePath { get; private init; } = String.Empty;

		/// <summary>Gets the parent node, or <see langword="null"/> for a group.</summary>
		public MacroRenameNodeViewModel? Parent { get; }

		/// <summary>Gets the nodes below this one.</summary>
		public IReadOnlyList<MacroRenameNodeViewModel> Children => this._children;

		/// <summary>Gets the match of a match node, otherwise <see langword="null"/>.</summary>
		public PreviewMatch? Match { get; private init; }

		/// <summary>Gets the position of the match in the preview it comes from (it keeps the original order).</summary>
		public Int32 Index { get; private init; }

		/// <summary>Gets the part of the line that is the macro name (a match node).</summary>
		public String MatchText { get; private init; } = String.Empty;

		/// <summary>Gets the rest of the line after the macro name (a match node).</summary>
		public String RestText { get; private init; } = String.Empty;

		/// <summary>Gets the number of matches below this node, or 1 for a match.</summary>
		public Int32 MatchCount { get; private set; }

		/// <summary>Gets the caption of the number of matches ("3 matches").</summary>
		public String CountText => this.Kind == MacroRenameNodeKind.Match
			? String.Empty
			: Loc.Format(this.MatchCount == 1 ? "Str.ScriptTools.MacroCountOne" : "Str.ScriptTools.MacroCountMany", this.MatchCount);

		/// <summary>Gets or sets whether the node is ticked; <see langword="null"/> means that only some matches below it are ticked.</summary>
		[ObservableProperty]
		public partial Boolean? IsChecked { get; set; }

		/// <summary>Gets or sets whether the node is expanded in the tree.</summary>
		[ObservableProperty]
		public partial Boolean IsExpanded { get; set; }

		/// <summary>Creates a group node.</summary>
		/// <param name="title">The name of the group.</param>
		/// <returns>The node.</returns>
		public static MacroRenameNodeViewModel CreateSection(String title)
		{
			ArgumentNullException.ThrowIfNull(title);

			return new MacroRenameNodeViewModel(MacroRenameNodeKind.Section, title, String.Empty, null);
		}

		/// <summary>Adds a file node below a group.</summary>
		/// <param name="section">The group.</param>
		/// <param name="filePath">The full path of the file.</param>
		/// <returns>The file node.</returns>
		public static MacroRenameNodeViewModel AddFile(MacroRenameNodeViewModel section, String filePath)
		{
			ArgumentNullException.ThrowIfNull(section);
			ArgumentNullException.ThrowIfNull(filePath);

			String name = Path.GetFileName(filePath);
			String folder = Path.GetDirectoryName(filePath) ?? String.Empty;
			MacroRenameNodeViewModel file = new(MacroRenameNodeKind.File, name.Length > 0 ? name : filePath, folder, section) { FilePath = filePath };
			section._children.Add(file);
			return file;
		}

		/// <summary>Adds a match node below a file.</summary>
		/// <param name="file">The file node.</param>
		/// <param name="match">The match.</param>
		/// <param name="index">The position of the match in the preview.</param>
		/// <returns>The match node.</returns>
		public static MacroRenameNodeViewModel AddMatch(MacroRenameNodeViewModel file, PreviewMatch match, Int32 index)
		{
			ArgumentNullException.ThrowIfNull(file);
			ArgumentNullException.ThrowIfNull(match);

			Int32 length = Math.Clamp(match.Span.Length, 0, match.PreviewText.Length);
			MacroRenameNodeViewModel node = new(
				MacroRenameNodeKind.Match,
				Loc.Format("Str.ScriptTools.MacroLine", match.Line),
				String.Empty,
				file)
			{
				FilePath = match.FilePath,
				Match = match,
				Index = index,
				MatchText = match.PreviewText.Substring(0, length),
				RestText = match.PreviewText.Substring(length),
				MatchCount = 1,
			};
			file._children.Add(node);
			for (MacroRenameNodeViewModel? ancestor = file; ancestor is not null; ancestor = ancestor.Parent)
			{
				ancestor.MatchCount++;
			}

			return node;
		}

		/// <summary>Gets the match nodes below this node, or the node itself when it is a match.</summary>
		/// <returns>The match nodes in tree order.</returns>
		public IEnumerable<MacroRenameNodeViewModel> EnumerateMatches()
		{
			if (this.Kind == MacroRenameNodeKind.Match)
			{
				yield return this;
				yield break;
			}

			foreach (MacroRenameNodeViewModel child in this._children)
			{
				foreach (MacroRenameNodeViewModel match in child.EnumerateMatches())
				{
					yield return match;
				}
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
				this.SetChildren(value.Value);
			}

			this.Parent?.RefreshFromChildren();
		}

		private void SetChildren(Boolean value)
		{
			foreach (MacroRenameNodeViewModel child in this._children)
			{
				child._updating = true;
				try
				{
					child.IsChecked = value;
				}
				finally
				{
					child._updating = false;
				}

				child.SetChildren(value);
			}
		}

		private void RefreshFromChildren()
		{
			Boolean any = false;
			Boolean all = true;
			foreach (MacroRenameNodeViewModel child in this._children)
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

			this.Parent?.RefreshFromChildren();
		}
	}
}