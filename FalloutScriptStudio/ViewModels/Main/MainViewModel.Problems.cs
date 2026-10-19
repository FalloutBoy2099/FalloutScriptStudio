namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.ComponentModel;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;

	/// <summary>The problem list and the long operation (progress and Cancel) in the status bar.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Gets a value indicating whether a long operation (a build, a mass compilation, a decompilation) is running.</summary>
		public Boolean IsOperationRunning => this._build.Operations.IsRunning;

		/// <summary>Gets what the running operation does, in the current language ("Compiling a.ssl (3/12)"), or an empty text.</summary>
		public String OperationText
		{
			get
			{
				OperationInfo? info = this._build.Operations.Current;
				if (info is null)
				{
					return String.Empty;
				}

				String text = DescribeOperation(info);
				return info.IsCancelling ? Loc.Format("Str.Shell.Operation.Cancelling", text) : text;
			}
		}

		/// <summary>Gets the progress of the running operation from 0 to 1.</summary>
		public Double OperationFraction => Math.Max(0, this._build.Operations.Current?.Fraction ?? 0);

		/// <summary>Gets a value indicating whether the running operation does not know how far it is.</summary>
		public Boolean IsOperationIndeterminate => (this._build.Operations.Current?.Fraction ?? -1) < 0;

		/// <summary>Gets the tool tip of the error and warning counters of the status bar.</summary>
		public String ProblemsTip => Loc.Format("Str.Shell.Problems.StatusTip", this.Output.ProblemErrorCount, this.Output.ProblemWarningCount);

		/// <summary>Brings the list of problems to the front.</summary>
		[RelayCommand]
		public void ShowProblems()
		{
			this.Output.ShowTab(OutputTab.Errors);
		}

		/// <summary>Cancels the running operation: the tool processes are killed and the files they were writing are cleaned up.</summary>
		[RelayCommand(CanExecute = nameof(CanCancelOperation))]
		public void CancelOperation()
		{
			_ = this._build.Cancel();
		}

		private Boolean CanBuild()
		{
			return !this._build.IsBusy;
		}

		private Boolean CanCancelOperation()
		{
			return this._build.Operations.Current is { IsCancelling: false };
		}

		private static String DescribeOperation(OperationInfo info)
		{
			Boolean counted = info.Total > 0 && info.Index > 0;
			return info.Kind switch
			{
				OperationKind.Compile => Loc.Format("Str.Shell.Operation.Compile", info.Subject),
				OperationKind.CompileAll => info.UpToDate > 0
					? Loc.Format("Str.Shell.Operation.CompileCountedUpToDate", info.Subject.Length > 0 ? info.Subject : "...", info.Index, info.Total, info.UpToDate)
					: Loc.Format("Str.Shell.Operation.CompileCounted", info.Subject.Length > 0 ? info.Subject : "...", info.Index, info.Total),
				OperationKind.Preprocess => Loc.Format("Str.Shell.Operation.Preprocess", info.Subject),
				OperationKind.RoundTrip => Loc.Format("Str.Shell.Operation.RoundTrip", info.Subject),
				OperationKind.MassCompile => counted
					? Loc.Format(info.UpToDate > 0 ? "Str.Shell.Operation.MassCompileCountedUpToDate" : "Str.Shell.Operation.MassCompileCounted", info.Subject, info.Index, info.Total, info.UpToDate)
					: Loc.Get("Str.Shell.Operation.MassCompile"),
				OperationKind.FormatProject => counted
					? Loc.Format("Str.Shell.Operation.FormatCounted", info.Subject, info.Index, info.Total)
					: Loc.Get("Str.Shell.Operation.Format"),
				_ => Loc.Format("Str.Shell.Operation.Decompile", info.Subject),
			};
		}

		/// <summary>Connects the status bar to the operation tracker and the problem counters, and lets problem actions open the settings.</summary>
		private void InitializeProblems()
		{
			this.Output.OpenSettingsHandler = this.OpenSettingsPageAsync;
			this.Output.SwitchProjectHandler = this.SwitchProjectAsync;
			this.Output.RestoreDocumentsHandler = this.RestoreProjectDocumentsAsync;
			this._build.Operations.Changed += (_, _) => this.RaiseOperationChanged();
			this.Output.PropertyChanged += this.OnOutputProblemsChanged;
			_ = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.RaiseProblemTexts());
		}

		private async Task OpenSettingsPageAsync(SettingsPage page)
		{
			await this._toolWindows.ShowSettingsAsync(page);
			await this.AfterSettingsChangedAsync();
		}

		private void OnOutputProblemsChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is nameof(OutputPanelViewModel.ProblemErrorCount) or nameof(OutputPanelViewModel.ProblemWarningCount))
			{
				this.OnPropertyChanged(nameof(this.ProblemsTip));
			}
		}

		private void RaiseOperationChanged()
		{
			this.OnPropertyChanged(nameof(this.IsBuildRunning));
			this.OnPropertyChanged(nameof(this.IsOperationRunning));
			this.OnPropertyChanged(nameof(this.OperationText));
			this.OnPropertyChanged(nameof(this.OperationFraction));
			this.OnPropertyChanged(nameof(this.IsOperationIndeterminate));
			this.CompileCommand.NotifyCanExecuteChanged();
			this.CompileAllOpenCommand.NotifyCanExecuteChanged();
			this.MassCompileCommand.NotifyCanExecuteChanged();
			this.BuildChangedCommand.NotifyCanExecuteChanged();
			this.RebuildAllCommand.NotifyCanExecuteChanged();
			this.PreprocessCommand.NotifyCanExecuteChanged();
			this.RoundTripCommand.NotifyCanExecuteChanged();
			this.CancelOperationCommand.NotifyCanExecuteChanged();
		}

		private void RaiseProblemTexts()
		{
			this.OnPropertyChanged(nameof(this.OperationText));
			this.OnPropertyChanged(nameof(this.ProblemsTip));
		}
	}
}