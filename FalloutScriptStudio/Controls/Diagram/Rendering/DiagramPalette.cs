namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using Model;

	/// <summary>
	/// Immutable snapshot of every brush and pen the diagram draws with. It is resolved from the WPF-UI Fluent theme
	/// resources (so light and dark themes are followed) with fallbacks to the system colours.
	/// </summary>
	internal sealed class DiagramPalette
	{
		private const Int32 KindCount = 5;

		private static readonly Double[] _linkDashes = { 4.0, 3.0 };

		private readonly Brush[] _headerTints = new Brush[KindCount];
		private readonly Pen[] _kindPens = new Pen[KindCount];

		private DiagramPalette(
			Color background,
			Color card,
			Color textPrimary,
			Color textSecondary,
			Color accent,
			Color success,
			Color caution,
			Color critical,
			Color neutral)
		{
			this.IsDark = Luminance(background) < 0.5;
			Boolean dark = this.IsDark;

			Color nodeFill = Composite(card, background);
			Color nodeStroke = Lerp(nodeFill, textPrimary, dark ? 0.30 : 0.22);

			this.Background = Freeze(new SolidColorBrush(background));
			this.GridDot = Freeze(new SolidColorBrush(Lerp(background, textPrimary, dark ? 0.18 : 0.14)));
			this.NodeFill = Freeze(new SolidColorBrush(nodeFill));
			this.NodeStroke = MakePen(nodeStroke, 1.0);
			this.NodeStrokeHover = MakePen(Lerp(nodeFill, textPrimary, dark ? 0.55 : 0.45), 1.0);
			this.TextPrimary = Freeze(new SolidColorBrush(textPrimary));
			this.TextSecondary = Freeze(new SolidColorBrush(textSecondary));
			this.TextTertiary = Freeze(new SolidColorBrush(Lerp(nodeFill, textPrimary, 0.45)));
			this.Accent = Freeze(new SolidColorBrush(accent));
			this.AccentPen = MakePen(accent, 2.0);
			this.AccentRingPen = MakePen(WithAlpha(accent, 0.28), 6.0);
			this.AccentSoft = Freeze(new SolidColorBrush(WithAlpha(accent, 0.16)));
			this.SelectionBandPen = MakePen(accent, 1.0);
			this.ShadowNear = Freeze(new SolidColorBrush(Color.FromArgb(dark ? (Byte)90 : (Byte)34, 0, 0, 0)));
			this.ShadowFar = Freeze(new SolidColorBrush(Color.FromArgb(dark ? (Byte)46 : (Byte)16, 0, 0, 0)));
			this.HoverOverlay = Freeze(new SolidColorBrush(WithAlpha(textPrimary, 0.09)));
			this.GlyphPen = MakePen(textSecondary, 1.5, roundCaps: true);
			this.GlyphPenStrong = MakePen(textPrimary, 1.5, roundCaps: true);
			this.GlyphPenOff = MakePen(Lerp(nodeFill, textPrimary, 0.40), 1.5, roundCaps: true);
			this.GlyphWarningPen = MakePen(critical, 1.5, roundCaps: true);
			this.TitleSeparatorPen = MakePen(Lerp(nodeFill, textPrimary, dark ? 0.16 : 0.10), 1.0);

			this.LinkNormal = Freeze(new SolidColorBrush(WithAlpha(textPrimary, dark ? 0.55 : 0.48)));
			this.LinkOutgoing = Freeze(new SolidColorBrush(dark ? Color.FromRgb(0xFF, 0x8A, 0x4C) : Color.FromRgb(0xE8, 0x59, 0x0C)));
			this.LinkIncoming = Freeze(new SolidColorBrush(accent));

			this.SetKind(DiagramNodeKind.Default, Composite(WithAlpha(accent, 0.12), nodeFill), nodeStroke, false);
			this.SetKind(DiagramNodeKind.Start, Composite(WithAlpha(success, 0.24), nodeFill), success, false);
			this.SetKind(DiagramNodeKind.End, Composite(WithAlpha(critical, 0.22), nodeFill), critical, false);
			this.SetKind(DiagramNodeKind.NoIncomingLinks, Composite(WithAlpha(caution, dark ? 0.16 : 0.24), nodeFill), caution, false);
			this.SetKind(DiagramNodeKind.Unused, Composite(WithAlpha(neutral, 0.16), nodeFill), Lerp(nodeFill, neutral, 0.9), true);

			this.ReplyFill = Freeze(new SolidColorBrush(Composite(WithAlpha(accent, 0.16), nodeFill)));
			this.ReplyText = Freeze(new SolidColorBrush(Lerp(textPrimary, accent, dark ? 0.35 : 0.55)));
			this.ReplyIcon = Freeze(new SolidColorBrush(accent));
			this.OptionFill = Freeze(new SolidColorBrush(Composite(WithAlpha(caution, dark ? 0.12 : 0.22), nodeFill)));
			this.OptionText = Freeze(new SolidColorBrush(Lerp(textPrimary, critical, dark ? 0.30 : 0.55)));
			this.OptionIcon = Freeze(new SolidColorBrush(caution));
			this.CallFill = Freeze(new SolidColorBrush(Composite(WithAlpha(neutral, 0.20), nodeFill)));
			this.DetailFill = Freeze(new SolidColorBrush(Composite(WithAlpha(textPrimary, 0.06), nodeFill)));

			Color noteFill = dark ? Color.FromRgb(0x5A, 0x50, 0x24) : Color.FromRgb(0xFF, 0xF4, 0xB8);
			Color noteStroke = dark ? Color.FromRgb(0x8C, 0x7C, 0x36) : Color.FromRgb(0xD9, 0xC4, 0x5F);
			Color noteText = dark ? Color.FromRgb(0xF6, 0xEF, 0xC6) : Color.FromRgb(0x2B, 0x25, 0x00);
			this.NoteFill = Freeze(new SolidColorBrush(noteFill));
			this.NoteStroke = MakePen(noteStroke, 1.0);
			this.NoteText = Freeze(new SolidColorBrush(noteText));
			this.NotePlaceholder = Freeze(new SolidColorBrush(WithAlpha(noteText, 0.5)));
		}

		public Boolean IsDark { get; }

		public Brush Background { get; }

		public Brush GridDot { get; }

		public Brush NodeFill { get; }

		public Pen NodeStroke { get; }

		public Pen NodeStrokeHover { get; }

		public Brush TextPrimary { get; }

		public Brush TextSecondary { get; }

		public Brush TextTertiary { get; }

		public Brush Accent { get; }

		public Pen AccentPen { get; }

		public Pen AccentRingPen { get; }

		public Brush AccentSoft { get; }

		public Pen SelectionBandPen { get; }

		public Brush ShadowNear { get; }

		public Brush ShadowFar { get; }

		public Brush HoverOverlay { get; }

		public Pen GlyphPen { get; }

		public Pen GlyphPenStrong { get; }

		public Pen GlyphPenOff { get; }

		public Pen GlyphWarningPen { get; }

		public Pen TitleSeparatorPen { get; }

		public Brush LinkNormal { get; }

		public Brush LinkOutgoing { get; }

		public Brush LinkIncoming { get; }

		public Brush ReplyFill { get; }

		public Brush ReplyText { get; }

		public Brush ReplyIcon { get; }

		public Brush OptionFill { get; }

		public Brush OptionText { get; }

		public Brush OptionIcon { get; }

		public Brush CallFill { get; }

		public Brush DetailFill { get; }

		public Brush NoteFill { get; }

		public Pen NoteStroke { get; }

		public Brush NoteText { get; }

		public Brush NotePlaceholder { get; }

		/// <summary>
		/// Resolves a palette from the resources visible to <paramref name="owner"/>.
		/// </summary>
		public static DiagramPalette Resolve(FrameworkElement owner)
		{
			ArgumentNullException.ThrowIfNull(owner);
			Color background = Find(owner, SystemColors.ControlColor, "ApplicationBackgroundBrush", "SolidBackgroundFillColorBaseBrush");
			Boolean dark = Luminance(background) < 0.5;
			Color card = Find(owner, SystemColors.WindowColor, "CardBackgroundFillColorDefaultBrush", "ControlFillColorDefaultBrush");
			Color textPrimary = Find(owner, SystemColors.ControlTextColor, "TextFillColorPrimaryBrush");
			Color textSecondary = Find(owner, Lerp(background, textPrimary, 0.72), "TextFillColorSecondaryBrush");
			Color accent = Find(owner, SystemColors.HighlightColor, "AccentFillColorDefaultBrush", "SystemAccentColorPrimaryBrush");
			Color success = Find(owner, dark ? Color.FromRgb(0x6C, 0xCB, 0x5F) : Color.FromRgb(0x0F, 0x7B, 0x0F), "SystemFillColorSuccessBrush");
			Color caution = Find(owner, dark ? Color.FromRgb(0xFC, 0xE1, 0x00) : Color.FromRgb(0x9D, 0x5D, 0x00), "SystemFillColorCautionBrush");
			Color critical = Find(owner, dark ? Color.FromRgb(0xFF, 0x99, 0xA4) : Color.FromRgb(0xC4, 0x2B, 0x1C), "SystemFillColorCriticalBrush");
			Color neutral = Find(owner, dark ? Color.FromRgb(0x9D, 0x9D, 0x9D) : Color.FromRgb(0x6B, 0x6B, 0x6B), "SystemFillColorNeutralBrush");
			return new DiagramPalette(background, card, textPrimary, textSecondary, accent, success, caution, critical, neutral);
		}

		public Brush GetHeaderTint(DiagramNodeKind kind)
		{
			return this._headerTints[(Int32)kind];
		}

		public Pen GetKindPen(DiagramNodeKind kind)
		{
			return this._kindPens[(Int32)kind];
		}

		public static Pen MakeLinkPen(Brush brush, Double thickness)
		{
			Pen pen = new(brush, thickness)
			{
				StartLineCap = PenLineCap.Round,
				EndLineCap = PenLineCap.Round,
				LineJoin = PenLineJoin.Round
			};
			pen.Freeze();
			return pen;
		}

		private static Color Find(FrameworkElement owner, Color fallback, params String[] keys)
		{
			foreach (String key in keys)
			{
				Object? resource = owner.TryFindResource(key);
				if (resource is SolidColorBrush brush)
				{
					return brush.Color;
				}

				if (resource is Color color)
				{
					return color;
				}
			}

			return fallback;
		}

		private static Double Luminance(Color c)
		{
			return ((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B)) / 255.0;
		}

		private static Color WithAlpha(Color c, Double alpha)
		{
			return Color.FromArgb((Byte)Math.Clamp(Math.Round(alpha * c.A), 0.0, 255.0), c.R, c.G, c.B);
		}

		private static Color Composite(Color over, Color under)
		{
			Double a = over.A / 255.0;
			return Color.FromRgb(
				(Byte)Math.Round((over.R * a) + (under.R * (1.0 - a))),
				(Byte)Math.Round((over.G * a) + (under.G * (1.0 - a))),
				(Byte)Math.Round((over.B * a) + (under.B * (1.0 - a))));
		}

		private static Color Lerp(Color from, Color to, Double t)
		{
			return Color.FromRgb(
				(Byte)Math.Round(from.R + ((to.R - from.R) * t)),
				(Byte)Math.Round(from.G + ((to.G - from.G) * t)),
				(Byte)Math.Round(from.B + ((to.B - from.B) * t)));
		}

		private static SolidColorBrush Freeze(SolidColorBrush brush)
		{
			brush.Freeze();
			return brush;
		}

		private static Pen MakePen(Color color, Double thickness, Boolean roundCaps = false)
		{
			Pen pen = new(Freeze(new SolidColorBrush(color)), thickness);
			if (roundCaps)
			{
				pen.StartLineCap = PenLineCap.Round;
				pen.EndLineCap = PenLineCap.Round;
				pen.LineJoin = PenLineJoin.Round;
			}

			pen.Freeze();
			return pen;
		}

		private void SetKind(DiagramNodeKind kind, Color tint, Color border, Boolean dashed)
		{
			Pen pen = new(Freeze(new SolidColorBrush(border)), kind == DiagramNodeKind.Default ? 1.0 : 1.5);
			if (dashed)
			{
				pen.DashStyle = new DashStyle(_linkDashes, 0.0);
			}

			pen.Freeze();
			this._kindPens[(Int32)kind] = pen;
			this._headerTints[(Int32)kind] = Freeze(new SolidColorBrush(tint));
		}
	}
}