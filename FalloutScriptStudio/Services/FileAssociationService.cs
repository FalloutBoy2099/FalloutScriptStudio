namespace FalloutScriptStudio.Services
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Runtime.InteropServices;
	using System.Runtime.Versioning;
	using Microsoft.Win32;

	/// <summary>
	/// Registers the editor as the handler of Fallout script files (<c>.ssl .int .msg .fcd</c>) for the current user
	/// and knows which file types the editor is allowed to open.
	/// </summary>
	[SupportedOSPlatform("windows")]
	public sealed class FileAssociationService : IFileAssociationService
	{
		private const Int32 SHCNE_ASSOCCHANGED = 0x8000000;
		private const UInt32 SHCNF_IDLIST = 0;
		private const String ProgIdPrefix = "FalloutScriptStudio";

		private static readonly String[] _associatedExtensions = { ".ssl", ".msg", ".int", ".fcd" };
		private static readonly String[] _openableExtensions = { ".ssl", ".msg", ".int", ".fcd", ".h", ".ini", ".txt", ".cfg", ".xshd" };

		/// <inheritdoc />
		public Boolean IsAssociated
		{
			get
			{
				using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.ssl", false);
				return String.Equals(key?.GetValue(String.Empty) as String, ProgIdPrefix + "SSL", StringComparison.Ordinal);
			}
		}

		/// <summary>Determines whether the editor can open a file of the given path (by extension).</summary>
		public static Boolean IsOpenable(String path)
		{
			String extension = Path.GetExtension(path).ToLowerInvariant();
			return _openableExtensions.Contains(extension, StringComparer.Ordinal);
		}

		/// <summary>
		/// Resolves the script that belongs to a <c>.fcd</c> flowchart file: the <c>.ssl</c> next to it or in the parent folder.
		/// </summary>
		/// <returns>The script path, or <see langword="null"/> when none exists.</returns>
		public static String? ResolveFlowchartScript(String fcdPath)
		{
			String script = Path.ChangeExtension(fcdPath, ".ssl");
			if (File.Exists(script))
			{
				return script;
			}

			String? directory = Path.GetDirectoryName(script);
			String? parent = directory is null ? null : Path.GetDirectoryName(directory);
			if (parent is null)
			{
				return null;
			}

			String candidate = Path.Combine(parent, Path.GetFileName(script));
			return File.Exists(candidate) ? candidate : null;
		}

		/// <summary>Registers the file types under <c>HKCU\Software\Classes</c> (no administrator rights required).</summary>
		/// <param name="executablePath">Path of the application executable.</param>
		/// <param name="iconsDirectory">Directory containing <c>icon_ssl.ico</c>, <c>icon_msg.ico</c>, ...</param>
		public void Associate(String executablePath, String iconsDirectory)
		{
			foreach (String extension in _associatedExtensions)
			{
				String kind = extension.TrimStart('.');
				String progId = ProgIdPrefix + kind.ToUpperInvariant();

				using (RegistryKey extensionKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + extension))
				{
					extensionKey.SetValue(String.Empty, progId);
				}

				using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + progId);
				key.SetValue(String.Empty, "Fallout Script Studio");
				key.SetValue("AlwaysShowExt", String.Empty);
				key.CreateSubKey("DefaultIcon").SetValue(String.Empty, Path.Combine(iconsDirectory, "icon_" + kind + ".ico"));
				key.CreateSubKey(@"Shell\OpenFalloutScriptStudio").SetValue(String.Empty, "Open in Fallout Script Studio");
				key.CreateSubKey(@"Shell\OpenFalloutScriptStudio\Command").SetValue(String.Empty, "\"" + executablePath + "\" \"%1\"");
				key.CreateSubKey("Shell").SetValue(String.Empty, "OpenFalloutScriptStudio");
			}

			SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
		}

		[DllImport("shell32.dll", SetLastError = true)]
		private static extern void SHChangeNotify(Int32 wEventId, UInt32 uFlags, IntPtr dwItem1, IntPtr dwItem2);
	}
}
