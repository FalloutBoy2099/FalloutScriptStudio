namespace FalloutScriptStudio.Problems
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.ToolWindows;

	/// <summary>
	/// Creates the <see cref="Problem"/> of every known failure, with its message, its fix hint and (when there is an obvious one) its
	/// action. The texts are in <c>Str.Shell.Problem.*</c>.
	/// </summary>
	public static class ProblemFactory
	{
		private const String KeyPrefix = "Str.Shell.Problem.";

		/// <summary>An external tool of the build that is missing or cannot be started.</summary>
		/// <param name="failure">The failure reported by the compiler service.</param>
		/// <returns>The problem.</returns>
		public static Problem FromToolFailure(ToolFailure failure)
		{
			ArgumentNullException.ThrowIfNull(failure);
			String folder = Path.GetDirectoryName(failure.ToolPath) ?? String.Empty;
			Boolean missing = failure.Kind == ToolFailureKind.NotFound;
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Tool,
				MessageKey = KeyPrefix + (missing ? "ToolNotFound" : "ToolStartFailed"),
				MessageArguments = missing ? new Object?[] { failure.ToolName, failure.ToolPath } : new Object?[] { failure.ToolName, failure.Detail },
				HintKey = KeyPrefix + (missing ? "ToolNotFoundHint" : "ToolStartFailedHint"),
				HintArguments = missing ? new Object?[] { failure.ToolName, folder } : new Object?[] { failure.ToolName },
				Action = ProblemAction.OpenSettings(SettingsPage.Compiler),
				Origin = ProblemOrigins.BuildTool,
			};
		}

		/// <summary>A compilation or preprocessing that failed without telling where (a user batch file, for example).</summary>
		/// <param name="script">The script.</param>
		/// <param name="preprocessing">Whether the script was only preprocessed.</param>
		/// <param name="owner">The document of the script, if it is open.</param>
		/// <returns>The problem.</returns>
		public static Problem BuildFailedWithoutDetails(String script, Boolean preprocessing, Object? owner)
		{
			ArgumentException.ThrowIfNullOrEmpty(script);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Build,
				MessageKey = KeyPrefix + (preprocessing ? "PreprocessFailed" : "CompileFailedNoDetails"),
				MessageArguments = new Object?[] { Path.GetFileName(script) },
				HintKey = KeyPrefix + "SeeBuildLogHint",
				File = script,
				Owner = owner,
			};
		}

		/// <summary>The folder for compiled scripts is not set.</summary>
		/// <param name="owner">The document that was being compiled.</param>
		/// <returns>The problem.</returns>
		public static Problem OutputFolderNotSet(Object? owner)
		{
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Settings,
				MessageKey = KeyPrefix + "OutputFolderNotSet",
				HintKey = KeyPrefix + "OutputFolderNotSetHint",
				Action = ProblemAction.OpenSettings(SettingsPage.Paths),
				Owner = owner,
			};
		}

		/// <summary>A document that is not an SSL script was sent to the compiler.</summary>
		/// <param name="fileName">The name of the document.</param>
		/// <param name="owner">The document.</param>
		/// <returns>The problem.</returns>
		public static Problem NotAScript(String fileName, Object? owner)
		{
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Build,
				MessageKey = KeyPrefix + "NotAScript",
				MessageArguments = new Object?[] { fileName },
				HintKey = KeyPrefix + "NotAScriptHint",
				Owner = owner,
			};
		}

		/// <summary>A file that could not be written.</summary>
		/// <param name="path">The file.</param>
		/// <param name="exception">What the system reported.</param>
		/// <param name="owner">The document, if the file is an open document.</param>
		/// <returns>The problem.</returns>
		public static Problem SaveFailed(String path, Exception exception, Object? owner)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(exception);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "SaveFailed",
				MessageArguments = new Object?[] { Path.GetFileName(path), exception.Message },
				HintKey = KeyPrefix + (IsReadOnly(path) ? "SaveFailedReadOnlyHint" : "SaveFailedHint"),
				File = path,
				Owner = owner,
				Origin = ProblemOrigins.Save,
			};
		}

		/// <summary>A file that could not be read.</summary>
		/// <param name="path">The file.</param>
		/// <param name="exception">What the system reported.</param>
		/// <returns>The problem.</returns>
		public static Problem OpenFailed(String path, Exception exception)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(exception);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "OpenFailed",
				MessageArguments = new Object?[] { Path.GetFileName(path), exception.Message },
				HintKey = KeyPrefix + "OpenFailedHint",
				File = path,
			};
		}

		/// <summary>A file that does not exist.</summary>
		/// <param name="path">The file.</param>
		/// <returns>The problem.</returns>
		public static Problem FileNotFound(String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "FileNotFound",
				MessageArguments = new Object?[] { path },
				HintKey = KeyPrefix + "FileNotFoundHint",
				File = path,
			};
		}

		/// <summary>A file that was not valid UTF-8 and was read with the code page of the settings.</summary>
		/// <param name="path">The file.</param>
		/// <param name="encodingName">The name of the encoding it was read with.</param>
		/// <param name="owner">The document.</param>
		/// <returns>The problem.</returns>
		public static Problem EncodingFallback(String path, String encodingName, Object owner)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(owner);
			return new Problem
			{
				Severity = ProblemSeverity.Info,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "EncodingFallback",
				MessageArguments = new Object?[] { Path.GetFileName(path), encodingName },
				HintKey = KeyPrefix + "EncodingFallbackHint",
				Action = ProblemAction.OpenSettings(SettingsPage.Editor),
				File = path,
				Owner = owner,
				Origin = ProblemOrigins.Encoding,
			};
		}

		/// <summary>A compiled script that could not be decompiled.</summary>
		/// <param name="path">The <c>.int</c> file.</param>
		/// <returns>The problem.</returns>
		public static Problem DecompileFailed(String path)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Tool,
				MessageKey = KeyPrefix + "DecompileFailed",
				MessageArguments = new Object?[] { Path.GetFileName(path) },
				HintKey = KeyPrefix + "DecompileFailedHint",
				Action = ProblemAction.OpenSettings(SettingsPage.Compiler),
				File = path,
			};
		}

		/// <summary>A script that could not be extracted from a game archive.</summary>
		/// <param name="scriptFileName">The script.</param>
		/// <param name="reason">The reason given by the extractor.</param>
		/// <returns>The problem.</returns>
		public static Problem ExtractFailed(String scriptFileName, String reason)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptFileName);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "ExtractFailed",
				MessageArguments = new Object?[] { scriptFileName, reason },
				HintKey = KeyPrefix + "ExtractFailedHint",
			};
		}

		/// <summary>An <c>#include</c> whose file does not exist.</summary>
		/// <param name="includePath">The path of the include as resolved.</param>
		/// <param name="fromFile">The script with the include, if it has a path.</param>
		/// <param name="owner">The document of the script.</param>
		/// <returns>The problem.</returns>
		public static Problem IncludeNotFound(String includePath, String? fromFile, Object? owner)
		{
			ArgumentException.ThrowIfNullOrEmpty(includePath);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "IncludeNotFound",
				MessageArguments = new Object?[] { includePath },
				HintKey = KeyPrefix + "IncludeNotFoundHint",
				Action = ProblemAction.OpenSettings(SettingsPage.Paths),
				File = fromFile,
				Owner = owner,
			};
		}

		/// <summary>A script that has no message file.</summary>
		/// <param name="scriptPath">The script.</param>
		/// <param name="owner">The document of the script.</param>
		/// <returns>The problem.</returns>
		public static Problem MessageFileMissing(String scriptPath, Object? owner)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			return new Problem
			{
				Severity = ProblemSeverity.Warning,
				Source = ProblemSource.Message,
				MessageKey = KeyPrefix + "MessageFileMissing",
				MessageArguments = new Object?[] { Path.GetFileName(scriptPath) },
				HintKey = KeyPrefix + "MessageFileMissingHint",
				Action = ProblemAction.OpenSettings(SettingsPage.Messages),
				File = scriptPath,
				Owner = owner,
			};
		}

		/// <summary>The native parser (<c>parser.dll</c>) is not in use although it is turned on.</summary>
		/// <param name="libraryExists">Whether <c>parser.dll</c> is where the settings expect it. When it is missing the built-in parser is the normal choice and the problem is only a note; when it is there the parser broke.</param>
		/// <param name="detail">The explanation given by the parser.</param>
		/// <returns>The problem.</returns>
		public static Problem NativeParser(Boolean libraryExists, String? detail)
		{
			return new Problem
			{
				Severity = libraryExists ? ProblemSeverity.Warning : ProblemSeverity.Info,
				Source = ProblemSource.Tool,
				MessageKey = KeyPrefix + (libraryExists ? "ParserBroken" : "ParserUnavailable"),
				MessageArguments = libraryExists ? new Object?[] { detail } : Array.Empty<Object?>(),
				HintKey = KeyPrefix + (libraryExists ? "ParserBrokenHint" : "ParserUnavailableHint"),
				Action = ProblemAction.OpenSettings(SettingsPage.Parser),
				Origin = ProblemOrigins.NativeParser,
			};
		}

		/// <summary>The analysis of a document failed with an unexpected exception.</summary>
		/// <param name="path">The document (path or name).</param>
		/// <param name="exception">The exception.</param>
		/// <param name="owner">The document.</param>
		/// <returns>The problem.</returns>
		public static Problem ParseFailed(String path, Exception exception, Object owner)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(exception);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Parse,
				MessageKey = KeyPrefix + "ParseFailed",
				MessageArguments = new Object?[] { Path.GetFileName(path), exception.Message },
				HintKey = KeyPrefix + "ParseFailedHint",
				File = path,
				Owner = owner,
			};
		}

		/// <summary>A problem with a file of the settings folder.</summary>
		/// <param name="problem">The problem reported by the settings service.</param>
		/// <returns>The problem.</returns>
		public static Problem FromSettings(SettingsProblem problem)
		{
			ArgumentNullException.ThrowIfNull(problem);
			String name = problem.Kind switch
			{
				SettingsProblemKind.Damaged => "SettingsDamaged",
				SettingsProblemKind.Unreadable => "SettingsUnreadable",
				_ => "SettingsSaveFailed",
			};
			String? folder = Path.GetDirectoryName(problem.Path);
			return new Problem
			{
				Severity = problem.Kind == SettingsProblemKind.Damaged ? ProblemSeverity.Warning : ProblemSeverity.Error,
				Source = ProblemSource.Settings,
				MessageKey = KeyPrefix + name,
				MessageArguments = problem.Kind == SettingsProblemKind.SaveFailed ? new Object?[] { Path.GetFileName(problem.Path), problem.Detail } : new Object?[] { Path.GetFileName(problem.Path) },
				HintKey = KeyPrefix + name + "Hint",
				Action = String.IsNullOrEmpty(folder) ? null : ProblemAction.OpenFolder(folder),
				File = problem.Path,
				Origin = ProblemOrigins.Settings,
			};
		}

		/// <summary>The external editor chosen in the settings could not be started.</summary>
		/// <param name="editor">The program.</param>
		/// <returns>The problem.</returns>
		public static Problem ExternalEditorFailed(String editor)
		{
			ArgumentException.ThrowIfNullOrEmpty(editor);
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.Tool,
				MessageKey = KeyPrefix + "ExternalEditorFailed",
				MessageArguments = new Object?[] { editor },
				HintKey = KeyPrefix + "ExternalEditorFailedHint",
				Action = ProblemAction.OpenSettings(SettingsPage.Paths),
			};
		}

		/// <summary>A background operation that failed with an unexpected exception.</summary>
		/// <param name="reason">The message of the exception.</param>
		/// <returns>The problem.</returns>
		public static Problem OperationFailed(String reason)
		{
			return new Problem
			{
				Severity = ProblemSeverity.Error,
				Source = ProblemSource.File,
				MessageKey = KeyPrefix + "OperationFailed",
				MessageArguments = new Object?[] { reason },
				HintKey = KeyPrefix + "OperationFailedHint",
			};
		}

		/// <summary>A script that was opened belongs to a project other than the active one: the hint offers to switch (it never switches by itself).</summary>
		/// <param name="projectFolder">The folder of the project that has a <c>fss-project.json</c>.</param>
		/// <returns>The problem.</returns>
		public static Problem ProjectDetected(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			return new Problem
			{
				Severity = ProblemSeverity.Info,
				Source = ProblemSource.Settings,
				MessageKey = KeyPrefix + "ProjectDetected",
				MessageArguments = new Object?[] { projectFolder },
				HintKey = KeyPrefix + "ProjectDetectedHint",
				Action = ProblemAction.SwitchProject(projectFolder),
				Origin = ProblemOrigins.ProjectDetection,
			};
		}

		/// <summary>The project file was edited into something that is not valid: the last valid settings stay in effect.</summary>
		/// <param name="path">The project file.</param>
		/// <param name="error">Where and why the parser stopped.</param>
		/// <returns>The problem.</returns>
		public static Problem ProjectFileInvalid(String path, ProjectFileError error)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(error);
			Boolean hasPosition = error.Line > 0;
			return new Problem
			{
				Severity = ProblemSeverity.Warning,
				Source = ProblemSource.Settings,
				MessageKey = KeyPrefix + (hasPosition ? "ProjectFileInvalid" : "ProjectFileInvalidNoPosition"),
				MessageArguments = hasPosition ? new Object?[] { Path.GetFileName(path), error.Message, error.Line, Math.Max(error.Column, 1) } : new Object?[] { Path.GetFileName(path), error.Message },
				HintKey = KeyPrefix + "ProjectFileInvalidHint",
				Action = ProblemAction.OpenFile(path),
				File = path,
				Line = hasPosition ? error.Line : -1,
				Column = hasPosition ? Math.Max(error.Column, 1) : -1,
				Origin = ProblemOrigins.ProjectFile,
			};
		}

		/// <summary>The project that was just opened had documents open when it was left: the hint offers to open them again (it never does so by itself when other documents are open).</summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="projectName">The name of the project.</param>
		/// <param name="count">How many documents are remembered.</param>
		/// <returns>The problem.</returns>
		public static Problem ProjectDocumentsAvailable(String projectFolder, String projectName, Int32 count)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			return new Problem
			{
				Severity = ProblemSeverity.Info,
				Source = ProblemSource.Settings,
				MessageKey = KeyPrefix + "ProjectDocumentsAvailable",
				MessageArguments = new Object?[] { projectName, count },
				HintKey = KeyPrefix + "ProjectDocumentsAvailableHint",
				Action = ProblemAction.RestoreDocuments(projectFolder),
				Origin = ProblemOrigins.ProjectSession,
			};
		}

		/// <summary>A variable that is declared but never read, or never used at all, or an argument that is never used.</summary>
		/// <param name="variable">The variable.</param>
		/// <param name="file">The script, or <see langword="null"/> when it was never saved.</param>
		/// <param name="owner">The document of the script.</param>
		/// <returns>The problem: an information of the parser, whose action removes the declaration when that is safe.</returns>
		public static Problem UnusedVariable(Core.Analysis.UnusedVariable variable, String? file, Object? owner)
		{
			ArgumentNullException.ThrowIfNull(variable);
			String suffix = variable.Kind == Core.Analysis.VariableDeclarationKind.Parameter
				? "UnusedParameter"
				: variable.Status == Core.Analysis.UnusedVariableStatus.WriteOnly ? "VariableNeverRead" : "UnusedVariable";
			return new Problem
			{
				Severity = ProblemSeverity.Info,
				Source = ProblemSource.Parse,
				MessageKey = KeyPrefix + suffix,
				MessageArguments = new Object?[] { variable.Name, variable.ProcedureName ?? String.Empty },
				HintKey = KeyPrefix + suffix + "Hint",
				File = file,
				Line = variable.Line,
				Column = variable.Column,
				Length = variable.Length,
				Action = variable.CanRemove && file is not null ? ProblemAction.RemoveUnusedVariable(file, variable.Line, variable.Column) : null,
				Owner = owner,
				Origin = ProblemOrigins.UnusedVariables,
			};
		}

		private static Boolean IsReadOnly(String path)
		{
			try
			{
				return System.IO.File.Exists(path) && (System.IO.File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return false;
			}
		}
	}
}
