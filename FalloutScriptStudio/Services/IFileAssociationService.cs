namespace FalloutScriptStudio.Services
{
	using System;

	/// <summary>
	/// Registers the editor as the handler of Fallout script files for the current user. The service writes to the registry, so the
	/// view models depend on this interface and the tests replace it.
	/// </summary>
	public interface IFileAssociationService
	{
		/// <summary>Gets a value indicating whether the editor is already the handler of <c>.ssl</c> files.</summary>
		Boolean IsAssociated { get; }

		/// <summary>Makes the editor the handler of the Fallout script file types.</summary>
		/// <param name="executablePath">The full path of the editor executable.</param>
		/// <param name="iconsDirectory">The folder with the <c>icon_*.ico</c> files of the file types.</param>
		void Associate(String executablePath, String iconsDirectory);
	}
}