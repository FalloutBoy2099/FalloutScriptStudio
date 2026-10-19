namespace FalloutScriptStudio.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;

	/// <summary>
	/// A command of the application as the command palette lists it: its name (the menu path and the header of the menu item), the command to
	/// run, and its state at this moment. The name follows the language; the state is read each time it is asked.
	/// </summary>
	public sealed class CommandEntry
	{
		/// <summary>The text between the parts of the name of a command.</summary>
		public const String PathSeparator = " > ";

		private readonly Func<CommandAvailability> _availability;
		private readonly Func<Boolean>? _isOn;
		private readonly Boolean _isChoice;

		/// <summary>Initializes a new instance of the <see cref="CommandEntry"/> class.</summary>
		/// <param name="id">The stable identifier.</param>
		/// <param name="path">The menus above the command, outermost first.</param>
		/// <param name="title">The name of the command itself.</param>
		/// <param name="command">The command to run.</param>
		/// <param name="parameter">The parameter of the command, or <see langword="null"/>.</param>
		/// <param name="availability">Tells whether the command can run now.</param>
		/// <param name="isOn">Tells whether a check item is on (or, for a choice, whether it is the current one); <see langword="null"/> for a command that has no such state.</param>
		/// <param name="isChoice"><see langword="true"/> when the entry is one of several exclusive choices (a theme, a language) instead of a setting that is on or off.</param>
		/// <param name="property">The Boolean property of the main view model a check item changes, or <see langword="null"/>.</param>
		public CommandEntry(
			String id,
			IReadOnlyList<CommandText> path,
			CommandText title,
			ICommand command,
			Object? parameter,
			Func<CommandAvailability> availability,
			Func<Boolean>? isOn = null,
			Boolean isChoice = false,
			String? property = null)
		{
			ArgumentException.ThrowIfNullOrEmpty(id);
			ArgumentNullException.ThrowIfNull(path);
			ArgumentNullException.ThrowIfNull(command);
			ArgumentNullException.ThrowIfNull(availability);
			this.Id = id;
			this.PathParts = path;
			this.TitlePart = title;
			this.Command = command;
			this.Parameter = parameter;
			this._availability = availability;
			this._isOn = isOn;
			this._isChoice = isChoice;
			this.Property = property;
		}

		/// <summary>Gets the stable identifier of the command.</summary>
		public String Id { get; }

		/// <summary>Gets the command to run.</summary>
		public ICommand Command { get; }

		/// <summary>Gets the parameter of the command, or <see langword="null"/>.</summary>
		public Object? Parameter { get; }

		/// <summary>Gets the property of the main view model that a check item changes, or <see langword="null"/> when the entry runs a command.</summary>
		public String? Property { get; }

		/// <summary>Gets the menus above the command, outermost first.</summary>
		public IReadOnlyList<CommandText> PathParts { get; }

		/// <summary>Gets the name of the command itself.</summary>
		public CommandText TitlePart { get; }

		/// <summary>Gets the name of the command in the current language (<c>Format Document</c>).</summary>
		public String Title => this.TitlePart.Resolve();

		/// <summary>Gets the menus above the command in the current language (<c>Code</c>, <c>View &gt; Theme</c>).</summary>
		public String Path => String.Join(PathSeparator, this.PathParts.Select(part => part.Resolve()));

		/// <summary>Gets the whole name in the current language (<c>Code &gt; Format Document</c>).</summary>
		public String FullName => this.PathParts.Count == 0 ? this.Title : this.Path + PathSeparator + this.Title;

		/// <summary>Gets what kind of state the entry shows: none for a plain command, on or off for a setting, current for a choice.</summary>
		public CommandStateKind StateKind => this._isOn is null ? CommandStateKind.None : this._isChoice ? CommandStateKind.Choice : CommandStateKind.Toggle;

		/// <summary>Gets a value indicating whether a check item is on (or a choice is the current one) now; always <see langword="false"/> for a plain command.</summary>
		public Boolean IsOn => this._isOn?.Invoke() ?? false;

		/// <summary>Gets whether the command can run now and, when it cannot, why.</summary>
		public CommandAvailability Availability => this._availability();

		/// <summary>Gets a value indicating whether the command can run now: its conditions hold and its <see cref="ICommand.CanExecute"/> agrees.</summary>
		public Boolean CanRun => this.Availability == CommandAvailability.Available;
	}
}