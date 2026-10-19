namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>
	/// The choice "inherit the global setting / override it in this project" of one option of the project settings.
	/// </summary>
	public class OverrideMode : ObservableObject
	{
		private Boolean _isOverridden;

		/// <summary>
		/// Initializes a new instance of the <see cref="OverrideMode"/> class.
		/// </summary>
		/// <param name="isOverridden">Whether the project overrides the global setting.</param>
		public OverrideMode(Boolean isOverridden)
		{
			this._isOverridden = isOverridden;
		}

		/// <summary>Gets or sets a value indicating whether the project overrides the global setting.</summary>
		public Boolean IsOverridden
		{
			get => this._isOverridden;
			set
			{
				if (this._isOverridden != value)
				{
					this._isOverridden = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.ModeIndex));
					this.OnOverrideChanged(value);
				}
			}
		}

		/// <summary>Gets or sets the position in the "Inherit global / Override" list: 0 inherits, 1 overrides.</summary>
		public Int32 ModeIndex
		{
			get => this._isOverridden ? 1 : 0;
			set => this.IsOverridden = value == 1;
		}

		/// <summary>Called after the mode changed.</summary>
		/// <param name="isOverridden">The new value of <see cref="IsOverridden"/>.</param>
		protected virtual void OnOverrideChanged(Boolean isOverridden)
		{
		}
	}
}