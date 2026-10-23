namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Buffers;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services.ScriptTools.Registration;

	/// <summary>
	/// One row of the script list grid. Edits are written through to the <see cref="Model"/>; values the game would not accept are refused.
	/// </summary>
	public sealed class ScriptEntryViewModel : ObservableObject
	{
		private static readonly SearchValues<Char> _braces = SearchValues.Create("{}");

		private readonly Action<ScriptEntryViewModel> _changed;
		private readonly Action<String> _rejected;
		private Int32 _number;

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptEntryViewModel"/> class.
		/// </summary>
		/// <param name="model">The entry.</param>
		/// <param name="number">The script id (1-based).</param>
		/// <param name="changed">Called after the user changed a value.</param>
		/// <param name="rejected">Called with an explanation when the user typed a value that is refused.</param>
		public ScriptEntryViewModel(ScriptListEntry model, Int32 number, Action<ScriptEntryViewModel> changed, Action<String> rejected)
		{
			ArgumentNullException.ThrowIfNull(model);
			ArgumentNullException.ThrowIfNull(changed);
			ArgumentNullException.ThrowIfNull(rejected);

			this.Model = model;
			this._number = number;
			this._changed = changed;
			this._rejected = rejected;
		}

		/// <summary>Gets the underlying entry.</summary>
		public ScriptListEntry Model { get; }

		/// <summary>Gets the text a screen reader announces for the entry (the automation name of a list or grid item is the text of its object).</summary>
		/// <returns>The text.</returns>
		public override String ToString()
		{
			return String.Join(" - ", new[] { this.Script, this.GameName }.Where(text => !String.IsNullOrEmpty(text)));
		}

		/// <summary>Gets or sets the script id (1-based); it is the position in the list.</summary>
		public Int32 Number
		{
			get => this._number;
			set => this.SetProperty(ref this._number, value);
		}

		/// <summary>Gets or sets the compiled script file name.</summary>
		public String Script
		{
			get => this.Model.Script;
			set
			{
				String name = (value ?? String.Empty).Trim();
				if (String.Equals(name, this.Model.Script, StringComparison.Ordinal))
				{
					return;
				}

				if (!ScriptRegistrationChecker.IsValidListedName(name))
				{
					this._rejected(Loc.Format("Str.ScriptTools.RegErrScriptNameLength", ScriptRegistrationChecker.MaxScriptNameLength));
					this.OnPropertyChanged();
					return;
				}

				this.Model.Script = name;
				this.OnPropertyChanged();
				this._changed(this);
			}
		}

		/// <summary>Gets or sets the description.</summary>
		public String Description
		{
			get => this.Model.Description;
			set
			{
				String text = value ?? String.Empty;
				if (String.Equals(text, this.Model.Description, StringComparison.Ordinal))
				{
					return;
				}

				this.Model.Description = text;
				this.OnPropertyChanged();
				this._changed(this);
			}
		}

		/// <summary>Gets or sets the number of local variables.</summary>
		public Int32 LocalVars
		{
			get => this.Model.LocalVars;
			set
			{
				if (value == this.Model.LocalVars)
				{
					return;
				}

				this.Model.LocalVars = Math.Max(0, value);
				this.OnPropertyChanged();
				this._changed(this);
			}
		}

		/// <summary>Gets or sets the name of the script in the game.</summary>
		public String GameName
		{
			get => this.Model.GameName;
			set
			{
				String text = value ?? String.Empty;
				if (String.Equals(text, this.Model.GameName, StringComparison.Ordinal))
				{
					return;
				}

				if (text.AsSpan().IndexOfAny(_braces) >= 0)
				{
					this._rejected(Loc.Get("Str.ScriptTools.RegErrGameNameBraces"));
					this.OnPropertyChanged();
					return;
				}

				this.Model.GameName = text;
				this.OnPropertyChanged();
				this._changed(this);
			}
		}
	}
}