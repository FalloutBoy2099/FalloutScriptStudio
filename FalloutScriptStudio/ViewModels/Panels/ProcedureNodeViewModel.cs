namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>A procedure in the procedures tree.</summary>
	public sealed partial class ProcedureNodeViewModel : TreeNodeViewModel
	{
		/// <summary>Initializes a node.</summary>
		public ProcedureNodeViewModel(ScriptProcedure procedure, String title, String key, Boolean isLocal)
			: base(title, key)
		{
			ArgumentNullException.ThrowIfNull(procedure);
			this.Procedure = procedure;
			this.IsLocal = isLocal;
		}

		/// <summary>Gets the procedure.</summary>
		public ScriptProcedure Procedure { get; }

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
