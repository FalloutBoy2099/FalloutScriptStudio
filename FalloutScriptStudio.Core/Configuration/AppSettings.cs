namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Text.Json;

	/// <summary>
	/// All persisted user options of the editor. This is a plain mutable model; the settings dialog edits a
	/// <see cref="Clone"/> of it and hands the result back to <see cref="ISettingsService.Apply"/>.
	/// </summary>
	public sealed class AppSettings
	{
		/// <summary>The current version of the settings file layout.</summary>
		public const Int32 CurrentVersion = 1;

		/// <summary>Gets or sets the version of the file layout this instance was loaded from.</summary>
		public Int32 Version { get; set; } = CurrentVersion;

		/// <summary>Gets or sets a value indicating whether the program has been started (and saved its settings) before.</summary>
		public Boolean IsFirstRunCompleted { get; set; }

		/// <summary>Gets or sets the folders and files the user configured.</summary>
		public PathSettings Paths { get; set; } = new PathSettings();

		/// <summary>Gets or sets the compilation options.</summary>
		public CompilerSettings Compiler { get; set; } = new CompilerSettings();

		/// <summary>Gets or sets the script editor options.</summary>
		public EditorSettings Editor { get; set; } = new EditorSettings();

		/// <summary>Gets or sets the message file options.</summary>
		public MessageSettings Messages { get; set; } = new MessageSettings();

		/// <summary>Gets or sets the flowchart options.</summary>
		public FlowchartSettings Flowchart { get; set; } = new FlowchartSettings();

		/// <summary>Gets or sets the window layout.</summary>
		public WindowSettings Window { get; set; } = new WindowSettings();

		/// <summary>
		/// Creates a deep copy of the settings.
		/// </summary>
		/// <returns>The copy.</returns>
		public AppSettings Clone()
		{
			return JsonSerializer.Deserialize<AppSettings>(JsonSerializer.SerializeToUtf8Bytes(this, SettingsJson.Options), SettingsJson.Options)
				?? new AppSettings();
		}

		/// <summary>
		/// Repairs values that are missing or out of range (for example after the file was edited by hand).
		/// </summary>
		public void Normalize()
		{
			this.Paths ??= new PathSettings();
			this.Compiler ??= new CompilerSettings();
			this.Editor ??= new EditorSettings();
			this.Editor.Formatting ??= new FormatSettings();
			this.Editor.Formatting.Normalize();
			this.Messages ??= new MessageSettings();
			this.Flowchart ??= new FlowchartSettings();
			this.Window ??= new WindowSettings();
			this.Paths.MessageSearchDirectories ??= [];
			this.Window.Placements ??= [];

			if (String.IsNullOrEmpty(this.Messages.Language))
			{
				this.Messages.Language = "english";
			}

			if (String.IsNullOrWhiteSpace(this.Compiler.PreprocessorDefine) || PreprocessorDefines.IsPlaceholder(this.Compiler.PreprocessorDefine))
			{
				this.Compiler.PreprocessorDefine = null;
			}

			this.Compiler.Profiles = CompileProfile.Normalize(this.Compiler.Profiles);
			this.Compiler.ActiveProfile = String.IsNullOrWhiteSpace(this.Compiler.ActiveProfile) ? null : this.Compiler.ActiveProfile.Trim();

			if (this.Editor.TabSize < EditorSettings.MinTabSize || this.Editor.TabSize > EditorSettings.MaxTabSize)
			{
				this.Editor.TabSize = EditorSettings.DefaultTabSize;
			}

			if (this.Editor.FontIndex < 0)
			{
				this.Editor.FontIndex = 0;
			}

			this.Editor.ZoomPercent = Math.Clamp(this.Editor.ZoomPercent, EditorSettings.MinZoomPercent, EditorSettings.MaxZoomPercent);
			if (!Enum.IsDefined(this.Editor.LiveDiagnostics))
			{
				this.Editor.LiveDiagnostics = LiveDiagnosticsLevel.ErrorsAndWarnings;
			}
		}
	}
}