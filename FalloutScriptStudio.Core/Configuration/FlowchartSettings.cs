namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// Options of the dialog flowchart.
	/// </summary>
	public sealed class FlowchartSettings
	{
		/// <summary>Gets or sets a value indicating whether the chart is updated automatically when the script changes.</summary>
		public Boolean AutoUpdate { get; set; }

		/// <summary>Gets or sets a value indicating whether the chart layout is saved automatically.</summary>
		public Boolean AutoSaveChart { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether nodes are arranged automatically.</summary>
		public Boolean AutoArrange { get; set; }

		/// <summary>Gets or sets a value indicating whether exit nodes are left out of the chart.</summary>
		public Boolean WithoutExitNode { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether nodes are hidden automatically.</summary>
		public Boolean AutoHideNodes { get; set; }
	}
}
