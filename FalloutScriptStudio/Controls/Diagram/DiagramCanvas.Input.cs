namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Model;

	// Keyboard handling and the callbacks used by the mouse input handler.
	public sealed partial class DiagramCanvas
	{
		private const Double ItemSnapMinimum = 0.0;

		private Point _lastClickPosition;

		internal Double Snap(Double value)
		{
			Double grid = this.SnapGridSize;
			Double snapped = grid > 0.0 ? Math.Round(value / grid) * grid : value;
			return Math.Max(snapped, ItemSnapMinimum);
		}

		internal void SetLastClickPosition(Point world)
		{
			this._lastClickPosition = world;
		}

		internal void RaiseItemHover(DiagramHitTestResult hit)
		{
			this.ItemHover?.Invoke(this, new DiagramItemEventArgs(hit.Item, hit.Row));
		}

		internal void RaiseRowClicked(DiagramRowEventArgs e)
		{
			this.RowClicked?.Invoke(this, e);
		}

		internal void RaiseItemDoubleClicked(DiagramItem item)
		{
			this.ItemDoubleClicked?.Invoke(this, new DiagramItemEventArgs(item));
		}

		internal void RaiseNodeCodeToggled(DiagramNode node)
		{
			this.NodeCodeToggled?.Invoke(this, new DiagramItemEventArgs(node));
		}

		/// <inheritdoc />
		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.Handled || e.OriginalSource is TextBox)
			{
				return;
			}

			ModifierKeys modifiers = Keyboard.Modifiers;
			if (modifiers == ModifierKeys.Control)
			{
				switch (e.Key)
				{
					case Key.A:
						this.SelectAll();
						e.Handled = true;
						break;
					case Key.D0:
					case Key.NumPad0:
						this.ResetZoom();
						e.Handled = true;
						break;
					case Key.OemPlus:
					case Key.Add:
						this.ZoomIn();
						e.Handled = true;
						break;
					case Key.OemMinus:
					case Key.Subtract:
						this.ZoomOut();
						e.Handled = true;
						break;
					default:
						break;
				}

				return;
			}

			if (e.Key == Key.Escape)
			{
				this.ClearSelection();
				e.Handled = true;
			}
			else if (e.Key == Key.Delete && this._scene.Selection.Count > 0)
			{
				this.DeleteRequested?.Invoke(this, EventArgs.Empty);
				e.Handled = true;
			}
		}
	}
}