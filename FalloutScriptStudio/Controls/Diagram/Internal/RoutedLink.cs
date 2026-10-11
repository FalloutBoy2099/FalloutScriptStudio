namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Routing;

	/// <summary>
	/// A link together with its currently computed route.
	/// </summary>
	internal sealed class RoutedLink
	{
		public RoutedLink(DiagramLink link, ConnectorRoute route, Boolean drawStartMarker)
		{
			this.Link = link;
			this.Route = route;
			this.DrawStartMarker = drawStartMarker;
		}

		public DiagramLink Link { get; }

		public ConnectorRoute Route { get; }

		/// <summary>Gets a value indicating whether the start of the line is attached to a visible row (and not to a collapsed header).</summary>
		public Boolean DrawStartMarker { get; }
	}
}
