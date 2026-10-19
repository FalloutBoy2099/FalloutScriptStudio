namespace FalloutScriptStudio.Editor
{
	using System;
	using Controllers;

	/// <summary>
	/// The controllers that belong to one text area of an editor view: the single pane of a normal view, or one of the two panes of a
	/// split view. The first pane (<see cref="IsPrimary"/>) also owns the state that is shared by both panes: the highlighting, the
	/// diagnostics, the occurrence marks and the view state of the document.
	/// </summary>
	public sealed class EditorPane
	{
		public EditorPane(
			EditorContext context,
			FoldingController folding,
			NavigationController navigation,
			BracketController brackets,
			TipController tips,
			CompletionController completion,
			InputController input,
			EditorCommandHandler commands,
			Boolean isPrimary)
		{
			this.Context = context;
			this.Folding = folding;
			this.Navigation = navigation;
			this.Brackets = brackets;
			this.Tips = tips;
			this.Completion = completion;
			this.Input = input;
			this.Commands = commands;
			this.IsPrimary = isPrimary;
		}

		/// <summary>Gets the context (the AvalonEdit control) of the pane.</summary>
		public EditorContext Context { get; }

		public FoldingController Folding { get; }

		public NavigationController Navigation { get; }

		public BracketController Brackets { get; }

		public TipController Tips { get; }

		public CompletionController Completion { get; }

		public InputController Input { get; }

		/// <summary>Gets the handler of the text commands; it acts on this pane.</summary>
		public EditorCommandHandler Commands { get; }

		/// <summary>Gets a value indicating whether this is the first pane.</summary>
		public Boolean IsPrimary { get; }
	}
}