namespace FalloutScriptStudio.Views.Help
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Documents;
	using FalloutScriptStudio.Core.Help;

	/// <summary>
	/// A text block that shows a signature with each part in its own color: the colors are the theme brushes, so they follow the light and
	/// dark theme, and the type of each parameter stands out from its name.
	/// </summary>
	public sealed class HelpSignatureText : TextBlock
	{
		/// <summary>Identifies <see cref="Segments"/>.</summary>
		public static readonly DependencyProperty SegmentsProperty = DependencyProperty.Register(
			nameof(Segments),
			typeof(IReadOnlyList<SignatureSegment>),
			typeof(HelpSignatureText),
			new PropertyMetadata(null, OnSegmentsChanged));

		/// <summary>Gets or sets the pieces of the signature.</summary>
		public IReadOnlyList<SignatureSegment>? Segments
		{
			get => (IReadOnlyList<SignatureSegment>?)this.GetValue(SegmentsProperty);
			set => this.SetValue(SegmentsProperty, value);
		}

		/// <summary>Gets the resource key of the brush a kind of piece is drawn with.</summary>
		/// <param name="kind">The kind of piece.</param>
		/// <returns>The key of a theme brush.</returns>
		public static String GetBrushKey(SignatureSegmentKind kind)
		{
			return kind switch
			{
				SignatureSegmentKind.ReturnType => "TextFillColorSecondaryBrush",
				SignatureSegmentKind.Name => "AccentTextFillColorPrimaryBrush",
				SignatureSegmentKind.ParameterType => "AccentTextFillColorSecondaryBrush",
				SignatureSegmentKind.ParameterName => "TextFillColorPrimaryBrush",
				_ => "TextFillColorTertiaryBrush",
			};
		}

		private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			HelpSignatureText text = (HelpSignatureText)d;
			text.Inlines.Clear();
			if (e.NewValue is not IReadOnlyList<SignatureSegment> segments)
			{
				return;
			}

			foreach (SignatureSegment segment in segments)
			{
				Run run = new(segment.Text);
				run.SetResourceReference(TextElement.ForegroundProperty, GetBrushKey(segment.Kind));
				if (segment.Kind == SignatureSegmentKind.Name)
				{
					run.FontWeight = FontWeights.SemiBold;
				}

				text.Inlines.Add(run);
			}
		}
	}
}