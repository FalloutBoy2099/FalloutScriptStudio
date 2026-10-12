namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Media;

	/// <summary>
	/// The unconstrained (single line) text measurements of a node. They are shared between the passes of one layout and handed on to the
	/// next layout of the same node, so that collapsing, expanding or resizing a node does not shape its texts again (creating a
	/// <see cref="FormattedText"/> takes about a tenth of a millisecond, and a node has a dozen of them).
	/// </summary>
	internal sealed class NaturalTextCache
	{
		private readonly DiagramTextStyles _styles;
		private readonly Dictionary<(String Text, Typeface Face, Double Size, Brush Brush), FormattedText> _items = new Dictionary<(String, Typeface, Double, Brush), FormattedText>();
		private NaturalTextCache? _previous;

		/// <summary>Initializes a cache.</summary>
		/// <param name="styles">Creates the texts.</param>
		/// <param name="previous">The cache of the previous layout of the node, or <see langword="null"/>; its texts are reused when they are asked for again.</param>
		public NaturalTextCache(DiagramTextStyles styles, NaturalTextCache? previous = null)
		{
			this._styles = styles;
			this._previous = previous;
		}

		/// <summary>Gets the text for a string, a font and a brush.</summary>
		/// <param name="text">The text.</param>
		/// <param name="face">The typeface.</param>
		/// <param name="size">The font size.</param>
		/// <param name="brush">The foreground.</param>
		/// <returns>The formatted text; the same instance each time the same arguments are given.</returns>
		public FormattedText Get(String text, Typeface face, Double size, Brush brush)
		{
			(String, Typeface, Double, Brush) key = (text, face, size, brush);
			if (this._items.TryGetValue(key, out FormattedText? formatted))
			{
				return formatted;
			}

			if (this._previous is null || !this._previous._items.TryGetValue(key, out formatted))
			{
				formatted = this._styles.Create(text, face, size, brush);
			}

			this._items[key] = formatted;
			return formatted;
		}

		/// <summary>Lets go of the cache this one was created from; only the texts that were asked for are kept.</summary>
		public void Seal()
		{
			this._previous = null;
		}
	}
}
