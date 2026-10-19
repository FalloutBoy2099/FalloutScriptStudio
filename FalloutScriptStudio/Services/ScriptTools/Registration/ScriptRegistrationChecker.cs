namespace FalloutScriptStudio.Services.ScriptTools.Registration
{
	using System;
	using System.IO;
	using Localization;

	/// <summary>
	/// The checks done before a script file is registered in <c>scripts.lst</c> (legacy <c>registerScriptToolStripMenuItem_Click</c>).
	/// </summary>
	public static class ScriptRegistrationChecker
	{
		/// <summary>The longest script file name the game accepts, without the extension.</summary>
		public const Int32 MaxScriptNameLength = 8;

		/// <summary>
		/// Checks whether a file can be registered.
		/// </summary>
		/// <param name="filePath">The path of the script, or <see langword="null"/> when it was never saved.</param>
		/// <returns>The verdict.</returns>
		public static ScriptRegistrationCheck Check(String? filePath)
		{
			if (String.IsNullOrEmpty(filePath))
			{
				return Refuse(Loc.Get("Str.ScriptTools.RegCheckUnsaved"));
			}

			String fileName = Path.GetFileName(filePath);
			String extension = Path.GetExtension(fileName);
			if (!extension.Equals(".ssl", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".int", StringComparison.OrdinalIgnoreCase))
			{
				return Refuse(Loc.Get("Str.ScriptTools.RegCheckWrongFile"));
			}

			String compiled = Path.ChangeExtension(fileName, "int");
			if (Path.GetFileNameWithoutExtension(compiled).Length > MaxScriptNameLength)
			{
				return Refuse(Loc.Format("Str.ScriptTools.RegCheckNameLength", MaxScriptNameLength));
			}

			if (fileName.Length >= 2 && String.Compare(fileName, 0, "gl", 0, 2, StringComparison.OrdinalIgnoreCase) == 0)
			{
				return new ScriptRegistrationCheck(
					ScriptRegistrationCheckKind.NeedsConfirmation,
					compiled,
					Loc.Get("Str.ScriptTools.RegCheckGlobalTitle"),
					Loc.Get("Str.ScriptTools.RegCheckGlobalMessage"));
			}

			if (compiled.Contains(' '))
			{
				return Refuse(Loc.Get("Str.ScriptTools.RegCheckSpaces"));
			}

			return new ScriptRegistrationCheck(ScriptRegistrationCheckKind.Allowed, compiled, String.Empty, String.Empty);
		}

		/// <summary>
		/// Checks a script file name typed into the list.
		/// </summary>
		/// <param name="scriptFileName">The file name.</param>
		/// <returns><see langword="true"/> when the name without extension is short enough.</returns>
		public static Boolean IsValidListedName(String scriptFileName)
		{
			ArgumentNullException.ThrowIfNull(scriptFileName);
			return Path.GetFileNameWithoutExtension(scriptFileName).Length <= MaxScriptNameLength;
		}

		private static ScriptRegistrationCheck Refuse(String message)
		{
			return new ScriptRegistrationCheck(ScriptRegistrationCheckKind.Refused, String.Empty, Loc.Get("Str.Common.Error"), message);
		}
	}
}