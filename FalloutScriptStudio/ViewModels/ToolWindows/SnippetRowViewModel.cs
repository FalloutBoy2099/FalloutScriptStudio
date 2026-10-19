namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Editor.Snippets;
	using FalloutScriptStudio.Localization;

	/// <summary>A snippet in the manager: a built-in snippet (read-only) or a snippet of the user's file or the project's file (edited in place).</summary>
	public sealed class SnippetRowViewModel : ObservableObject
	{
		private readonly SnippetManagerViewModel _owner;
		private readonly Snippet? _builtIn;
		private SnippetTriggerIssue _triggerIssue;
		private IReadOnlyList<SnippetProblem> _bodyProblems = Array.Empty<SnippetProblem>();
		private Boolean _isReplaced;

		/// <summary>Initializes the row of a snippet of a file.</summary>
		/// <param name="owner">The manager.</param>
		/// <param name="source">The file the snippet belongs to.</param>
		/// <param name="definition">The snippet as the file stores it (edited in place).</param>
		public SnippetRowViewModel(SnippetManagerViewModel owner, SnippetSource source, SnippetDefinition definition)
		{
			ArgumentNullException.ThrowIfNull(owner);
			ArgumentNullException.ThrowIfNull(definition);
			this._owner = owner;
			this.Source = source;
			this.Definition = definition;
			this._bodyProblems = SnippetTemplate.Parse(definition.Body).Problems;
		}

		/// <summary>Initializes the row of a built-in snippet.</summary>
		/// <param name="owner">The manager.</param>
		/// <param name="snippet">The snippet.</param>
		public SnippetRowViewModel(SnippetManagerViewModel owner, Snippet snippet)
		{
			ArgumentNullException.ThrowIfNull(owner);
			ArgumentNullException.ThrowIfNull(snippet);
			this._owner = owner;
			this._builtIn = snippet;
			this.Source = SnippetSource.BuiltIn;
			this.Definition = SnippetDefinition.From(snippet);
		}

		/// <summary>Gets where the snippet comes from.</summary>
		public SnippetSource Source { get; }

		/// <summary>Gets the snippet as it is stored (for a built-in snippet, a copy that is never saved).</summary>
		public SnippetDefinition Definition { get; }

		/// <summary>Gets a value indicating whether the snippet ships with the editor and cannot be changed.</summary>
		public Boolean IsBuiltIn => this.Source == SnippetSource.BuiltIn;

		/// <summary>Gets a value indicating whether the snippet can be edited.</summary>
		public Boolean IsEditable => !this.IsBuiltIn;

		/// <summary>Gets or sets the trigger word.</summary>
		public String Trigger
		{
			get => this.Definition.Trigger;
			set
			{
				if (this.IsBuiltIn || String.Equals(this.Definition.Trigger, value, StringComparison.Ordinal))
				{
					return;
				}

				this.Definition.Trigger = value ?? String.Empty;
				this.OnPropertyChanged();
				this.OnPropertyChanged(nameof(this.AccessibleName));
				this._owner.OnRowChanged();
			}
		}

		/// <summary>Gets or sets the description.</summary>
		public String Description
		{
			get => this.IsBuiltIn && this._builtIn is not null ? SnippetDescriptions.Describe(this._builtIn) : this.Definition.Description;
			set
			{
				if (this.IsBuiltIn || String.Equals(this.Definition.Description, value, StringComparison.Ordinal))
				{
					return;
				}

				this.Definition.Description = value ?? String.Empty;
				this.OnPropertyChanged();
				this.OnPropertyChanged(nameof(this.AccessibleName));
				this._owner.OnRowChanged();
			}
		}

		/// <summary>Gets or sets the body.</summary>
		public String Body
		{
			get => this.Definition.Body;
			set
			{
				if (this.IsBuiltIn || String.Equals(this.Definition.Body, value, StringComparison.Ordinal))
				{
					return;
				}

				this.Definition.Body = value ?? String.Empty;
				this.OnPropertyChanged();
				this._owner.OnRowChanged();
			}
		}

		/// <summary>Gets or sets a value indicating whether the snippet is available.</summary>
		public Boolean IsEnabled
		{
			get => this.Definition.Enabled;
			set
			{
				if (this.IsBuiltIn || this.Definition.Enabled == value)
				{
					return;
				}

				this.Definition.Enabled = value;
				this.OnPropertyChanged();
				this.OnPropertyChanged(nameof(this.StateText));
				this.OnPropertyChanged(nameof(this.AccessibleName));
				this._owner.OnRowChanged();
			}
		}

		/// <summary>Gets what is wrong with the trigger.</summary>
		public SnippetTriggerIssue TriggerIssue => this._triggerIssue;

		/// <summary>Gets the mistakes of the body.</summary>
		public IReadOnlyList<SnippetProblem> BodyProblems => this._bodyProblems;

		/// <summary>Gets a value indicating whether the trigger cannot be used (the snippets cannot be saved).</summary>
		public Boolean HasTriggerIssue => this.IsEditable && this._triggerIssue != SnippetTriggerIssue.None;

		/// <summary>Gets a value indicating whether the body has a mistake.</summary>
		public Boolean HasBodyProblems => this.IsEditable && this._bodyProblems.Count > 0;

		/// <summary>Gets the message about the trigger in the current language; empty when the trigger is fine.</summary>
		public String TriggerMessage => this.HasTriggerIssue
			? Loc.Format("Str.ToolWindows.Snippets.Issue." + this._triggerIssue, SnippetValidator.MaxTriggerLength)
			: String.Empty;

		/// <summary>Gets the message about the mistakes of the body in the current language; empty when the body is fine.</summary>
		public String BodyMessage => this.HasBodyProblems
			? String.Join(Environment.NewLine, this._bodyProblems.Select(problem => Loc.Format("Str.ToolWindows.Snippets.Problem." + problem.Kind, problem.Text)))
			: String.Empty;

		/// <summary>Gets a value indicating whether a message is shown under the editing fields.</summary>
		public Boolean HasMessage => this.HasTriggerIssue || this.HasBodyProblems;

		/// <summary>Gets the message shown under the editing fields: the trigger first, then the body.</summary>
		public String Message => this.HasTriggerIssue && this.HasBodyProblems
			? this.TriggerMessage + Environment.NewLine + this.BodyMessage
			: this.HasTriggerIssue ? this.TriggerMessage : this.BodyMessage;

		/// <summary>Gets the name of the source in the current language.</summary>
		public String SourceText => SnippetListItem.GetSourceText(this.Source);

		/// <summary>Gets the state of the snippet in the current language: disabled, or replaced by a snippet of a later source; empty otherwise.</summary>
		public String StateText => !this.IsBuiltIn && !this.Definition.Enabled
			? Loc.Get("Str.ToolWindows.Snippets.StateDisabled")
			: this._isReplaced ? Loc.Get("Str.ToolWindows.Snippets.StateReplaced") : String.Empty;

		/// <summary>Gets the name a screen reader reads for the row.</summary>
		public String AccessibleName => this.StateText.Length == 0
			? Loc.Format("Str.ToolWindows.Snippets.Item", this.Trigger, this.Description, this.SourceText)
			: Loc.Format("Str.ToolWindows.Snippets.ItemState", this.Trigger, this.Description, this.SourceText, this.StateText);

		/// <summary>Works out what is wrong with the trigger and the body again (the manager calls it after any change of the list).</summary>
		/// <param name="sameSourceTriggers">The triggers of the other rows of the same source.</param>
		/// <param name="replaced">Whether a snippet of a later source has the same trigger.</param>
		internal void Update(IEnumerable<String> sameSourceTriggers, Boolean replaced)
		{
			this._isReplaced = replaced;
			this._triggerIssue = this.IsBuiltIn ? SnippetTriggerIssue.None : SnippetValidator.CheckTrigger(this.Definition.Trigger, sameSourceTriggers);
			this._bodyProblems = this.IsBuiltIn ? Array.Empty<SnippetProblem>() : SnippetTemplate.Parse(this.Definition.Body).Problems;
			this.NotifyValidation();
		}

		/// <summary>Tells the bindings that the texts changed with the language.</summary>
		internal void RefreshTexts()
		{
			this.OnPropertyChanged(nameof(this.Description));
			this.OnPropertyChanged(nameof(this.SourceText));
			this.NotifyValidation();
		}

		private void NotifyValidation()
		{
			this.OnPropertyChanged(nameof(this.TriggerIssue));
			this.OnPropertyChanged(nameof(this.BodyProblems));
			this.OnPropertyChanged(nameof(this.HasTriggerIssue));
			this.OnPropertyChanged(nameof(this.HasBodyProblems));
			this.OnPropertyChanged(nameof(this.TriggerMessage));
			this.OnPropertyChanged(nameof(this.BodyMessage));
			this.OnPropertyChanged(nameof(this.HasMessage));
			this.OnPropertyChanged(nameof(this.Message));
			this.OnPropertyChanged(nameof(this.StateText));
			this.OnPropertyChanged(nameof(this.AccessibleName));
		}
	}
}