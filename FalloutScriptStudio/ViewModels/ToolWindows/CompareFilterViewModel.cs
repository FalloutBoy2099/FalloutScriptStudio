namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using Core.Messages;
	using Localization;

	/// <summary>The toggle of one status in the filter bar of the comparison: it shows or hides the lines of that status, and counts them.</summary>
	public sealed class CompareFilterViewModel : ObservableObject
	{
		private Boolean _isChecked;
		private Int32 _count;

		/// <summary>Initializes a new instance of the <see cref="CompareFilterViewModel"/> class.</summary>
		/// <param name="status">The status the toggle is for.</param>
		/// <param name="isChecked">Whether the lines of the status are shown at first.</param>
		public CompareFilterViewModel(MessageComparisonStatus status, Boolean isChecked)
		{
			this.Status = status;
			this._isChecked = isChecked;
		}

		/// <summary>Occurs after <see cref="IsChecked"/> changed.</summary>
		public event EventHandler? Toggled;

		/// <summary>Gets the status the toggle is for.</summary>
		public MessageComparisonStatus Status { get; }

		/// <summary>Gets or sets a value indicating whether the lines of the status are shown.</summary>
		public Boolean IsChecked
		{
			get => this._isChecked;
			set
			{
				if (this.SetProperty(ref this._isChecked, value))
				{
					this.Toggled?.Invoke(this, EventArgs.Empty);
				}
			}
		}

		/// <summary>Gets the number of lines of the status in the selected comparison.</summary>
		public Int32 Count
		{
			get => this._count;
			private set
			{
				if (this.SetProperty(ref this._count, value))
				{
					this.OnPropertyChanged(nameof(this.Label));
				}
			}
		}

		/// <summary>Gets the text of the toggle: the status and its count.</summary>
		public String Label => Loc.Format("Str.ToolWindows.Compare.FilterLabel", ComparisonTexts.StatusName(this.Status), this._count);

		/// <summary>Gets the text a screen reader announces for the toggle (the automation name of a list or grid item is the text of its object).</summary>
		/// <returns>The text.</returns>
		public override String ToString()
		{
			return this.Label;
		}

		/// <summary>Sets the count without raising <see cref="Toggled"/>.</summary>
		/// <param name="count">The count.</param>
		public void SetCount(Int32 count)
		{
			this.Count = count;
		}

		/// <summary>Raises the change of the texts after a language change.</summary>
		public void RefreshTexts()
		{
			this.OnPropertyChanged(nameof(this.Label));
		}
	}
}