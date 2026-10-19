namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// Raised by <see cref="IProjectService"/> after the project file was changed outside of the editor (edited by hand, replaced, deleted)
	/// and the change was handled.
	/// </summary>
	public sealed class ProjectFileChangedEventArgs : EventArgs
	{
		/// <summary>Initializes a new instance of the <see cref="ProjectFileChangedEventArgs"/> class.</summary>
		/// <param name="kind">What happened.</param>
		/// <param name="path">The full path of the project file.</param>
		/// <param name="error">Why the content is not valid, for <see cref="ProjectFileChangeKind.Invalid"/>.</param>
		public ProjectFileChangedEventArgs(ProjectFileChangeKind kind, String path, ProjectFileError? error = null)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			this.Kind = kind;
			this.Path = path;
			this.Error = error;
		}

		/// <summary>Gets what happened.</summary>
		public ProjectFileChangeKind Kind { get; }

		/// <summary>Gets the full path of the project file.</summary>
		public String Path { get; }

		/// <summary>Gets why the content is not valid (<see cref="ProjectFileChangeKind.Invalid"/>), otherwise <see langword="null"/>.</summary>
		public ProjectFileError? Error { get; }
	}
}