namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using System.Windows.Input;
	using FalloutScriptStudio.Editor;

	/// <summary>Sends text to an editor the way the keyboard does (preview text input, then text input).</summary>
	public static class TypingSimulator
	{
		/// <summary>Types characters one by one.</summary>
		/// <param name="session">The editor session.</param>
		/// <param name="text">The characters.</param>
		public static void Type(EditorSession session, String text)
		{
			Type(session.Context.TextArea, text);
		}

		/// <summary>Types characters one by one into a text area.</summary>
		/// <param name="area">The text area, for example the one of the second pane of a split view.</param>
		/// <param name="text">The characters.</param>
		public static void Type(ICSharpCode.AvalonEdit.Editing.TextArea area, String text)
		{
			foreach (Char c in text)
			{
				TextComposition composition = new TextComposition(InputManager.Current, area, c.ToString());
				TextCompositionManager.StartComposition(composition);
			}
		}
	}
}