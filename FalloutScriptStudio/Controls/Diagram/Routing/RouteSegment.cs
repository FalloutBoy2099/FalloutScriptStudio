namespace FalloutScriptStudio.Controls.Diagram.Routing
{
	using System;
	using System.Windows;

	/// <summary>
	/// One straight, axis-aligned piece of a <see cref="ConnectorRoute"/>.
	/// </summary>
	/// <param name="Direction">Direction of travel.</param>
	/// <param name="Length">Non-negative length of the piece.</param>
	public readonly record struct RouteSegment(RouteDirection Direction, Double Length)
	{
		/// <summary>
		/// Gets the point reached when travelling along this segment from <paramref name="origin"/>.
		/// </summary>
		/// <param name="origin">Start of the segment.</param>
		/// <returns>The end of the segment.</returns>
		public Point GetDestination(Point origin)
		{
			return this.Direction switch
			{
				RouteDirection.Up => new Point(origin.X, origin.Y - this.Length),
				RouteDirection.Down => new Point(origin.X, origin.Y + this.Length),
				RouteDirection.Left => new Point(origin.X - this.Length, origin.Y),
				_ => new Point(origin.X + this.Length, origin.Y)
			};
		}
	}
}
