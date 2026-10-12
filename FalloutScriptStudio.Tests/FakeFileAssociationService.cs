namespace FalloutScriptStudio.Tests
{
	using System;
	using FalloutScriptStudio.Services;

	/// <summary>A file association service that never touches the registry.</summary>
	internal sealed class FakeFileAssociationService : IFileAssociationService
	{
		public Boolean IsAssociated { get; set; }

		public Exception? FailWith { get; set; }

		public Int32 AssociateCount { get; private set; }

		public String? LastExecutable { get; private set; }

		public String? LastIconsDirectory { get; private set; }

		public void Associate(String executablePath, String iconsDirectory)
		{
			if (this.FailWith is not null)
			{
				throw this.FailWith;
			}

			this.AssociateCount++;
			this.LastExecutable = executablePath;
			this.LastIconsDirectory = iconsDirectory;
			this.IsAssociated = true;
		}
	}
}
