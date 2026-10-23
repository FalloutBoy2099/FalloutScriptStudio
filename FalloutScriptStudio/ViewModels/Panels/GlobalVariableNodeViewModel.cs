namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Problems;

	/// <summary>How serious the findings of a global variable are (the badge of its row).</summary>
	public enum GlobalNodeSeverity
	{
		/// <summary>No finding.</summary>
		None = 0,

		/// <summary>Information only.</summary>
		Info = 1,

		/// <summary>At least one warning.</summary>
		Warning = 2,
	}

	/// <summary>
	/// A global variable in the tree of the Globals tab. Its uses are created when the node is expanded for the first time, so that a project
	/// with tens of thousands of uses does not create a node for each of them.
	/// </summary>
	public sealed partial class GlobalVariableNodeViewModel : TreeNodeViewModel
	{
		private readonly Lazy<String> _searchText;
		private Boolean _loaded;

		/// <summary>Initializes a node.</summary>
		/// <param name="variable">The variable.</param>
		/// <param name="key">A key that is stable across rebuilds of the tree.</param>
		public GlobalVariableNodeViewModel(GlobalVariable variable, String key)
			: base(variable.DisplayName, key)
		{
			ArgumentNullException.ThrowIfNull(variable);
			this.Variable = variable;
			this._searchText = new Lazy<String>(() => this.BuildSearchText());
			this.Children.Add(GlobalUseNodeViewModel.CreatePlaceholder());
			this.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(this.IsExpanded) && this.IsExpanded)
				{
					this.EnsureChildren();
				}
			};
		}

		/// <summary>Gets the variable.</summary>
		public GlobalVariable Variable { get; }

		/// <inheritdoc/>
		public override Boolean IsSymbol => true;

		/// <summary>Gets the findings about the variable.</summary>
		public IReadOnlyList<GlobalFinding> Findings { get; private set; } = Array.Empty<GlobalFinding>();

		/// <summary>Gets the most serious finding.</summary>
		public GlobalNodeSeverity Severity { get; private set; }

		/// <summary>Gets the findings in the current language, one per line (the tool tip of the badge).</summary>
		public String FindingText => String.Join(Environment.NewLine, this.Findings.Select(static f => ProblemFactory.GlobalVariable(f).Message));

		/// <summary>Gets the number of uses (shown after the name).</summary>
		public Int32 UseCount => this.Variable.Uses.Count;

		/// <summary>Gets the access of the variable as a symbol: <c>R</c>, <c>W</c> or <c>RW</c>.</summary>
		public String AccessSymbol => this.Variable.ReadCount > 0 ? (this.Variable.WriteCount > 0 ? "RW" : "R") : "W";

		/// <summary>Creates the nodes of the uses (once).</summary>
		public void EnsureChildren()
		{
			if (this._loaded)
			{
				return;
			}

			this._loaded = true;
			this.Children.Clear();
			Int32 index = 0;
			foreach (GlobalVariableUse use in this.Variable.Uses)
			{
				this.Children.Add(new GlobalUseNodeViewModel(use, this.Variable, this.Key + "/" + (index++).ToString(System.Globalization.CultureInfo.InvariantCulture)));
			}
		}

		/// <summary>Sets the findings of the variable.</summary>
		/// <param name="findings">The findings.</param>
		public void SetFindings(IReadOnlyList<GlobalFinding> findings)
		{
			this.Findings = findings;
			this.Severity = findings.Count == 0
				? GlobalNodeSeverity.None
				: findings.Any(static f => f.Severity == GlobalFindingSeverity.Warning) ? GlobalNodeSeverity.Warning : GlobalNodeSeverity.Info;
			this.OnPropertyChanged(nameof(this.Findings));
			this.OnPropertyChanged(nameof(this.Severity));
			this.OnPropertyChanged(nameof(this.FindingText));
		}

		/// <inheritdoc/>
		protected override String GetFilterText()
		{
			return this._searchText.Value;
		}

		private String BuildSearchText()
		{
			HashSet<String> parts = new(StringComparer.OrdinalIgnoreCase) { this.Variable.DisplayName };
			foreach (String name in this.Variable.Names)
			{
				parts.Add(name);
			}

			foreach (GlobalVariableUse use in this.Variable.Uses)
			{
				parts.Add(use.FileName);
			}

			return String.Join('\n', parts);
		}
	}
}