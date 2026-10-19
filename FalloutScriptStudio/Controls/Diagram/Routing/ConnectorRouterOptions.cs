namespace FalloutScriptStudio.Controls.Diagram.Routing
{
	using System;

	/// <summary>
	/// Tuning values of the <see cref="ConnectorRouter"/>.
	/// </summary>
	public sealed record ConnectorRouterOptions
	{
		/// <summary>Gets the length of the horizontal stub used when the line has to turn around at the target's right edge.</summary>
		public Double TurnStubLength { get; init; } = 30.0;

		/// <summary>Gets the length of the short horizontal piece the line leaves its source with before it turns vertically.</summary>
		public Double ExitStubLength { get; init; } = 20.0;

		/// <summary>Gets the clearance kept between a detour channel and the rectangles it passes.</summary>
		public Double Clearance { get; init; } = 20.0;
	}
}