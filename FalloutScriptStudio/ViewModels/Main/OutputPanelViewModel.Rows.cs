namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;

	/// <summary>The rows of the Errors tab: collection from the active script and the problems, filtering by severity and source, grouping.</summary>
	public sealed partial class OutputPanelViewModel
	{
		partial void OnShowParserEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowBuildEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowToolEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowFileEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowSettingsEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowMessageEntriesChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowErrorsChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowWarningsChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		partial void OnShowInfosChanged(Boolean value)
		{
			this.RebuildErrors();
		}

		private static String GroupName(ProblemSource source)
		{
			return Loc.Get(source switch
			{
				ProblemSource.Parse => "Str.Shell.Output.GroupParser",
				ProblemSource.Build => "Str.Shell.Output.GroupBuild",
				ProblemSource.Tool => "Str.Shell.Output.GroupTools",
				ProblemSource.File => "Str.Shell.Output.GroupFiles",
				ProblemSource.Settings => "Str.Shell.Output.GroupSettings",
				_ => "Str.Shell.Output.GroupMessages",
			});
		}

		private void RebuildErrors()
		{
			ErrorItemViewModel? previous = this.SelectedError;
			List<ErrorItemViewModel> all = this.CollectRows();
			this.ProblemErrorCount = all.Count(item => item.Severity == ProblemSeverity.Error);
			this.ProblemWarningCount = all.Count(item => item.Severity == ProblemSeverity.Warning);
			this.ProblemInfoCount = all.Count(item => item.Severity == ProblemSeverity.Info);

			this.Errors.Clear();
			ErrorItemViewModel? restored = null;
			foreach (ErrorItemViewModel item in all.Where(this.PassesFilters))
			{
				this.Errors.Add(item);
				if (previous is not null && restored is null && IsSameEntry(previous, item))
				{
					restored = item;
				}
			}

			this.SelectedError = restored;
			this.OnPropertyChanged(nameof(this.ErrorCount));
		}

		private static Boolean IsSameEntry(ErrorItemViewModel left, ErrorItemViewModel right)
		{
			return left.Source == right.Source
				&& left.Line == right.Line
				&& String.Equals(left.Message, right.Message, StringComparison.Ordinal)
				&& String.Equals(left.FullPath, right.FullPath, StringComparison.OrdinalIgnoreCase);
		}

		private Boolean PassesFilters(ErrorItemViewModel item)
		{
			Boolean severity = item.Severity switch
			{
				ProblemSeverity.Error => this.ShowErrors,
				ProblemSeverity.Warning => this.ShowWarnings,
				_ => this.ShowInfos,
			};
			Boolean source = item.Source switch
			{
				ProblemSource.Parse => this.ShowParserEntries,
				ProblemSource.Build => this.ShowBuildEntries,
				ProblemSource.Tool => this.ShowToolEntries,
				ProblemSource.File => this.ShowFileEntries,
				ProblemSource.Settings => this.ShowSettingsEntries,
				_ => this.ShowMessageEntries,
			};
			return severity && source;
		}

		private List<ErrorItemViewModel> CollectRows()
		{
			List<ErrorItemViewModel> rows = new List<ErrorItemViewModel>();
			String parserGroup = GroupName(ProblemSource.Parse);
			String buildGroup = GroupName(ProblemSource.Build);
			if (this._aggregatedErrors is not null)
			{
				foreach (BuildError error in this._aggregatedErrors)
				{
					rows.Add(new ErrorItemViewModel(error, buildGroup, ProblemSource.Build));
				}
			}
			else if (this._document is { } document && !this._hideDocumentRows)
			{
				if (this._settings.Settings.Editor.EnableParser)
				{
					foreach (BuildError error in document.ParserErrors)
					{
						rows.Add(new ErrorItemViewModel(error, parserGroup, ProblemSource.Parse));
					}
				}

				foreach (BuildErrorType type in new[] { BuildErrorType.Error, BuildErrorType.Warning, BuildErrorType.Message })
				{
					foreach (BuildError error in document.BuildErrors.Where(e => e.Type == type))
					{
						rows.Add(new ErrorItemViewModel(error, buildGroup, ProblemSource.Build));
					}
				}
			}

			// The rows of the same group stay together (the grid groups by name); inside a source the worst come first, in the order reported.
			IEnumerable<Problem> reported = this._problems.Problems
				.Select((problem, index) => (problem, index))
				.OrderBy(entry => entry.problem.Source)
				.ThenBy(entry => entry.problem.Severity)
				.ThenBy(entry => entry.index)
				.Select(entry => entry.problem);
			foreach (Problem problem in reported)
			{
				rows.Add(new ErrorItemViewModel(problem, GroupName(problem.Source)));
			}

			// Parser and build entries first (as they always were), then the other sources in the order of the enumeration.
			return rows
				.Select((row, index) => (row, index))
				.OrderBy(entry => entry.row.Source)
				.ThenBy(entry => entry.index)
				.Select(entry => entry.row)
				.ToList();
		}
	}
}
