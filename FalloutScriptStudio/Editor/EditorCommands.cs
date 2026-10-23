namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Windows.Input;
	using Documents;
	using Localization;
	using Views.Editor;

	/// <summary>
	/// The routed commands of the script editor. Every <see cref="EditorCommand"/> has one; menus and toolbars can bind to
	/// them (the command target is the focused editor) as an alternative to calling
	/// <see cref="IEditorSurface.Execute"/>. The legacy shortcuts are attached as default gestures.
	/// </summary>
	public static class EditorCommands
	{
		private static readonly List<(RoutedUICommand Command, String Key)> _texts = [];

		private static readonly Dictionary<EditorCommand, RoutedUICommand> _byCommand = new()
		{
			[EditorCommand.CommentSelection] = Create("Str.Editor.Cmd.CommentSelection", nameof(EditorCommand.CommentSelection), new KeyGesture(Key.C, ModifierKeys.Alt)),
			[EditorCommand.UncommentSelection] = Create("Str.Editor.Cmd.UncommentSelection", nameof(EditorCommand.UncommentSelection), new KeyGesture(Key.X, ModifierKeys.Alt)),
			[EditorCommand.ToggleLineComment] = Create("Str.Editor.Cmd.ToggleLineComment", nameof(EditorCommand.ToggleLineComment), new KeyGesture(Key.Divide, ModifierKeys.Control), new KeyGesture(Key.OemQuestion, ModifierKeys.Control)),
			[EditorCommand.ToggleBlockComment] = Create("Str.Editor.Cmd.ToggleBlockComment", nameof(EditorCommand.ToggleBlockComment), new KeyGesture(Key.Multiply, ModifierKeys.Control)),
			[EditorCommand.AlignSelectionLeft] = Create("Str.Editor.Cmd.AlignSelectionLeft", nameof(EditorCommand.AlignSelectionLeft), new KeyGesture(Key.A, ModifierKeys.Alt)),
			[EditorCommand.DecreaseIndent] = Create("Str.Editor.Cmd.DecreaseIndent", nameof(EditorCommand.DecreaseIndent), new KeyGesture(Key.Q, ModifierKeys.Alt)),
			[EditorCommand.IncreaseIndent] = Create("Str.Editor.Cmd.IncreaseIndent", nameof(EditorCommand.IncreaseIndent)),
			[EditorCommand.FormatDocument] = Create("Str.Editor.Cmd.FormatDocument", nameof(EditorCommand.FormatDocument)),
			[EditorCommand.FormatSelection] = Create("Str.Editor.Cmd.FormatSelection", nameof(EditorCommand.FormatSelection), new KeyGesture(Key.F, ModifierKeys.Alt)),
			[EditorCommand.CapitalizeWord] = Create("Str.Editor.Cmd.CapitalizeWord", nameof(EditorCommand.CapitalizeWord), new KeyGesture(Key.W, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.UpperCase] = Create("Str.Editor.Cmd.UpperCase", nameof(EditorCommand.UpperCase), new KeyGesture(Key.A, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.LowerCase] = Create("Str.Editor.Cmd.LowerCase", nameof(EditorCommand.LowerCase), new KeyGesture(Key.Z, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.ConvertLeadingTabsToSpaces] = Create("Str.Editor.Cmd.ConvertLeadingTabsToSpaces", nameof(EditorCommand.ConvertLeadingTabsToSpaces)),
			[EditorCommand.ConvertAllTabsToSpaces] = Create("Str.Editor.Cmd.ConvertAllTabsToSpaces", nameof(EditorCommand.ConvertAllTabsToSpaces)),
			[EditorCommand.ConvertLeadingSpacesToTabs] = Create("Str.Editor.Cmd.ConvertLeadingSpacesToTabs", nameof(EditorCommand.ConvertLeadingSpacesToTabs)),
			[EditorCommand.ConvertAllSpacesToTabs] = Create("Str.Editor.Cmd.ConvertAllSpacesToTabs", nameof(EditorCommand.ConvertAllSpacesToTabs)),
			[EditorCommand.TrimTrailingSpaces] = Create("Str.Editor.Cmd.TrimTrailingSpaces", nameof(EditorCommand.TrimTrailingSpaces)),
			[EditorCommand.ConvertHexDec] = Create("Str.Editor.Cmd.ConvertHexDec", nameof(EditorCommand.ConvertHexDec)),
			[EditorCommand.ToggleOutline] = Create("Str.Editor.Cmd.ToggleOutline", nameof(EditorCommand.ToggleOutline)),
			[EditorCommand.CollapseAll] = Create("Str.Editor.Cmd.CollapseAll", nameof(EditorCommand.CollapseAll)),
			[EditorCommand.ExpandAll] = Create("Str.Editor.Cmd.ExpandAll", nameof(EditorCommand.ExpandAll)),
			[EditorCommand.HighlightWord] = Create("Str.Editor.Cmd.HighlightWord", nameof(EditorCommand.HighlightWord)),
			[EditorCommand.GoToDeclaration] = Create("Str.Editor.Cmd.GoToDeclaration", nameof(EditorCommand.GoToDeclaration), new KeyGesture(Key.F12, ModifierKeys.Control)),
			[EditorCommand.GoToDefinition] = Create("Str.Editor.Cmd.GoToDefinition", nameof(EditorCommand.GoToDefinition), new KeyGesture(Key.F12)),
			[EditorCommand.FindReferences] = Create("Str.Editor.Cmd.FindReferences", nameof(EditorCommand.FindReferences), new KeyGesture(Key.F12, ModifierKeys.Alt)),
			[EditorCommand.RenameSymbol] = Create("Str.Editor.Cmd.RenameSymbol", nameof(EditorCommand.RenameSymbol), new KeyGesture(Key.F2)),
			[EditorCommand.OpenInclude] = Create("Str.Editor.Cmd.OpenInclude", nameof(EditorCommand.OpenInclude), new KeyGesture(Key.G, ModifierKeys.Alt)),
			[EditorCommand.ShowCompletion] = Create("Str.Editor.Cmd.ShowCompletion", nameof(EditorCommand.ShowCompletion), new KeyGesture(Key.Space, ModifierKeys.Control), new KeyGesture(Key.Space, ModifierKeys.Alt)),
			[EditorCommand.GoToProcedureBegin] = Create("Str.Editor.Cmd.GoToProcedureBegin", nameof(EditorCommand.GoToProcedureBegin)),
			[EditorCommand.DeleteLine] = Create("Str.Editor.Cmd.DeleteLine", nameof(EditorCommand.DeleteLine)),
			[EditorCommand.SplitDocument] = Create("Str.Editor.Cmd.SplitDocument", nameof(EditorCommand.SplitDocument), new KeyGesture(Key.S, ModifierKeys.Alt)),
			[EditorCommand.SelectWord] = Create("Str.Editor.Cmd.SelectWord", nameof(EditorCommand.SelectWord)),
			[EditorCommand.DeleteToLineEnd] = Create("Str.Editor.Cmd.DeleteToLineEnd", nameof(EditorCommand.DeleteToLineEnd), new KeyGesture(Key.D, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.GoToMatchingBracket] = Create("Str.Editor.Cmd.GoToMatchingBracket", nameof(EditorCommand.GoToMatchingBracket), new KeyGesture(Key.B, ModifierKeys.Control)),
			[EditorCommand.ExtractProcedure] = Create("Str.Editor.Cmd.ExtractProcedure", nameof(EditorCommand.ExtractProcedure), new KeyGesture(Key.E, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.DeleteUnusedProcedure] = Create("Str.Editor.Cmd.DeleteUnusedProcedure", nameof(EditorCommand.DeleteUnusedProcedure)),
			[EditorCommand.FindUnusedVariables] = Create("Str.Editor.Cmd.FindUnusedVariables", nameof(EditorCommand.FindUnusedVariables)),
			[EditorCommand.RemoveUnusedVariable] = Create("Str.Editor.Cmd.RemoveUnusedVariable", nameof(EditorCommand.RemoveUnusedVariable)),
			[EditorCommand.QuickFix] = Create("Str.Editor.Cmd.QuickFix", nameof(EditorCommand.QuickFix), new KeyGesture(Key.OemPeriod, ModifierKeys.Control)),
			[EditorCommand.PreviewDialogueFromNode] = Create("Str.Editor.Cmd.PreviewDialogueFromNode", nameof(EditorCommand.PreviewDialogueFromNode), new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.InsertSnippet] = Create("Str.Editor.Cmd.InsertSnippet", nameof(EditorCommand.InsertSnippet), new KeyGesture(Key.J, ModifierKeys.Control)),
			[EditorCommand.SurroundWith] = Create("Str.Editor.Cmd.SurroundWith", nameof(EditorCommand.SurroundWith), new KeyGesture(Key.J, ModifierKeys.Control | ModifierKeys.Shift)),
			[EditorCommand.ShowHelp] = Create("Str.Editor.Cmd.ShowHelp", nameof(EditorCommand.ShowHelp), new KeyGesture(Key.F1, ModifierKeys.Control)),
		};

		static EditorCommands()
		{
			_ = LanguageChangedSubscription.SubscribeForever(RefreshTexts);
		}

		/// <summary>Gets the command that navigates to the previous caret position.</summary>
		public static RoutedUICommand NavigateBack { get; } = Create("Str.Editor.Cmd.NavigateBack", nameof(NavigateBack), new KeyGesture(Key.Left, ModifierKeys.Alt));

		/// <summary>Gets the command that navigates to the next caret position.</summary>
		public static RoutedUICommand NavigateForward { get; } = Create("Str.Editor.Cmd.NavigateForward", nameof(NavigateForward), new KeyGesture(Key.Right, ModifierKeys.Alt));

		/// <summary>Gets the routed command of a text command.</summary>
		/// <param name="command">The text command.</param>
		/// <returns>The routed command.</returns>
		public static RoutedUICommand Get(EditorCommand command)
		{
			return _byCommand[command];
		}

		/// <summary>Gets all text commands that have a routed command.</summary>
		public static IEnumerable<KeyValuePair<EditorCommand, RoutedUICommand>> All => _byCommand;

		/// <summary>Gets the key of the localized text of a command, for menus that follow a language change with <c>SetResourceReference</c>.</summary>
		/// <param name="command">The routed command.</param>
		/// <returns>The resource key, or <see langword="null"/> when the command is not one of the editor.</returns>
		public static String? GetTextKey(RoutedUICommand command)
		{
			foreach ((RoutedUICommand candidate, String key) in _texts)
			{
				if (candidate == command)
				{
					return key;
				}
			}

			return null;
		}

		private static void RefreshTexts()
		{
			foreach ((RoutedUICommand command, String key) in _texts)
			{
				command.Text = Loc.Get(key);
			}
		}

		private static RoutedUICommand Create(String textKey, String name, params InputGesture[] gestures)
		{
			InputGestureCollection collection = [.. gestures];

			RoutedUICommand command = new(Loc.Get(textKey), name, typeof(ScriptEditorView), collection);
			_texts.Add((command, textKey));
			return command;
		}
	}
}