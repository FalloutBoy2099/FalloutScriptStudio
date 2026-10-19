namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>An entry of the preprocessor definition list of the toolbar; the empty value means "no definition".</summary>
	public sealed class PreprocessorDefineOption : ObservableObject
	{
		/// <summary>Initializes a new instance of the <see cref="PreprocessorDefineOption"/> class.</summary>
		/// <param name="value">The definition, or an empty string for "none".</param>
		public PreprocessorDefineOption(String value)
		{
			this.Value = value;
		}

		/// <summary>Gets the definition (empty for "none").</summary>
		public String Value { get; }

		/// <summary>Gets the text shown in the list: the definition, or a dash (the same in every language) for "none".</summary>
		public String Caption => this.Value.Length == 0 ? "—" : this.Value;
	}
}