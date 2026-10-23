namespace FalloutScriptStudio.Core.Help
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The documentation of one function, opcode, macro or procedure: its signature (with the parts to color), the description of the
	/// description files or the comment of the source, the categories of the functions tree, the arguments of a hook and the code the
	/// functions tree inserts. The descriptions stay in English: they come from <c>opcodes.txt</c> and <c>functions.ini</c>.
	/// </summary>
	public sealed class HelpTopic
	{
		/// <summary>Initializes a new instance of the <see cref="HelpTopic"/> class.</summary>
		/// <param name="name">The name.</param>
		/// <param name="kind">What the topic describes.</param>
		/// <param name="signature">The pieces of the signature.</param>
		public HelpTopic(String name, HelpTopicKind kind, IReadOnlyList<SignatureSegment> signature)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(signature);
			this.Name = name;
			this.Kind = kind;
			this.Signature = signature;
			this.SignatureText = FunctionSignatureFormatter.ToText(signature);
		}

		/// <summary>Gets the name.</summary>
		public String Name { get; }

		/// <summary>Gets what the topic describes.</summary>
		public HelpTopicKind Kind { get; }

		/// <summary>Gets the pieces of the signature, in order (the name alone when no signature is known).</summary>
		public IReadOnlyList<SignatureSegment> Signature { get; }

		/// <summary>Gets the signature as one line of text.</summary>
		public String SignatureText { get; }

		/// <summary>Gets the one-line description (the <c>&lt;d&gt;</c> text of the functions tree, or the first line of the description).</summary>
		public String Summary { get; init; } = String.Empty;

		/// <summary>Gets the paragraphs of the description; the lines of a paragraph are separated by line breaks.</summary>
		public IReadOnlyList<String> Description { get; init; } = Array.Empty<String>();

		/// <summary>Gets the lines that describe the arguments and the results of a hook (<c>int arg1 - ...</c>, <c>int ret1 - ...</c>).</summary>
		public IReadOnlyList<String> Arguments { get; init; } = Array.Empty<String>();

		/// <summary>Gets the places of the functions tree the topic is in, each as <c>Category &gt; Sub category</c>.</summary>
		public IReadOnlyList<String> Categories { get; init; } = Array.Empty<String>();

		/// <summary>Gets the code the functions tree inserts, with <c>&lt;cr&gt;</c> for line breaks, or <see langword="null"/> when the topic is not in the tree.</summary>
		public String? InsertCode { get; init; }

		/// <summary>Gets a value indicating whether the topic belongs to sfall (it is documented on the sfall site).</summary>
		public Boolean IsSfall { get; init; }

		/// <summary>Gets the page of the sfall documentation that describes the topic (for example <c>objects-and-scripts</c>), or <see langword="null"/> when it is not known.</summary>
		public String? DocumentationPage { get; init; }

		/// <summary>Gets the definition of a macro or a variable as the source writes it (<c>#define MAX(a, b) ...</c>), or <see langword="null"/>.</summary>
		public String? Definition { get; init; }

		/// <summary>Gets the file the topic is declared in (a macro, a procedure), or <see langword="null"/>.</summary>
		public String? File { get; init; }

		/// <summary>Gets the 1-based line the topic is declared on, or -1.</summary>
		public Int32 Line { get; init; } = -1;

		/// <summary>Gets a value indicating whether the topic has a description, arguments or a definition to show.</summary>
		public Boolean IsDocumented => this.Summary.Length > 0 || this.Description.Count > 0 || this.Arguments.Count > 0 || !String.IsNullOrEmpty(this.Definition);

		/// <summary>Gets a value indicating whether the sfall documentation can be opened for the topic.</summary>
		public Boolean HasDocumentation => this.Kind is HelpTopicKind.Function or HelpTopicKind.Handler or HelpTopicKind.Define;

		/// <summary>Gets a value indicating whether the topic can be inserted into a script (it is in the functions tree).</summary>
		public Boolean CanInsert => !String.IsNullOrEmpty(this.InsertCode);

		/// <inheritdoc />
		public override String ToString()
		{
			return this.Kind + " " + this.SignatureText;
		}
	}
}