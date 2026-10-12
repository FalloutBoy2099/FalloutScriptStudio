namespace FalloutScriptStudio.Problems
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// The one model of everything that can go wrong in the editor: a parser or compiler message, a missing tool, a file that cannot be
	/// saved, a damaged settings file... The text is either a localization key with arguments (so it follows a language change) or a text
	/// already formatted by an external tool.
	/// </summary>
	public sealed record Problem
	{
		private static readonly IReadOnlyList<Object?> NoArguments = Array.Empty<Object?>();

		/// <summary>Gets the severity.</summary>
		public required ProblemSeverity Severity { get; init; }

		/// <summary>Gets where the problem comes from.</summary>
		public required ProblemSource Source { get; init; }

		/// <summary>Gets the localization key of the message, or <see langword="null"/> when <see cref="Text"/> is used.</summary>
		public String? MessageKey { get; init; }

		/// <summary>Gets the arguments of the message.</summary>
		public IReadOnlyList<Object?> MessageArguments { get; init; } = NoArguments;

		/// <summary>Gets the message as a tool wrote it (used when there is no <see cref="MessageKey"/>).</summary>
		public String? Text { get; init; }

		/// <summary>Gets the file the problem points to, or <see langword="null"/>.</summary>
		public String? File { get; init; }

		/// <summary>Gets the 1-based line, or -1.</summary>
		public Int32 Line { get; init; } = -1;

		/// <summary>Gets the 1-based column, or -1.</summary>
		public Int32 Column { get; init; } = -1;

		/// <summary>Gets the length of the affected text, or -1.</summary>
		public Int32 Length { get; init; } = -1;

		/// <summary>Gets the localization key of the fix hint ("what to do"), or <see langword="null"/>.</summary>
		public String? HintKey { get; init; }

		/// <summary>Gets the arguments of the hint.</summary>
		public IReadOnlyList<Object?> HintArguments { get; init; } = NoArguments;

		/// <summary>Gets the obvious fix, or <see langword="null"/>.</summary>
		public ProblemAction? Action { get; init; }

		/// <summary>Gets the object the problem belongs to (the document): the problem is dropped when it is re-parsed, recompiled or closed.</summary>
		public Object? Owner { get; init; }

		/// <summary>Gets the name of the producer that replaces or removes the problem when its situation changes, or <see langword="null"/>.</summary>
		public String? Origin { get; init; }

		/// <summary>Gets the kind of log entry this problem was made from, so that its icon stays the same as before.</summary>
		public BuildErrorType? LegacyType { get; init; }

		/// <summary>Gets the message in the current language.</summary>
		public String Message => this.MessageKey is not null ? Loc.Format(this.MessageKey, ToArray(this.MessageArguments)) : this.Text ?? String.Empty;

		/// <summary>Gets the hint in the current language, or <see langword="null"/>.</summary>
		public String? Hint => this.HintKey is not null ? Loc.Format(this.HintKey, ToArray(this.HintArguments)) : null;

		/// <summary>Gets a value identifying the problem: reporting an equal problem again replaces the first one instead of listing it twice.</summary>
		public String Identity => String.Join(
			'\u001f',
			(Int32)this.Source,
			(Int32)this.Severity,
			this.MessageKey ?? this.Text,
			String.Join('\u001e', this.MessageArguments),
			this.File,
			this.Line.ToString(CultureInfo.InvariantCulture),
			this.Column.ToString(CultureInfo.InvariantCulture));

		/// <summary>Creates the problem for an entry of a build log or a parser log.</summary>
		/// <param name="error">The entry.</param>
		/// <param name="source">Where it comes from.</param>
		/// <param name="owner">The document it belongs to, if any.</param>
		/// <returns>The problem.</returns>
		public static Problem FromBuildError(BuildError error, ProblemSource source, Object? owner = null)
		{
			ArgumentNullException.ThrowIfNull(error);
			Boolean hasFile = !String.IsNullOrEmpty(error.FileName) && !String.Equals(error.FileName, BuildError.NoFile, StringComparison.OrdinalIgnoreCase);
			return new Problem
			{
				Severity = error.Type switch
				{
					BuildErrorType.Error => ProblemSeverity.Error,
					BuildErrorType.Warning => ProblemSeverity.Warning,
					_ => ProblemSeverity.Info,
				},
				Source = source,
				Text = error.Message,
				File = hasFile ? error.FileName : null,
				Line = error.Line,
				Column = error.Column,
				Length = error.Length,
				Owner = owner,
				LegacyType = error.Type,
			};
		}

		/// <summary>Converts the problem to the entry type of the error list, with the message in the current language.</summary>
		/// <returns>The entry.</returns>
		public BuildError ToBuildError()
		{
			BuildErrorType type = this.LegacyType ?? this.Severity switch
			{
				ProblemSeverity.Error => BuildErrorType.Error,
				ProblemSeverity.Warning => BuildErrorType.Warning,
				_ => BuildErrorType.Message,
			};
			return new BuildError(type, this.Message, this.File ?? BuildError.NoFile, this.Line, this.Column, this.Length);
		}

		private static Object?[] ToArray(IReadOnlyList<Object?> arguments)
		{
			Object?[] array = new Object?[arguments.Count];
			for (Int32 i = 0; i < array.Length; i++)
			{
				array[i] = arguments[i];
			}

			return array;
		}
	}
}
