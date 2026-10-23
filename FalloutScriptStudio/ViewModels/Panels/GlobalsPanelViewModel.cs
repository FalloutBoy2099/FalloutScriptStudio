namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Workspace;

	/// <summary>
	/// The "Globals" tab of the right panel: the global variables of the whole project (sfall globals, game, map and local variables) grouped
	/// by family, with every place that reads or writes them, a badge for the variables that have findings, a filter, navigation, the export
	/// to CSV and the check that lists the findings in the problems. It follows the project index (after a short pause).
	/// </summary>
	public sealed partial class GlobalsPanelViewModel : ObservableObject, IDisposable
	{
		private readonly GlobalVariablesService _service;
		private readonly IWorkspace _workspace;
		private readonly IDialogService _dialogs;
		private readonly IShellService _shell;
		private readonly HashSet<String> _expanded = new(StringComparer.Ordinal);
		private Int32 _generation;
		private Int32 _variableCount;
		private Int32 _useCount;
		private Int32 _fileCount;
		private Boolean _truncated;
		private Boolean _disposed;

		/// <summary>Initializes the panel.</summary>
		/// <param name="service">The global variables of the project.</param>
		/// <param name="workspace">Navigates to the uses and lists them.</param>
		/// <param name="dialogs">Asks where the CSV file goes.</param>
		/// <param name="shell">The clipboard.</param>
		public GlobalsPanelViewModel(GlobalVariablesService service, IWorkspace workspace, IDialogService dialogs, IShellService shell)
		{
			ArgumentNullException.ThrowIfNull(service);
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(shell);
			this._service = service;
			this._workspace = workspace;
			this._dialogs = dialogs;
			this._shell = shell;
			this._service.Changed += this.OnServiceChanged;
			_ = LanguageChangedSubscription.Subscribe(this, static panel => panel.OnLanguageChanged());
			this.Rebuild(this._service.Current, Array.Empty<GlobalFinding>());
		}

		/// <summary>Gets the groups of the tree (one per family of variables that has some).</summary>
		public ObservableCollection<TreeGroupNodeViewModel> Groups { get; } = [];

		/// <summary>True when the project uses global variables.</summary>
		[ObservableProperty]
		public partial Boolean HasData { get; set; }

		/// <summary>The text the tree is filtered with: a name, a macro, a number or a file name.</summary>
		[ObservableProperty]
		public partial String FilterText { get; set; } = String.Empty;

		/// <summary>Gets the number of findings of the last analysis.</summary>
		[ObservableProperty]
		public partial Int32 FindingCount { get; set; }

		/// <summary>Gets the counts under the filter box, in the current language ("12 variables, 30 uses in 5 files").</summary>
		public String SummaryText
		{
			get
			{
				String text = Loc.Format("Str.Panels.GlobalsSummary", this._variableCount, this._useCount, this._fileCount);
				return this._truncated ? text + " " + Loc.Get("Str.Panels.GlobalsTruncated") : text;
			}
		}

		partial void OnFilterTextChanged(String value)
		{
			this.ApplyFilter();
		}

		/// <summary>Analyzes the variables of the index again and rebuilds the tree.</summary>
		/// <returns>A task that completes when the tree shows the new variables.</returns>
		public async Task RefreshAsync()
		{
			Int32 generation = Interlocked.Increment(ref this._generation);
			GlobalVariableIndex variables = this._service.Current;
			IReadOnlyList<GlobalFinding> findings = await this._service.AnalyzeAsync().ConfigureAwait(true);
			if (generation == Volatile.Read(ref this._generation) && !this._disposed)
			{
				this.Rebuild(variables, findings);
			}
		}

		/// <summary>Rebuilds the tree from some variables and findings (the expanded nodes stay expanded).</summary>
		/// <param name="variables">The variables.</param>
		/// <param name="findings">The findings to show as badges.</param>
		public void Rebuild(GlobalVariableIndex variables, IReadOnlyList<GlobalFinding> findings)
		{
			ArgumentNullException.ThrowIfNull(variables);
			ArgumentNullException.ThrowIfNull(findings);

			foreach (TreeGroupNodeViewModel group in this.Groups)
			{
				this.Remember(group);
				foreach (TreeNodeViewModel child in group.Children)
				{
					this.Remember(child);
				}
			}

			this._variableCount = variables.Variables.Count;
			this._useCount = variables.UseCount;
			this._fileCount = variables.FileCount;
			this._truncated = variables.IsTruncated;
			this.FindingCount = findings.Count;

			Dictionary<String, List<GlobalFinding>> byPlace = new(StringComparer.OrdinalIgnoreCase);
			foreach (GlobalFinding finding in findings)
			{
				String place = finding.File + "|" + finding.Line.ToString(CultureInfo.InvariantCulture);
				if (!byPlace.TryGetValue(place, out List<GlobalFinding>? list))
				{
					list = [];
					byPlace.Add(place, list);
				}

				list.Add(finding);
			}

			List<TreeGroupNodeViewModel> groups = [];
			foreach (IGrouping<GlobalVariableKind, GlobalVariable> family in variables.Variables.GroupBy(static v => v.Kind))
			{
				GlobalVariable[] members = family.ToArray();
				TreeGroupNodeViewModel group = new(
					Loc.Format(GetGroupKey(family.Key), members.Length),
					"globals:" + family.Key,
					false)
				{
					ToolTip = Loc.Get(GetGroupKey(family.Key) + "Tip"),
				};
				Int32 index = 0;
				foreach (GlobalVariable variable in members)
				{
					GlobalVariableNodeViewModel node = new(variable, group.Key + "/" + (index++).ToString(CultureInfo.InvariantCulture) + "/" + variable.Key);
					node.ToolTip = variable.DisplayName;
					if (byPlace.Count > 0)
					{
						node.SetFindings(FindingsOf(variable, byPlace));
					}

					node.IsExpanded = this._expanded.Contains(node.Key);
					group.Children.Add(node);
				}

				group.IsExpanded = !this._expanded.Contains("collapsed:" + group.Key);
				groups.Add(group);
			}

			this.Groups.Clear();
			foreach (TreeGroupNodeViewModel group in groups)
			{
				this.Groups.Add(group);
			}

			this.HasData = groups.Count > 0;
			this.OnPropertyChanged(nameof(this.SummaryText));
			this.ApplyFilter();
		}

		/// <summary>Goes to the first use of a variable, or to the place of a use.</summary>
		/// <param name="node">A variable or a use.</param>
		/// <returns>A task that completes when the editor shows the place.</returns>
		[RelayCommand]
		public async Task NavigateAsync(TreeNodeViewModel? node)
		{
			GlobalVariableUse? use = node switch
			{
				GlobalUseNodeViewModel { Use: { } u } => u,
				GlobalVariableNodeViewModel variable => variable.Variable.Uses.FirstOrDefault(),
				_ => null,
			};
			if (use is not null)
			{
				await this._workspace.GoToAsync(use.File, use.Line);
			}
		}

		/// <summary>Lists every use of a variable in the Errors tab.</summary>
		/// <param name="node">A variable or one of its uses.</param>
		/// <returns>A task that completes when the list is shown.</returns>
		[RelayCommand]
		public async Task FindAllAsync(TreeNodeViewModel? node)
		{
			if (GetVariable(node) is { } variable)
			{
				await this._workspace.ShowReferencesAsync(
					variable.DisplayName,
					variable.Uses.Select(static u => new SourceReference(u.File, u.Line)).ToArray());
			}
		}

		/// <summary>Puts the name of a variable on the clipboard.</summary>
		/// <param name="node">A variable or one of its uses.</param>
		[RelayCommand]
		public void CopyName(TreeNodeViewModel? node)
		{
			if (GetVariable(node) is { } variable)
			{
				this._shell.SetClipboardText(GetEntry(variable));
			}
		}

		/// <summary>Leaves a variable out of the findings from now on (it is added to the ignore list of the settings).</summary>
		/// <param name="node">A variable or one of its uses.</param>
		/// <returns>A task that completes when the findings are computed again.</returns>
		[RelayCommand]
		public async Task IgnoreAsync(TreeNodeViewModel? node)
		{
			if (GetVariable(node) is { } variable)
			{
				this._service.Ignore(GetEntry(variable));
				await this.RefreshAsync();
			}
		}

		/// <summary>Analyzes the variables and lists the findings in the problems.</summary>
		/// <returns>A task that completes when the findings are listed.</returns>
		[RelayCommand]
		public async Task CheckAsync()
		{
			IReadOnlyList<GlobalFinding> findings = await this._service.CheckAsync();
			this.Rebuild(this._service.Current, findings);
			this._workspace.ReportStatus(Loc.Format("Str.Panels.GlobalsChecked", findings.Count));
		}

		/// <summary>Writes the uses of the variables that pass the filter to a CSV file the user chooses.</summary>
		/// <returns>A task that completes when the file is written or the user cancelled.</returns>
		[RelayCommand]
		public async Task ExportCsvAsync()
		{
			String? path = this._dialogs.PickSaveFile(Loc.Get("Str.Panels.GlobalsExportTitle"), Loc.Get("Str.Panels.GlobalsExportFilter"), null, "global-variables.csv");
			if (String.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				IEnumerable<GlobalVariable> variables = this.Groups
					.SelectMany(static g => g.Children)
					.OfType<GlobalVariableNodeViewModel>()
					.Where(static n => n.IsFilterVisible)
					.Select(static n => n.Variable);
				String csv = GlobalVariableCsv.Format(variables);
				await AtomicFile.WriteAllBytesAsync(path, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray(), CancellationToken.None);
				this._workspace.ReportStatus(Loc.Format("Str.Panels.GlobalsExported", Path.GetFileName(path)));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Panels.GlobalsExportTitle"), ex.Message);
			}
		}

		/// <summary>Stops following the project.</summary>
		public void Dispose()
		{
			this._disposed = true;
			this._service.Changed -= this.OnServiceChanged;
		}

		private static String GetGroupKey(GlobalVariableKind kind)
		{
			return kind switch
			{
				GlobalVariableKind.SfallGlobal => "Str.Panels.GlobalsGroupSfall",
				GlobalVariableKind.GameVariable => "Str.Panels.GlobalsGroupGame",
				GlobalVariableKind.MapVariable => "Str.Panels.GlobalsGroupMap",
				_ => "Str.Panels.GlobalsGroupLocal",
			};
		}

		private static GlobalVariable? GetVariable(TreeNodeViewModel? node)
		{
			return node switch
			{
				GlobalVariableNodeViewModel variable => variable.Variable,
				GlobalUseNodeViewModel { Owner: { } owner } => owner,
				_ => null,
			};
		}

		/// <summary>The text of the ignore list and the clipboard for a variable: its macro, its name or its number.</summary>
		private static String GetEntry(GlobalVariable variable)
		{
			return variable.Aliases.Count > 0 ? variable.Aliases[0] : variable.Key;
		}

		private static IReadOnlyList<GlobalFinding> FindingsOf(GlobalVariable variable, Dictionary<String, List<GlobalFinding>> byPlace)
		{
			List<GlobalFinding>? found = null;
			HashSet<String> places = new(StringComparer.OrdinalIgnoreCase);
			foreach (GlobalVariableUse use in variable.Uses)
			{
				String place = use.File + "|" + use.Line.ToString(CultureInfo.InvariantCulture);
				if (!places.Add(place) || !byPlace.TryGetValue(place, out List<GlobalFinding>? candidates))
				{
					continue;
				}

				foreach (GlobalFinding finding in candidates)
				{
					if (variable.Names.Append(variable.DisplayName).Any(name => String.Equals(name, finding.Variable, StringComparison.Ordinal)) || variable.Key == finding.Variable)
					{
						(found ??= []).Add(finding);
					}
				}
			}

			return found is null ? Array.Empty<GlobalFinding>() : found;
		}

		private void Remember(TreeNodeViewModel node)
		{
			if (node is TreeGroupNodeViewModel)
			{
				String key = "collapsed:" + node.Key;
				if (node.IsExpanded)
				{
					this._expanded.Remove(key);
				}
				else
				{
					this._expanded.Add(key);
				}
			}
			else if (node.IsExpanded)
			{
				this._expanded.Add(node.Key);
			}
			else
			{
				this._expanded.Remove(node.Key);
			}
		}

		private void ApplyFilter()
		{
			_ = TreeFilter.Apply(this.Groups, this.FilterText);
		}

		private void OnServiceChanged(Object? sender, EventArgs e)
		{
			_ = this.RefreshAsync();
		}

		private void OnLanguageChanged()
		{
			this.Rebuild(this._service.Current, this.Groups.SelectMany(static g => g.Children).OfType<GlobalVariableNodeViewModel>().SelectMany(static n => n.Findings).ToArray());
		}
	}
}