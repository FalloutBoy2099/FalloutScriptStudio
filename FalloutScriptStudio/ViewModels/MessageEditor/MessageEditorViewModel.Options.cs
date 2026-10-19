namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;

	public sealed partial class MessageEditorViewModel
	{
		/// <summary>The font sizes (in points) the user can choose from.</summary>
		private static readonly Int32[] _fontSizes = { 9, 10, 12, 14, 16, 18, 20, 24 };

		private Int32 _fontSizeIndex;
		private Boolean _highlightComments = true;
		private CommentHighlightColor _commentColor = CommentHighlightColor.LightYellow;
		private Boolean _showLipColumn;
		private Boolean _useOem866;
		private Boolean _isWindowOnTop;
		private Int32 _commentStep = 10;
		private Boolean _suppressEncodingReload;
		private Boolean _optionsInitialized;

		/// <summary>Gets the font sizes (in points) the user can choose from.</summary>
		public IReadOnlyList<Int32> FontSizes => _fontSizes;

		/// <summary>Gets all choices of the comment highlight color.</summary>
		public IReadOnlyList<CommentHighlightColor> CommentColors { get; } = new[]
		{
			CommentHighlightColor.LightYellow,
			CommentHighlightColor.PaleGreen,
			CommentHighlightColor.Lavender,
		};

		/// <summary>Gets or sets the index of the selected font size.</summary>
		public Int32 FontSizeIndex
		{
			get => this._fontSizeIndex;
			set
			{
				Int32 index = Math.Clamp(value, 0, _fontSizes.Length - 1);
				if (this.SetProperty(ref this._fontSizeIndex, index))
				{
					this.OnPropertyChanged(nameof(this.FontSize));
					this.OnPropertyChanged(nameof(this.LineFontSize));
					this.OnPropertyChanged(nameof(this.LineColumnWidth));
					this.StoreSetting(messages => messages.FontSizeIndex = index);
				}
			}
		}

		/// <summary>Gets the font size of the message text in device independent pixels.</summary>
		public Double FontSize => _fontSizes[this._fontSizeIndex] * 96.0 / 72.0;

		/// <summary>Gets the font size of the line number column in device independent pixels (a little smaller than the text).</summary>
		public Double LineFontSize => this.GetLinePoints() * 96.0 / 72.0;

		/// <summary>Gets the width of the line number column in device independent pixels; it grows with the font.</summary>
		public Double LineColumnWidth
		{
			get
			{
				Int32 points = this.GetLinePoints();
				return points >= 13 ? 75 : points >= 11 ? 65 : 54;
			}
		}

		/// <summary>Gets or sets a value indicating whether comment lines get a background color.</summary>
		public Boolean HighlightComments
		{
			get => this._highlightComments;
			set
			{
				if (this.SetProperty(ref this._highlightComments, value))
				{
					this.StoreSetting(messages => messages.HighlightComments = value);
				}
			}
		}

		/// <summary>Gets or sets the background color of comment lines.</summary>
		public CommentHighlightColor CommentColor
		{
			get => this._commentColor;
			set
			{
				if (this.SetProperty(ref this._commentColor, value))
				{
					this.StoreSetting(messages => messages.CommentColor = value);
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether the sound (lip) column is shown.</summary>
		public Boolean ShowLipColumn
		{
			get => this._showLipColumn;
			set
			{
				if (this.SetProperty(ref this._showLipColumn, value))
				{
					this.StoreSetting(messages => messages.ShowLipColumn = value);
					this.UpdateSearchMatches();
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether the file is read and written with the DOS code page 866.</summary>
		public Boolean UseOem866
		{
			get => this._useOem866;
			set
			{
				if (this.SetProperty(ref this._useOem866, value))
				{
					this.OnPropertyChanged(nameof(this.EncodingName));
					if (!this._suppressEncodingReload && !this._applyingSettings)
					{
						this.EncodingReloadTask = this.ReloadForEncodingAsync(!value);
					}
				}
			}
		}

		/// <summary>Gets a task that completes when the reload that follows an encoding change is finished.</summary>
		public Task EncodingReloadTask { get; private set; } = Task.CompletedTask;

		/// <summary>Gets the name of the active encoding for the status bar.</summary>
		public String EncodingName => this._useOem866 ? "OEM 866" : "ANSI";

		/// <summary>Gets or sets a value indicating whether the window stays above its owner.</summary>
		public Boolean IsWindowOnTop
		{
			get => this._isWindowOnTop;
			set => this.SetProperty(ref this._isWindowOnTop, value);
		}

		/// <summary>Gets or sets the step added to the rounded message number that follows a comment.</summary>
		public Int32 CommentStep
		{
			get => this._commentStep;
			set => this.SetProperty(ref this._commentStep, value < 1 ? 1 : value);
		}

		/// <summary>Gets the encoding used to read and write the file.</summary>
		public Encoding CurrentEncoding => this._useOem866 ? TextEncodings.Oem866 : TextEncodings.Ansi;

		/// <summary>Makes the text one step larger.</summary>
		[RelayCommand]
		private void IncreaseFontSize()
		{
			this.FontSizeIndex++;
		}

		/// <summary>Makes the text one step smaller.</summary>
		[RelayCommand]
		private void DecreaseFontSize()
		{
			this.FontSizeIndex--;
		}

		/// <summary>Chooses the comment highlight color.</summary>
		/// <param name="color">The color.</param>
		[RelayCommand]
		private void SetCommentColor(CommentHighlightColor color)
		{
			this.CommentColor = color;
		}

		/// <summary>Chooses the font size.</summary>
		/// <param name="index">The index in <see cref="FontSizes"/>.</param>
		[RelayCommand]
		private void SetFontSizeIndex(Int32 index)
		{
			this.FontSizeIndex = index;
		}

		private Int32 GetLinePoints()
		{
			Int32 size = _fontSizes[this._fontSizeIndex];
			return size switch
			{
				24 => 14,
				20 => 13,
				18 => 12,
				16 => 11,
				14 => 10,
				12 => 9,
				_ => size - 1,
			};
		}

		private void ReadSettings()
		{
			MessageSettings messages = this._settings.Settings.Messages;
			this._applyingSettings = true;
			try
			{
				this.FontSizeIndex = messages.FontSizeIndex;
				this.HighlightComments = messages.HighlightComments;
				this.CommentColor = messages.CommentColor;
				this.ShowLipColumn = messages.ShowLipColumn;
				if (!this._optionsInitialized)
				{
					this.UseOem866 = this._settings.Settings.Editor.FileEncoding == TextEncodingKind.Oem866;
					this._optionsInitialized = true;
				}
			}
			finally
			{
				this._applyingSettings = false;
			}
		}

		private void StoreSetting(Action<MessageSettings> change)
		{
			if (this._applyingSettings)
			{
				return;
			}

			this._applyingSettings = true;
			try
			{
				this._settings.Update(settings => change(settings.Messages));
			}
			finally
			{
				this._applyingSettings = false;
			}
		}

		private void OnSettingsChanged(Object? sender, SettingsChangedEventArgs e)
		{
			if (!this._applyingSettings)
			{
				this.ReadSettings();
			}
		}
	}
}