namespace FalloutScriptStudio.Controls.Diagram.Model
{
	using System;
	using System.Windows;
	using CommunityToolkit.Mvvm.ComponentModel;

	/// <summary>
	/// Base class of everything that can be placed on a diagram canvas. All coordinates are expressed in
	/// unscaled "world" units (device independent pixels at 100 % zoom).
	/// </summary>
	public abstract class DiagramItem : ObservableObject
	{
		private Double _x;
		private Double _y;
		private Double _width;
		private Double _height;
		private Boolean _isHidden;
		private Boolean _isSelected;
		private Object? _tag;

		/// <summary>Gets or sets arbitrary caller data.</summary>
		public Object? Tag
		{
			get => this._tag;
			set => this.SetProperty(ref this._tag, value);
		}

		/// <summary>Gets or sets the left edge of the item.</summary>
		public Double X
		{
			get => this._x;
			set => this.SetProperty(ref this._x, Sanitize(value));
		}

		/// <summary>Gets or sets the top edge of the item.</summary>
		public Double Y
		{
			get => this._y;
			set => this.SetProperty(ref this._y, Sanitize(value));
		}

		/// <summary>
		/// Gets or sets the width of the item. A value of zero (or less) means "automatic" for nodes: the canvas
		/// picks a width from the content when the node is added. Values below <see cref="MinimumWidth"/> are raised.
		/// </summary>
		public Double Width
		{
			get => this._width;
			set
			{
				Double v = Sanitize(value);
				this.SetProperty(ref this._width, v <= 0.0 ? 0.0 : Math.Max(v, this.MinimumWidth));
			}
		}

		/// <summary>
		/// Gets or sets the height of the item. For items with <see cref="HasAutomaticHeight"/> the height is computed
		/// by the canvas (it is valid once the item has been added to a canvas) and assignments are ignored.
		/// </summary>
		public Double Height
		{
			get => this._height;
			set
			{
				if (this.HasAutomaticHeight)
				{
					return;
				}

				this.SetProperty(ref this._height, Math.Max(Sanitize(value), this.MinimumHeight));
			}
		}

		/// <summary>Gets the bounding rectangle in world coordinates.</summary>
		public Rect Bounds => new(this._x, this._y, this._width, this._height);

		/// <summary>Gets or sets a value indicating whether the item is excluded from rendering, hit testing and routing.</summary>
		public Boolean IsHidden
		{
			get => this._isHidden;
			set => this.SetProperty(ref this._isHidden, value);
		}

		/// <summary>Gets a value indicating whether the item is part of the current canvas selection.</summary>
		public Boolean IsSelected => this._isSelected;

		/// <summary>Gets the smallest width the item can have.</summary>
		public abstract Double MinimumWidth { get; }

		/// <summary>Gets the smallest height the item can have.</summary>
		public virtual Double MinimumHeight => 0.0;

		/// <summary>Gets a value indicating whether the height is computed from the content.</summary>
		public virtual Boolean HasAutomaticHeight => false;

		/// <summary>Gets a value indicating whether the user may resize the item vertically.</summary>
		public virtual Boolean CanResizeVertically => false;

		/// <summary>Gets a value indicating whether the user may resize the item horizontally.</summary>
		public virtual Boolean CanResizeHorizontally => true;

		internal void SetMeasuredHeight(Double height)
		{
			this.SetProperty(ref this._height, Sanitize(height), nameof(this.Height));
		}

		internal void SetSelected(Boolean value)
		{
			this.SetProperty(ref this._isSelected, value, nameof(this.IsSelected));
		}

		private static Double Sanitize(Double value)
		{
			return Double.IsFinite(value) ? value : 0.0;
		}
	}
}