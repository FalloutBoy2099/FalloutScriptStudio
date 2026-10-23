namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Windows;
	using System.Windows.Automation;

	/// <summary>An element of a UI Automation tree with the properties read once (see <see cref="Uia.Snapshot"/>).</summary>
	internal sealed class UiNode
	{
		/// <summary>Initializes a new instance of the <see cref="UiNode"/> class from an element whose properties were cached.</summary>
		/// <param name="element">The element.</param>
		public UiNode(AutomationElement element)
		{
			this.Element = element;
			this.Name = element.Cached.Name;
			this.Type = element.Cached.ControlType;
			this.Id = element.Cached.AutomationId;
			this.Bounds = element.Cached.BoundingRectangle;
			this.IsOffscreen = element.Cached.IsOffscreen;
			this.IsEnabled = element.Cached.IsEnabled;
			this.ClassName = element.Cached.ClassName;
			this.HelpText = element.Cached.HelpText;
		}

		/// <summary>Gets the element.</summary>
		public AutomationElement Element { get; }

		/// <summary>Gets the name.</summary>
		public String Name { get; }

		/// <summary>Gets the control type.</summary>
		public ControlType Type { get; }

		/// <summary>Gets the automation id.</summary>
		public String Id { get; }

		/// <summary>Gets the bounding rectangle in device pixels.</summary>
		public Rect Bounds { get; }

		/// <summary>Gets a value indicating whether the element is scrolled or clipped out of view.</summary>
		public Boolean IsOffscreen { get; }

		/// <summary>Gets a value indicating whether the element is enabled.</summary>
		public Boolean IsEnabled { get; }

		/// <summary>Gets the class name.</summary>
		public String ClassName { get; }

		/// <summary>Gets the help text.</summary>
		public String HelpText { get; }

		/// <summary>Gets the control type as a short text (<c>Button</c>, <c>Text</c>...).</summary>
		public String TypeName => this.Type.ProgrammaticName.Replace("ControlType.", String.Empty, StringComparison.Ordinal);

		/// <inheritdoc />
		public override String ToString()
		{
			return this.TypeName + " '" + this.Name + "'" + (this.Id.Length > 0 ? " #" + this.Id : String.Empty) + " " + (Int32)this.Bounds.X + "," + (Int32)this.Bounds.Y + " " + (Int32)this.Bounds.Width + "x" + (Int32)this.Bounds.Height;
		}
	}
}