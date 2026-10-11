namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.Core.Compilation;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// Reads the binary files of the old editor (<c>settings.dat</c>, <c>recent.dat</c>, <c>folding.dat</c>) and its
	/// <c>.ini</c> lists so that existing users keep their configuration.
	/// </summary>
	/// <remarks>
	/// The read order is exactly the one of the legacy <c>Settings.LoadInternal</c>. A file that ends early or is damaged
	/// keeps the values read so far and a problem is reported; nothing is thrown for bad data.
	/// </remarks>
	public sealed class LegacySettingsImporter
	{
		private readonly ILogger<LegacySettingsImporter> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="LegacySettingsImporter"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public LegacySettingsImporter(ILogger<LegacySettingsImporter> logger)
		{
			ArgumentNullException.ThrowIfNull(logger);
			this._logger = logger;
		}

		/// <summary>
		/// Imports every legacy file found in the settings folder.
		/// </summary>
		/// <param name="paths">The paths of the installation.</param>
		/// <returns>The imported data.</returns>
		public LegacyImportResult ImportFromDirectory(AppPaths paths)
		{
			ArgumentNullException.ThrowIfNull(paths);

			List<String> problems = new List<String>();
			AppSettings? settings = null;
			RecentFiles? recent = null;
			ScriptPositions? positions = null;
			FoldedProcedures? folding = null;
			SearchHistory? history = null;
			PreprocessorDefines? defines = null;

			if (this.TryOpen(paths.LegacySettingsFile, problems, out Stream? settingsStream))
			{
				using (settingsStream)
				{
					settings = this.ReadSettings(settingsStream, problems);
				}
			}

			if (this.TryOpen(paths.LegacyRecentFile, problems, out Stream? recentStream))
			{
				using (recentStream)
				{
					(recent, positions) = this.ReadRecent(recentStream, problems);
				}
			}

			if (this.TryOpen(paths.LegacyFoldingFile, problems, out Stream? foldingStream))
			{
				using (foldingStream)
				{
					folding = this.ReadFolding(foldingStream, problems);
				}
			}

			IReadOnlyList<String>? queries = this.ReadLines(paths.LegacySearchHistoryFile, problems);
			IReadOnlyList<String>? directories = this.ReadLines(paths.LegacySearchFoldersFile, problems);
			if (queries != null || directories != null)
			{
				history = new SearchHistory();
				foreach (String query in queries ?? Array.Empty<String>())
				{
					history.Queries.Add(query);
				}

				history.Directories.AddRange(directories ?? Array.Empty<String>());
			}

			IReadOnlyList<String>? definitions = this.ReadLines(paths.LegacyPreprocessorDefinesFile, problems);
			if (definitions != null)
			{
				defines = new PreprocessorDefines();
				defines.Items.AddRange(definitions);

				// Every line of PreprocDefine.ini is a user definition (the "none" choice and DEBUG/RELEASE are items of the combo box
				// itself in the legacy editor); stray placeholders and duplicates are dropped.
				defines.Sanitize();
			}

			return new LegacyImportResult(settings, recent, positions, folding, history, defines, problems);
		}

		/// <summary>
		/// Reads a legacy <c>settings.dat</c> stream.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="problems">Receives a description when the stream is damaged.</param>
		/// <returns>The settings; values that could not be read keep their defaults.</returns>
		public AppSettings ReadSettings(Stream stream, ICollection<String> problems)
		{
			ArgumentNullException.ThrowIfNull(stream);
			ArgumentNullException.ThrowIfNull(problems);

			AppSettings settings = new AppSettings();
			try
			{
				using BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true);
				ReadSettingsFields(br, settings);
			}
			catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException or ArgumentException or OverflowException)
			{
				problems.Add("settings.dat is damaged or has an unexpected format: " + ex.Message);
				this._logger.LogWarning(ex, "The legacy settings file could not be read completely");
			}

			settings.Normalize();
			return settings;
		}

		/// <summary>
		/// Reads a legacy <c>recent.dat</c> stream.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="problems">Receives a description when the stream is damaged.</param>
		/// <returns>The recent files and the remembered script positions.</returns>
		public (RecentFiles Recent, ScriptPositions Positions) ReadRecent(Stream stream, ICollection<String> problems)
		{
			ArgumentNullException.ThrowIfNull(stream);
			ArgumentNullException.ThrowIfNull(problems);

			RecentFiles recent = new RecentFiles();
			ScriptPositions positions = new ScriptPositions();
			try
			{
				using BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true);
				Int32 recentCount = br.ReadByte();
				Int32 recentMessageCount = br.ReadByte();
				for (Int32 i = 0; i < recentCount; i++)
				{
					recent.Scripts.Add(br.ReadString());
				}

				for (Int32 i = 0; i < recentMessageCount; i++)
				{
					recent.Messages.Add(br.ReadString());
				}

				Int32 positionCount = br.ReadByte();
				for (Int32 i = 0; i < positionCount; i++)
				{
					String script = br.ReadString();
					positions.Lines[script] = br.ReadUInt16();
				}
			}
			catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException or ArgumentException)
			{
				problems.Add("recent.dat is damaged or has an unexpected format: " + ex.Message);
				this._logger.LogWarning(ex, "The legacy recent files list could not be read completely");
			}

			return (recent, positions);
		}

		/// <summary>
		/// Reads a legacy <c>folding.dat</c> stream.
		/// </summary>
		/// <param name="stream">The stream.</param>
		/// <param name="problems">Receives a description when the stream is damaged.</param>
		/// <returns>The folded procedures.</returns>
		public FoldedProcedures ReadFolding(Stream stream, ICollection<String> problems)
		{
			ArgumentNullException.ThrowIfNull(stream);
			ArgumentNullException.ThrowIfNull(problems);

			FoldedProcedures folding = new FoldedProcedures();
			if (stream.CanSeek && stream.Length == 0)
			{
				return folding;
			}

			try
			{
				using BinaryReader br = new BinaryReader(stream, Encoding.UTF8, true);
				Int32 scriptCount = br.ReadInt32();
				for (Int32 i = 0; i < scriptCount; i++)
				{
					String script = br.ReadString();
					Int32 procedureCount = br.ReadInt32();
					for (Int32 j = 0; j < procedureCount; j++)
					{
						folding.SetFolded(script, br.ReadString());
					}
				}
			}
			catch (Exception ex) when (ex is EndOfStreamException or IOException or FormatException or ArgumentException or OverflowException)
			{
				problems.Add("folding.dat is damaged or has an unexpected format: " + ex.Message);
				this._logger.LogWarning(ex, "The legacy folding file could not be read completely");
			}

			return folding;
		}

		private static void ReadSettingsFields(BinaryReader br, AppSettings settings)
		{
			settings.IsFirstRunCompleted = br.ReadBoolean();
			settings.Messages.RegisterScriptAllowDefine = br.ReadBoolean();
			br.ReadByte(); // unused
			settings.Editor.Theme = ToEnum(br.ReadByte(), HighlightTheme.Dark);
			settings.Editor.FileEncoding = ToEnum(br.ReadByte(), TextEncodingKind.Ansi);
			settings.Compiler.Optimization = ToOptimization(br.ReadByte());
			settings.Compiler.ShowWarnings = br.ReadBoolean();
			settings.Compiler.DebugInfo = br.ReadBoolean();
			settings.Compiler.SearchIncludePath = br.ReadBoolean();
			settings.Paths.OutputDirectory = ReadOptionalString(br, settings.Paths.OutputDirectory);
			settings.Compiler.WarnOnFailedCompile = br.ReadBoolean();
			settings.Compiler.MultiThreadedBatch = br.ReadBoolean();
			settings.Paths.LastMassCompileDirectory = ReadOptionalString(br, settings.Paths.LastMassCompileDirectory);
			settings.Paths.LastSearchDirectory = ReadOptionalString(br, settings.Paths.LastSearchDirectory);

			WindowPlacement mainWindow = new WindowPlacement
			{
				IsMaximized = br.ReadBoolean(),
				X = br.ReadInt32(),
				Y = br.ReadInt32(),
				Width = br.ReadInt32(),
				Height = br.ReadInt32(),
			};
			if (!mainWindow.IsEmpty)
			{
				settings.Window.SetPlacement(WindowKind.Main, mainWindow);
			}

			settings.Window.SplitterPosition = br.ReadInt32();
			settings.Messages.AutoOpenMessageFiles = br.ReadBoolean();
			settings.Window.SecondarySplitterPosition = br.ReadInt32();
			settings.Paths.HeadersDirectory = ReadOptionalString(br, settings.Paths.HeadersDirectory);

			String language = br.ReadString();
			settings.Messages.Language = language.Length == 0 ? "english" : language;

			settings.Editor.TabsToSpaces = br.ReadBoolean();
			settings.Editor.TabSize = br.ReadInt32();
			settings.Editor.EnableParser = br.ReadBoolean();
			settings.Compiler.ShortCircuit = br.ReadBoolean();
			settings.Editor.Autocomplete = br.ReadBoolean();
			settings.Window.ShowLog = br.ReadBoolean();
			settings.Editor.ParserWarnings = br.ReadBoolean();
			Boolean useWatcom = br.ReadBoolean();
			settings.Compiler.PreprocessorDefine = ReadOptionalString(br, settings.Compiler.PreprocessorDefine);
			settings.Compiler.CompileToSourceDirectory = br.ReadBoolean();

			Int32 messageFolders = br.ReadByte();
			for (Int32 i = 0; i < messageFolders; i++)
			{
				settings.Paths.MessageSearchDirectories.Add(br.ReadString());
			}

			settings.Messages.OpenEditorAutomatically = br.ReadBoolean();
			settings.Compiler.UseUserCommand = br.ReadBoolean();
			settings.Messages.HighlightComments = br.ReadBoolean();
			settings.Messages.CommentColor = ToEnum(br.ReadByte(), CommentHighlightColor.LightYellow);
			settings.Messages.FontSizeIndex = br.ReadByte();
			settings.Paths.ScriptsHeaderFile = ReadOptionalString(br, settings.Paths.ScriptsHeaderFile);
			settings.Messages.AssociateByScriptId = br.ReadBoolean();

			settings.Flowchart.AutoUpdate = br.ReadBoolean();
			settings.Flowchart.AutoSaveChart = br.ReadBoolean();
			settings.Flowchart.AutoArrange = br.ReadBoolean();
			settings.Flowchart.WithoutExitNode = br.ReadBoolean();
			Boolean useMcpp = br.ReadBoolean();
			settings.Compiler.Preprocessor = useWatcom
				? PreprocessorKind.Watcom
				: useMcpp ? PreprocessorKind.Mcpp : PreprocessorKind.Builtin;

			settings.Editor.AutocompleteColor = br.ReadBoolean();
			settings.Editor.AutoInputPairedCharacters = br.ReadBoolean();
			settings.Editor.ShowTabCharacters = br.ReadBoolean();
			settings.Editor.AutoRemoveTrailingSpaces = br.ReadBoolean();
			settings.Editor.ShowTips = br.ReadBoolean();
			settings.Editor.ShortDescriptions = br.ReadBoolean();
			settings.Flowchart.AutoHideNodes = br.ReadBoolean();
			settings.Editor.FontIndex = br.ReadByte();
			settings.Editor.FontSizeAdjustment = br.ReadSByte();
			settings.Editor.ShowVerticalRuler = br.ReadBoolean();
			settings.Editor.StoreLastPosition = br.ReadBoolean();
			settings.Editor.SaveScriptsAsUtf8 = br.ReadBoolean();
			settings.Compiler.DecompileForFallout1 = br.ReadBoolean();
			_ = br.ReadBoolean(); // native text rendering: a WinForms-only option, the WPF editor has nothing to switch
			settings.Paths.ExternalEditorPath = ReadOptionalString(br, settings.Paths.ExternalEditorPath);
			settings.Compiler.UseLegacyDecompiler = br.ReadBoolean();

			// The old decompiler only exists as an external program: choosing it in the legacy editor selects the external decompiler here.
			if (settings.Compiler.UseLegacyDecompiler)
			{
				settings.Compiler.Decompiler = DecompilerKind.ExternalInt2Ssl;
			}

			settings.Editor.SearchIgnoreCase = br.ReadBoolean();
			settings.Editor.SearchWholeWord = br.ReadBoolean();
			settings.Paths.SolutionFolder = ReadOptionalString(br, settings.Paths.SolutionFolder);
		}

		/// <summary>Reads a string; an empty string keeps the current value (the legacy "not set" convention).</summary>
		private static String? ReadOptionalString(BinaryReader br, String? current)
		{
			String value = br.ReadString();
			return value.Length > 0 ? value : current;
		}

		private static TEnum ToEnum<TEnum>(Byte value, TEnum fallback)
			where TEnum : struct, Enum
		{
			TEnum candidate = (TEnum)Enum.ToObject(typeof(TEnum), value);
			return Enum.IsDefined(candidate) ? candidate : fallback;
		}

		/// <summary>Old files may contain 255 (written by an even older version) which the legacy dialog displayed as "Basic".</summary>
		private static OptimizationLevel ToOptimization(Byte value)
		{
			return value == Byte.MaxValue ? OptimizationLevel.Basic : ToEnum(value, OptimizationLevel.Basic);
		}

		private Boolean TryOpen(String path, List<String> problems, [NotNullWhen(true)] out Stream? stream)
		{
			stream = null;
			if (!File.Exists(path))
			{
				return false;
			}

			try
			{
				stream = File.OpenRead(path);
				return true;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				problems.Add(Path.GetFileName(path) + " could not be opened: " + ex.Message);
				this._logger.LogWarning(ex, "The legacy file {Path} could not be opened", path);
				return false;
			}
		}

		private List<String>? ReadLines(String path, List<String> problems)
		{
			if (!File.Exists(path))
			{
				return null;
			}

			try
			{
				List<String> lines = new List<String>();
				foreach (String line in File.ReadAllLines(path))
				{
					if (line.Length > 0)
					{
						lines.Add(line);
					}
				}

				return lines;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				problems.Add(Path.GetFileName(path) + " could not be read: " + ex.Message);
				this._logger.LogWarning(ex, "The legacy file {Path} could not be read", path);
				return null;
			}
		}
	}
}
