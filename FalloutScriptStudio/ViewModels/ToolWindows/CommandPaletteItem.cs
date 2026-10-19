namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;
	using Commands;
	using Documents;
	using Core.TextTools;
	using FalloutScriptStudio.Editor;
	using Localization;

	/// <summary>A row of the command palette: a command with its name, menu path, shortcut and state, in the language of the moment it was created.</summary>
	public sealed class CommandPaletteItem
	{
		private CommandPaletteItem(CommandEntry entry, String gesture, Boolean isRecent)
		{
			this.Entry = entry;
			this.Name = entry.Title;
			this.PathText = entry.Path;
			this.Gesture = gesture;
			this.IsRecent = isRecent;
			CommandAvailability availability = entry.Availability;
			this.IsEnabled = availability == CommandAvailability.Available;
			this.ReasonText = this.IsEnabled ? String.Empty : Loc.Get("Str.Shell.Palette.Why." + availability);
			this.StateText = GetStateText(entry);
			this.RecentText = isRecent ? Loc.Get("Str.Shell.Palette.Recent") : String.Empty;
			this.AccessibleName = this.BuildAccessibleName();
			this.NameParts = new[] { new PaletteTextPart(this.Name, false) };
		}

		/// <summary>Gets the command.</summary>
		public CommandEntry Entry { get; }

		/// <summary>Gets the name of the command (the header of its menu item).</summary>
		public String Name { get; }

		/// <summary>Gets the menus above the command (<c>Code</c>), empty for a command that has none.</summary>
		public String PathText { get; }

		/// <summary>Gets the whole name (<c>Code &gt; Format Document</c>).</summary>
		public String FullName => this.PathText.Length == 0 ? this.Name : this.PathText + CommandEntry.PathSeparator + this.Name;

		/// <summary>Gets the shortcut of the command, empty when it has none.</summary>
		public String Gesture { get; }

		/// <summary>Gets a value indicating whether the command can run now.</summary>
		public Boolean IsEnabled { get; }

		/// <summary>Gets why the command cannot run now; empty for a command that can.</summary>
		public String ReasonText { get; }

		/// <summary>Gets a value indicating whether <see cref="ReasonText"/> is shown (the command cannot run now).</summary>
		public Boolean HasReason => this.ReasonText.Length > 0;

		/// <summary>Gets the state of a setting or a choice (<c>On</c>, <c>Off</c>, <c>Current</c>), empty for a plain command.</summary>
		public String StateText { get; }

		/// <summary>Gets a value indicating whether the command is one of the recently used commands.</summary>
		public Boolean IsRecent { get; }

		/// <summary>Gets the text that marks a recently used command, empty for the others.</summary>
		public String RecentText { get; }

		/// <summary>Gets the name cut into the pieces the filter matched and the others (one piece without a filter), so that the matched letters can be emphasized.</summary>
		public IReadOnlyList<PaletteTextPart> NameParts { get; private set; }

		/// <summary>Gets the dimmed text at the right end of the row: the state of the option, the recent mark and the shortcut, on one line.</summary>
		public String TrailingText => String.Join("  ", new[] { this.StateText, this.RecentText, this.Gesture }.Where(text => text.Length > 0));

		/// <summary>Gets the name a screen reader reads for the row.</summary>
		public String AccessibleName { get; }

		/// <summary>Creates the row of an entry.</summary>
		/// <param name="entry">The command.</param>
		/// <param name="gestures">Tells the shortcut of the commands, or <see langword="null"/>.</param>
		/// <param name="isRecent">Whether the command was used recently.</param>
		/// <returns>The row.</returns>
		public static CommandPaletteItem Create(CommandEntry entry, ICommandGestureSource? gestures, Boolean isRecent)
		{
			ArgumentNullException.ThrowIfNull(entry);
			return new CommandPaletteItem(entry, GetGesture(entry, gestures), isRecent);
		}

		/// <summary>Cuts the name into the pieces the filter matched and the others.</summary>
		/// <param name="pattern">What the user typed; empty for no emphasis.</param>
		public void Highlight(String pattern)
		{
			ArgumentNullException.ThrowIfNull(pattern);

			List<PaletteTextPart> parts = [];
			Int32 position = 0;
			foreach (MatchRange range in FuzzyMatcher.FindMatchRanges(pattern, this.Name))
			{
				if (range.Start > position)
				{
					parts.Add(new PaletteTextPart(this.Name[position..range.Start], false));
				}

				parts.Add(new PaletteTextPart(this.Name.Substring(range.Start, range.Length), true));
				position = range.Start + range.Length;
			}

			if (position < this.Name.Length || parts.Count == 0)
			{
				parts.Add(new PaletteTextPart(this.Name[position..], false));
			}

			this.NameParts = parts;
		}

		private static String GetGesture(CommandEntry entry, ICommandGestureSource? gestures)
		{
			String? gesture = gestures?.GetGesture(entry);
			if (String.IsNullOrEmpty(gesture) && entry.Parameter is EditorCommand command)
			{
				IEnumerable<InputGesture> editorGestures = EditorCommands.All.FirstOrDefault(pair => pair.Key == command).Value?.InputGestures.Cast<InputGesture>() ?? Enumerable.Empty<InputGesture>();
				gesture = GestureText.FormatAll(editorGestures);
			}

			return gesture ?? String.Empty;
		}

		private static String GetStateText(CommandEntry entry)
		{
			return entry.StateKind switch
			{
				CommandStateKind.Toggle => Loc.Get(entry.IsOn ? "Str.Shell.Palette.On" : "Str.Shell.Palette.Off"),
				CommandStateKind.Choice when entry.IsOn => Loc.Get("Str.Shell.Palette.Current"),
				_ => String.Empty,
			};
		}

		private String BuildAccessibleName()
		{
			List<String> parts = [this.PathText.Length == 0 ? this.Name : this.Name + ", " + this.PathText];
			if (this.StateText.Length > 0)
			{
				parts.Add(this.StateText);
			}

			if (this.IsRecent)
			{
				parts.Add(this.RecentText);
			}

			if (this.Gesture.Length > 0)
			{
				parts.Add(Loc.Format("Str.Shell.Palette.AccShortcut", this.Gesture));
			}

			if (!this.IsEnabled)
			{
				parts.Add(Loc.Format("Str.Shell.Palette.AccUnavailable", this.ReasonText));
			}

			return String.Join(", ", parts);
		}
	}
}