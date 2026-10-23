namespace FalloutScriptStudio.ViewModels.Help
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Help;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// What the help shows for a topic (the editor popup and the Help tab use the same view): the colored signature, the description, the
	/// arguments of a hook, the places in the functions tree, and the commands to open the sfall documentation, copy the signature and
	/// insert the call. The descriptions stay in English; the labels around them are read through <see cref="Loc"/> when asked.
	/// </summary>
	public sealed class HelpTopicViewModel
	{
		/// <summary>Initializes the view model.</summary>
		/// <param name="topic">The topic.</param>
		/// <param name="openDocumentation">Opens the documentation of the topic; the command is disabled without it.</param>
		/// <param name="copySignature">Copies the signature; the command is disabled without it.</param>
		/// <param name="insertCall">Inserts the call into the script; the command is disabled without it.</param>
		public HelpTopicViewModel(HelpTopic topic, Action<HelpTopic>? openDocumentation = null, Action<HelpTopic>? copySignature = null, Action<HelpTopic>? insertCall = null)
		{
			ArgumentNullException.ThrowIfNull(topic);
			this.Topic = topic;
			this.OpenDocumentationCommand = new RelayCommand(() => openDocumentation?.Invoke(topic), () => openDocumentation is not null && topic.HasDocumentation);
			this.CopySignatureCommand = new RelayCommand(() => copySignature?.Invoke(topic), () => copySignature is not null);
			this.InsertCallCommand = new RelayCommand(() => insertCall?.Invoke(topic), () => insertCall is not null && topic.CanInsert);
		}

		/// <summary>Gets the topic.</summary>
		public HelpTopic Topic { get; }

		/// <summary>Gets the name.</summary>
		public String Name => this.Topic.Name;

		/// <summary>Gets the pieces of the signature, to color.</summary>
		public IReadOnlyList<SignatureSegment> Signature => this.Topic.Signature;

		/// <summary>Gets the signature as one line (the automation name and what is copied).</summary>
		public String SignatureText => this.Topic.SignatureText;

		/// <summary>Gets the kind of the topic in the current language ("Function", "Macro"...).</summary>
		public String KindText => Loc.Get("Str.Panels.HelpKind." + this.Topic.Kind);

		/// <summary>Gets a value indicating whether the one-line summary is shown (it adds to the description instead of repeating it).</summary>
		public Boolean ShowSummary => this.Topic.Summary.Length > 0 && this.Topic.Description.Count > 0 && !this.Topic.Description[0].StartsWith(this.Topic.Summary, StringComparison.Ordinal);

		/// <summary>Gets the one-line summary.</summary>
		public String Summary => this.Topic.Summary;

		/// <summary>Gets the paragraphs of the description (the summary alone when the topic has no description).</summary>
		public IReadOnlyList<String> Paragraphs => this.Topic.Description.Count > 0 ? this.Topic.Description : this.Topic.Summary.Length > 0 ? [this.Topic.Summary] : Array.Empty<String>();

		/// <summary>Gets a value indicating whether the topic has a description to show.</summary>
		public Boolean HasParagraphs => this.Paragraphs.Count > 0;

		/// <summary>Gets the lines about the arguments and results of a hook.</summary>
		public IReadOnlyList<String> Arguments => this.Topic.Arguments;

		/// <summary>Gets a value indicating whether the topic has argument lines.</summary>
		public Boolean HasArguments => this.Topic.Arguments.Count > 0;

		/// <summary>Gets the places of the functions tree, one per line.</summary>
		public String Categories => String.Join(Environment.NewLine, this.Topic.Categories);

		/// <summary>Gets a value indicating whether the topic is in the functions tree.</summary>
		public Boolean HasCategories => this.Topic.Categories.Count > 0;

		/// <summary>Gets the definition of a macro or a variable as written in the source.</summary>
		public String Definition => this.Topic.Definition ?? String.Empty;

		/// <summary>Gets a value indicating whether the topic has a definition to show.</summary>
		public Boolean HasDefinition => !String.IsNullOrEmpty(this.Topic.Definition);

		/// <summary>Gets where a macro, a procedure or a variable of the project is declared (<c>file.ssl:12</c>), or an empty text.</summary>
		public String Source => String.IsNullOrEmpty(this.Topic.File) ? String.Empty : Path.GetFileName(this.Topic.File) + (this.Topic.Line > 0 ? ":" + this.Topic.Line.ToString(CultureInfo.InvariantCulture) : String.Empty);

		/// <summary>Gets the place of the declaration as a sentence in the current language ("Declared in: a.ssl:12").</summary>
		public String SourceText => this.HasSource ? Loc.Format("Str.Panels.DeclaredIn", this.Source) : String.Empty;

		/// <summary>Gets a value indicating whether the topic is declared in the project.</summary>
		public Boolean HasSource => this.Source.Length > 0;

		/// <summary>Gets a value indicating whether the topic has a comment, a description or a definition at all.</summary>
		public Boolean IsDocumented => this.Topic.IsDocumented;

		/// <summary>Gets a value indicating whether the sfall documentation can be opened.</summary>
		public Boolean CanOpenDocumentation => this.OpenDocumentationCommand.CanExecute(null);

		/// <summary>Gets a value indicating whether the call can be inserted into the script.</summary>
		public Boolean CanInsert => this.InsertCallCommand.CanExecute(null);

		/// <summary>Gets the command that opens the page of the sfall documentation.</summary>
		public RelayCommand OpenDocumentationCommand { get; }

		/// <summary>Gets the command that copies the signature to the clipboard.</summary>
		public RelayCommand CopySignatureCommand { get; }

		/// <summary>Gets the command that inserts the call into the active script.</summary>
		public RelayCommand InsertCallCommand { get; }

		/// <summary>Gets the signature, the description and the arguments as plain text (the automation help text and what a screen reader reads).</summary>
		public String PlainText => String.Join(Environment.NewLine, new[] { this.SignatureText }.Concat(this.Paragraphs).Concat(this.Arguments));
	}
}