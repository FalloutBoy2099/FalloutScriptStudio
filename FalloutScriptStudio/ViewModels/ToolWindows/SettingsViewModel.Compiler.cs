namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Localization;

	// Compiler page: preprocessor, optimization and decompiler.
	public sealed partial class SettingsViewModel
	{
		/// <summary>Gets the optimization levels to choose from.</summary>
		public IReadOnlyList<Choice<OptimizationLevel>> OptimizationChoices { get; } = new List<Choice<OptimizationLevel>>
		{
			new Choice<OptimizationLevel>(OptimizationLevel.None, Loc.Get("Str.ToolWindows.Choice.Optimization.None")),
			new Choice<OptimizationLevel>(OptimizationLevel.Basic, Loc.Get("Str.ToolWindows.Choice.Optimization.Basic")),
			new Choice<OptimizationLevel>(OptimizationLevel.Full, Loc.Get("Str.ToolWindows.Choice.Optimization.Full")),
			new Choice<OptimizationLevel>(OptimizationLevel.Experimental, Loc.Get("Str.ToolWindows.Choice.Optimization.Experimental")),
		};

		/// <summary>Gets the preprocessors to choose from.</summary>
		public IReadOnlyList<Choice<PreprocessorKind>> PreprocessorChoices { get; } = new List<Choice<PreprocessorKind>>
		{
			new Choice<PreprocessorKind>(PreprocessorKind.Builtin, Loc.Get("Str.ToolWindows.Choice.Preprocessor.Builtin")),
			new Choice<PreprocessorKind>(PreprocessorKind.Mcpp, Loc.Get("Str.ToolWindows.Choice.Preprocessor.Mcpp")),
			new Choice<PreprocessorKind>(PreprocessorKind.Watcom, Loc.Get("Str.ToolWindows.Choice.Preprocessor.Watcom")),
		};

		/// <summary>Gets the decompilers to choose from.</summary>
		public IReadOnlyList<Choice<DecompilerKind>> DecompilerChoices { get; } = new List<Choice<DecompilerKind>>
		{
			new Choice<DecompilerKind>(DecompilerKind.BuiltIn, Loc.Get("Str.ToolWindows.Choice.Decompiler.BuiltIn")),
			new Choice<DecompilerKind>(DecompilerKind.ExternalInt2Ssl, Loc.Get("Str.ToolWindows.Choice.Decompiler.External")),
		};

		/// <summary>Gets or sets a value indicating whether the user batch file compiles the scripts.</summary>
		public Boolean UseUserCommand
		{
			get => this.Draft.Compiler.UseUserCommand;
			set
			{
				if (this.Draft.Compiler.UseUserCommand != value)
				{
					this.Draft.Compiler.UseUserCommand = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.IsPreprocessorEnabled));
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether compiled scripts are written next to their sources.</summary>
		public Boolean CompileToSourceDirectory
		{
			get => this.Draft.Compiler.CompileToSourceDirectory;
			set
			{
				if (this.Draft.Compiler.CompileToSourceDirectory != value)
				{
					this.Draft.Compiler.CompileToSourceDirectory = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.IsOutputDirectoryEnabled));
				}
			}
		}

		/// <summary>Gets a value indicating whether the output folder can be edited (it is ignored while compiling next to the sources).</summary>
		public Boolean IsOutputDirectoryEnabled => !this.Draft.Compiler.CompileToSourceDirectory;

		/// <summary>Gets a value indicating whether the preprocessor can be chosen (not when a batch file does the compilation).</summary>
		public Boolean IsPreprocessorEnabled => !this.Draft.Compiler.UseUserCommand;

		/// <summary>Gets or sets the decompiler that turns compiled scripts back into source.</summary>
		public DecompilerKind Decompiler
		{
			get => this.Draft.Compiler.Decompiler;
			set
			{
				if (this.Draft.Compiler.Decompiler != value)
				{
					this.Draft.Compiler.Decompiler = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.IsExternalDecompilerEnabled));
				}
			}
		}

		/// <summary>Gets a value indicating whether the options of the external decompiler apply (they do not for the built-in one).</summary>
		public Boolean IsExternalDecompilerEnabled => this.Draft.Compiler.Decompiler == DecompilerKind.ExternalInt2Ssl;

		/// <summary>Gets or sets the path of the external <c>int2ssl.exe</c> (empty to look for it in the resources folder).</summary>
		public String ExternalDecompilerPath
		{
			get => this.Draft.Compiler.ExternalDecompilerPath ?? String.Empty;
			set
			{
				if (!String.Equals(this.Draft.Compiler.ExternalDecompilerPath ?? String.Empty, value, StringComparison.Ordinal))
				{
					this.Draft.Compiler.ExternalDecompilerPath = value;
					this.OnPropertyChanged();
				}
			}
		}

		[RelayCommand]
		private void BrowseExternalDecompiler()
		{
			String? initial = ExistingDirectory(Path.GetDirectoryName(this.ExternalDecompilerPath)) ?? ExistingDirectory(this._settingsService.Paths.ResourcesDirectory);
			String? file = this._dialogService.PickOpenFile(Loc.Get("Str.ToolWindows.Settings.SelectInt2ssl"), CreateFilter("Str.ToolWindows.Filter.ExecutableFiles", "*.exe"), initial);
			if (file != null)
			{
				this.ExternalDecompilerPath = file;
			}
		}

		[RelayCommand]
		private void ClearExternalDecompilerPath()
		{
			this.ExternalDecompilerPath = String.Empty;
		}
	}
}
