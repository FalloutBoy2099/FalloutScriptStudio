namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Windows.Media;
	using Model;

	/// <summary>
	/// Frozen vector glyphs (12 x 12 units) for <see cref="DiagramRowIcon"/>.
	/// </summary>
	internal static class DiagramIcons
	{
		private static readonly Geometry ReplyGeometry = Create("M1,1.5 H11 V8 H6.5 L3.5,11 V8 H1 Z");
		private static readonly Geometry OptionGeometry = Create("M2.5,1 L10.5,6 L2.5,11 Z");
		private static readonly Geometry CallGeometry = Create("M6,1 L11,6 L6,11 V8 H1.5 V4 H6 Z");
		private static readonly Geometry ArrowGeometry = Create("M1,5 H7 V2 L11,6 L7,10 V7 H1 Z");

		/// <summary>
		/// Gets the glyph for an icon, or <c>null</c> for <see cref="DiagramRowIcon.None"/>.
		/// </summary>
		public static Geometry? Get(DiagramRowIcon icon)
		{
			return icon switch
			{
				DiagramRowIcon.Reply => ReplyGeometry,
				DiagramRowIcon.Option => OptionGeometry,
				DiagramRowIcon.Call => CallGeometry,
				DiagramRowIcon.Arrow => ArrowGeometry,
				_ => null
			};
		}

		private static Geometry Create(String data)
		{
			Geometry geometry = Geometry.Parse(data);
			geometry.Freeze();
			return geometry;
		}
	}
}