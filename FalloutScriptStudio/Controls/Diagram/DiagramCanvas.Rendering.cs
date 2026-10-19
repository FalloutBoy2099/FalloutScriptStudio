namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using Rendering;
	using Localization;

	// Theme, DPI and language handling: keeps the render context of the scene up to date.
	public sealed partial class DiagramCanvas
	{
		private static readonly DependencyProperty ThemeProbeProperty = DependencyProperty.Register(
			"ThemeProbe",
			typeof(Object),
			typeof(DiagramCanvas),
			new PropertyMetadata(null, OnThemeChanged));

		/// <inheritdoc />
		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == FontFamilyProperty && this._scene != null)
			{
				this.RebuildContext();
			}
		}

		/// <inheritdoc />
		protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
		{
			base.OnDpiChanged(oldDpi, newDpi);
			this.RebuildContext();
		}

		private static void OnThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			DiagramCanvas canvas = (DiagramCanvas)d;
			if (canvas._scene != null)
			{
				canvas.RebuildContext();
			}
		}

		private DiagramRenderContext CreateContext()
		{
			DiagramPalette palette = DiagramPalette.Resolve(this);
			DiagramTextStyles styles = new(this.FontFamily, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			return new DiagramRenderContext(palette, styles, this.LowDetail, this.HighQuality);
		}

		private void OnCanvasLoaded(Object sender, RoutedEventArgs e)
		{
			this.RebuildContext();
			Loc.LanguageChanged -= this.OnLanguageChanged;
			Loc.LanguageChanged += this.OnLanguageChanged;
		}

		private void OnCanvasUnloaded(Object sender, RoutedEventArgs e)
		{
			Loc.LanguageChanged -= this.OnLanguageChanged;
		}

		private void OnLanguageChanged(Object? sender, EventArgs e)
		{
			// The texts drawn by the canvas (the placeholder of a note) are read when an item is drawn: draw everything again.
			this.RebuildContext();
		}

		private void RebuildContext()
		{
			this._scene.SetContext(this.CreateContext());
			if (this._noteEditor != null)
			{
				DiagramPalette palette = this._scene.Context.Palette;
				this._noteEditor.Background = palette.NoteFill;
				this._noteEditor.Foreground = palette.NoteText;
			}
		}
	}
}