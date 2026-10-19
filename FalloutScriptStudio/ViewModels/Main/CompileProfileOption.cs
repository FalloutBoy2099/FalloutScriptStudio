namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>An entry of the compile profile list of the toolbar; the empty value means "no profile".</summary>
	public sealed class CompileProfileOption : ObservableObject
	{
		/// <summary>Initializes a new instance of the <see cref="CompileProfileOption"/> class.</summary>
		/// <param name="value">The name of the profile, or an empty string for "none".</param>
		public CompileProfileOption(String value)
		{
			this.Value = value;
		}

		/// <summary>Gets the name of the profile (empty for "none").</summary>
		public String Value { get; }

		/// <summary>Gets the text shown in the list: the name of the profile, or a dash (the same in every language) for "none".</summary>
		public String Caption => this.Value.Length == 0 ? "—" : this.Value;
	}
}