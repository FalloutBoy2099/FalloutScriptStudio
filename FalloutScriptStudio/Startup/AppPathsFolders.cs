namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>The folders of an installation, as <see cref="AppPaths"/> manages them.</summary>
	public sealed class AppPathsFolders : IStartupFolders
	{
		private readonly AppPaths _paths;

		/// <summary>Initializes a new instance of the <see cref="AppPathsFolders"/> class.</summary>
		/// <param name="paths">The paths of the installation.</param>
		public AppPathsFolders(AppPaths paths)
		{
			ArgumentNullException.ThrowIfNull(paths);
			this._paths = paths;
		}

		/// <inheritdoc />
		public IReadOnlyList<String> EnsureCreated()
		{
			return this._paths.EnsureCreated();
		}

		/// <inheritdoc />
		public IReadOnlyList<String> PrepareScratch()
		{
			return this._paths.PrepareScratchDirectory();
		}

		/// <inheritdoc />
		public IReadOnlyList<String> DeleteScratch()
		{
			return this._paths.DeleteScratchDirectory();
		}
	}
}
