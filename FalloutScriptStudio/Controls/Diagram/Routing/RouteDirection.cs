namespace FalloutScriptStudio.Controls.Diagram.Routing
{
	/// <summary>
	/// Direction of one straight piece of a connector line (screen orientation: Y grows downwards).
	/// </summary>
	public enum RouteDirection
	{
		/// <summary>Towards decreasing Y.</summary>
		Up,

		/// <summary>Towards increasing X.</summary>
		Right,

		/// <summary>Towards increasing Y.</summary>
		Down,

		/// <summary>Towards decreasing X.</summary>
		Left
	}
}