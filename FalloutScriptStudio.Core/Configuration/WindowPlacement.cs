namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Text.Json.Serialization;

	/// <summary>
	/// The remembered position and size of a window.
	/// </summary>
	public sealed record WindowPlacement
	{
		/// <summary>Gets a value indicating whether the window was maximized.</summary>
		public Boolean IsMaximized { get; init; }

		/// <summary>Gets the left edge of the window.</summary>
		public Int32 X { get; init; }

		/// <summary>Gets the top edge of the window.</summary>
		public Int32 Y { get; init; }

		/// <summary>Gets the width of the client area.</summary>
		public Int32 Width { get; init; }

		/// <summary>Gets the height of the client area.</summary>
		public Int32 Height { get; init; }

		/// <summary>Gets a value indicating whether no size was ever stored, so the window should keep its default placement.</summary>
		[JsonIgnore]
		public Boolean IsEmpty => this.Width == 0;
	}
}