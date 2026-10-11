namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>A variable in the variables tree (or a local variable under a procedure).</summary>
	public sealed class VariableNodeViewModel : TreeNodeViewModel
	{
		/// <summary>Initializes a node.</summary>
		public VariableNodeViewModel(ScriptVariable variable, String key)
			: base(variable.Name, key)
		{
			this.Variable = variable;
		}

		/// <inheritdoc/>
		public override Boolean IsSymbol => true;

		/// <summary>Gets the variable.</summary>
		public ScriptVariable Variable { get; }
	}
}
