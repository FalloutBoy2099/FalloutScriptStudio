namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>A variable in the variables tree (or a local variable under a procedure).</summary>
	public sealed class VariableNodeViewModel : TreeNodeViewModel
	{
		private ScriptVariable _variable;

		/// <summary>Initializes a node.</summary>
		public VariableNodeViewModel(ScriptVariable variable, String key)
			: base(variable.Name, key)
		{
			this._variable = variable;
		}

		/// <inheritdoc/>
		public override Boolean IsSymbol => true;

		/// <summary>Gets the variable; the node keeps showing the same variable when the script is parsed again, with the new data.</summary>
		public ScriptVariable Variable
		{
			get => this._variable;
			internal set
			{
				if (!ReferenceEquals(this._variable, value))
				{
					this._variable = value;
					this.OnPropertyChanged();
				}
			}
		}
	}
}
