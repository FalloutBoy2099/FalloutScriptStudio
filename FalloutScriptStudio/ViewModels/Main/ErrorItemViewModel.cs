namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Globalization;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;

	/// <summary>One row of the Errors list: a parser entry, a build entry, a problem of a tool or a file, or a search result.</summary>
	public sealed class ErrorItemViewModel
	{
		/// <summary>Initializes a row for a parser, build or search entry.</summary>
		/// <param name="error">The entry.</param>
		/// <param name="group">The group header ("Parser", "Build", "Search").</param>
		/// <param name="source">Where the entry comes from.</param>
		public ErrorItemViewModel(BuildError error, String group, ProblemSource source = ProblemSource.Build)
		{
			ArgumentNullException.ThrowIfNull(error);
			this.Error = error;
			this.Group = group;
			this.Source = source;
			this.Severity = error.Type switch
			{
				BuildErrorType.Error => ProblemSeverity.Error,
				BuildErrorType.Warning => ProblemSeverity.Warning,
				_ => ProblemSeverity.Info,
			};
		}

		/// <summary>Initializes a row for a problem (the text is taken in the current language).</summary>
		/// <param name="problem">The problem.</param>
		/// <param name="group">The group header.</param>
		public ErrorItemViewModel(Problem problem, String group)
		{
			ArgumentNullException.ThrowIfNull(problem);
			this.Problem = problem;
			this.Error = problem.ToBuildError();
			this.Group = group;
			this.Source = problem.Source;
			this.Severity = problem.Severity;
			this.Hint = problem.Hint;
		}

		/// <summary>Gets the underlying entry.</summary>
		public BuildError Error { get; }

		/// <summary>Gets the problem the row was made from, or <see langword="null"/> for a parser, build or search entry.</summary>
		public Problem? Problem { get; }

		/// <summary>Gets the group header.</summary>
		public String Group { get; }

		/// <summary>Gets where the entry comes from.</summary>
		public ProblemSource Source { get; }

		/// <summary>Gets the severity.</summary>
		public ProblemSeverity Severity { get; }

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

		/// <summary>Gets what to do about it ("what to do"), or <see langword="null"/>.</summary>
		public String? Hint { get; }

		/// <summary>Gets a value indicating whether the row has a fix hint.</summary>
		public Boolean HasHint => !String.IsNullOrEmpty(this.Hint);

		/// <summary>Gets the obvious fix, or <see langword="null"/>.</summary>
		public ProblemAction? Action => this.Problem?.Action;

		/// <summary>Gets a value indicating whether the row offers a fix.</summary>
		public Boolean HasAction => this.Action is not null;

		/// <summary>Gets the text of the link that runs the fix, in the current language.</summary>
		public String ActionLabel => this.Action?.Label ?? String.Empty;

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

		/// <summary>Gets the text copied by "copy selected": the message, followed by the hint when there is one.</summary>
		public String ClipboardText => this.HasHint ? this.Message + "\r\n" + this.Hint : this.Message;

		/// <summary>Gets the line copied by "copy all": <c>file(line,column): kind: message</c>, followed by the hint when there is one.</summary>
		public String ReportLine
		{
			get
			{
				StringBuilder line = new();
				if (this.HasFile)
				{
					line.Append(this.Error.FileName);
					if (this.Error.Line > 0)
					{
						line.Append('(').Append(this.Error.Line.ToString(CultureInfo.InvariantCulture));
						if (this.Error.Column > 0)
						{
							line.Append(',').Append(this.Error.Column.ToString(CultureInfo.InvariantCulture));
						}

						line.Append(')');
					}

					line.Append(": ");
				}

				String type = this.TypeText;
				if (type.Length > 0)
				{
					line.Append(type).Append(": ");
				}

				line.Append(this.Message);
				if (this.HasHint)
				{
					line.Append("\r\n    ").Append(this.Hint);
				}

				return line.ToString();
			}
		}

		/// <summary>Returns the text a screen reader announces for the row: place, kind and message on one line.</summary>
		/// <returns>The text of the row.</returns>
		public override String ToString()
		{
			return this.ReportLine.Replace("\r\n    ", " ", StringComparison.Ordinal);
		}

		private Boolean HasFile => !String.IsNullOrEmpty(this.Error.FileName)
			&& !String.Equals(this.Error.FileName, BuildError.NoFile, StringComparison.OrdinalIgnoreCase);
	}
}