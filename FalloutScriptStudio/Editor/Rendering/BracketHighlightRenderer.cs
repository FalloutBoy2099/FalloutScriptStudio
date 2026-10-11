namespace FalloutScriptStudio.Editor.Rendering
{
	using System;
	using System.Windows.Media;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>Highlights the two brackets of a matching pair.</summary>
	public sealed class BracketHighlightRenderer : IBackgroundRenderer
	{
		private EditorPalette _palette;
		private BracketPair? _pair;

		public BracketHighlightRenderer(EditorPalette palette)
		{
			this._palette = palette;
		}

		/// <inheritdoc />
		public KnownLayer Layer => KnownLayer.Selection;

		/// <summary>Gets the highlighted pair.</summary>
		public BracketPair? Pair => this._pair;

		/// <summary>Sets the colors.</summary>
		/// <param name="palette">The palette of the current theme.</param>
		public void SetPalette(EditorPalette palette)
		{
			this._palette = palette;
		}

		/// <summary>Sets the pair to highlight.</summary>
		/// <param name="pair">The pair, or <see langword="null"/> for none.</param>
		/// <returns><see langword="true"/> when the highlighted pair changed.</returns>
		public Boolean SetPair(BracketPair? pair)
		{
			if (Nullable.Equals(this._pair, pair))
			{
				return false;
			}

			this._pair = pair;
			return true;
		}

		/// <inheritdoc />
		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (this._pair is not BracketPair pair)
			{
				return;
			}

			BackgroundGeometryBuilder builder = new BackgroundGeometryBuilder { CornerRadius = 2, AlignToWholePixels = true };
			builder.AddSegment(textView, new TextSegment { StartOffset = pair.OpenOffset, Length = 1 });
			builder.CloseFigure();
			builder.AddSegment(textView, new TextSegment { StartOffset = pair.CloseOffset, Length = 1 });
			Geometry? geometry = builder.CreateGeometry();
			if (geometry is not null)
			{
				drawingContext.DrawGeometry(this._palette.Bracket, this._palette.BracketBorder, geometry);
			}
		}
	}
}
