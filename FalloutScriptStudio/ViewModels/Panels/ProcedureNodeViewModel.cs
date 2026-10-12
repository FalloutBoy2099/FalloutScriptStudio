namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>A procedure in the procedures tree.</summary>
	public sealed partial class ProcedureNodeViewModel : TreeNodeViewModel
	{
		private ScriptProcedure _procedure;

		/// <summary>Initializes a node.</summary>
		public ProcedureNodeViewModel(ScriptProcedure procedure, String title, String key, Boolean isLocal)
			: base(title, key)
		{
			ArgumentNullException.ThrowIfNull(procedure);
			this._procedure = procedure;
			this.IsLocal = isLocal;
		}

		/// <summary>Gets the procedure; the node keeps showing the same procedure when the script is parsed again, with the new data.</summary>
		public ScriptProcedure Procedure
		{
			get => this._procedure;
			internal set
			{
				if (!ReferenceEquals(this._procedure, value))
				{
					this._procedure = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.CanEditNodeCode));
					this.OnPropertyChanged(nameof(this.IsStandard));
				}
			}
		}

		/// <summary>Gets a value indicating whether the procedure is defined in the current script.</summary>
		public Boolean IsLocal { get; }

		/// <inheritdoc/>
		public override Boolean CanModify => this.IsLocal;

		/// <inheritdoc/>
		public override Boolean IsSymbol => true;

		/// <summary>
		/// Gets a value indicating whether the node code editor can be opened for the procedure: a procedure of this script that is a
		/// dialogue node (its name contains <c>node</c>) or <c>talk_p_proc</c>.
		/// </summary>
		public Boolean CanEditNodeCode => this.IsLocal
			&& (this.Procedure.Name.Contains("node", StringComparison.OrdinalIgnoreCase)
				|| String.Equals(this.Procedure.Name, "talk_p_proc", StringComparison.OrdinalIgnoreCase));

		/// <summary>Gets a value indicating whether the procedure is one of the standard handlers (<c>talk_p_proc</c>…).</summary>
		public Boolean IsStandard => this.Procedure.IsStandard;

		/// <summary>True while the caret is inside this procedure.</summary>
		[ObservableProperty]
		public partial Boolean IsCurrent { get; set; }
	}
}
