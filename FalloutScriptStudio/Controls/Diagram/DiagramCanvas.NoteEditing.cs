namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Model;
	using Rendering;

	// In-place editing of note texts.
	public sealed partial class DiagramCanvas
	{
		private DiagramNote? _editingNote;
		private TextBox? _noteEditor;

		internal void BeginEditNote(DiagramNote note)
		{
			if (ReferenceEquals(note, this._editingNote))
			{
				return;
			}

			this.CommitNoteEdit();
			DiagramPalette palette = this._scene.Context.Palette;
			this._editingNote = note;
			note.SetEditing(true);
			TextBox editor = new()
			{
				Text = note.Text,
				AcceptsReturn = true,
				TextWrapping = TextWrapping.Wrap,
				BorderThickness = new Thickness(0),
				Padding = new Thickness(0),
				Background = palette.NoteFill,
				Foreground = palette.NoteText,
				CaretBrush = palette.NoteText,
				FontFamily = this.FontFamily,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};
			editor.KeyDown += this.OnNoteEditorKeyDown;
			editor.LostKeyboardFocus += (s, e) => this.CommitNoteEdit();
			this._noteEditor = editor;
			this._editorLayer.Children.Add(editor);
			this.PositionNoteEditor();
			editor.Focus();
			editor.CaretIndex = editor.Text.Length;
		}

		internal void CommitNoteEdit()
		{
			this.EndNoteEdit(true);
		}

		internal void CancelNoteEdit()
		{
			this.EndNoteEdit(false);
		}

		private void PositionNoteEditor()
		{
			if (this._noteEditor == null || this._editingNote == null)
			{
				return;
			}

			DiagramNote note = this._editingNote;
			Double zoom = this.Zoom;
			Double pad = DiagramMetrics.NotePadding;
			Point origin = this._surface.WorldToSurface(new Point(note.X + pad, note.Y + pad));
			Canvas.SetLeft(this._noteEditor, origin.X);
			Canvas.SetTop(this._noteEditor, origin.Y);
			this._noteEditor.Width = Math.Max(10.0, (note.Width - (2.0 * pad)) * zoom);
			this._noteEditor.Height = Math.Max(10.0, (note.Height - (2.0 * pad)) * zoom);
			this._noteEditor.FontSize = DiagramMetrics.NoteFontSize * zoom;
		}

		private void OnNoteEditorKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				this.CancelNoteEdit();
				this.Focus();
				e.Handled = true;
			}
			else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
			{
				this.CommitNoteEdit();
				this.Focus();
				e.Handled = true;
			}
		}

		private void EndNoteEdit(Boolean commit)
		{
			TextBox? editor = this._noteEditor;
			DiagramNote? note = this._editingNote;
			if (editor == null || note == null)
			{
				return;
			}

			this._noteEditor = null;
			this._editingNote = null;
			if (commit)
			{
				note.Text = editor.Text;
			}

			this._editorLayer.Children.Remove(editor);
			note.SetEditing(false);
		}
	}
}