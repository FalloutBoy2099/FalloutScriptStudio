namespace FalloutScriptStudio.Workspace.Help
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Core.Configuration;
	using Core.Functions;
	using Core.Help;
	using Core.IO;
	using Core.Parsing;
	using Documents;
	using Services;

	/// <summary>
	/// Default <see cref="IFunctionHelpService"/>. The catalog of the built-in topics is made from <c>opcodes.txt</c> and the functions tree on
	/// first use and again when the tree is edited or <c>opcodes.txt</c> changes; the comment of a macro or a procedure of the project is
	/// read from the open document or from the file (the last few files are remembered while their stamp is unchanged).
	/// </summary>
	public sealed class FunctionHelpService : IFunctionHelpService, IDisposable
	{
		private const Int32 MaxCachedFiles = 16;
		private static readonly TimeSpan _defaultCheckInterval = TimeSpan.FromSeconds(1);

		private readonly ISettingsService _settings;
		private readonly IFunctionCatalogService? _functions;
		private readonly IShellService _shell;
		private readonly IWorkspace? _workspace;
		private readonly TimeSpan _checkInterval;
		private readonly Object _gate = new();
		private readonly Dictionary<String, CachedFile> _files = new(StringComparer.OrdinalIgnoreCase);
		private FunctionHelpCatalog? _catalog;
		private Int64 _opcodesStamp;
		private DateTime _checkedAt = DateTime.MinValue;

		/// <summary>Initializes the service.</summary>
		/// <param name="settings">The settings (the folder of the description files, the code page of the scripts).</param>
		/// <param name="functions">The functions tree; the topics of the description files only when <see langword="null"/>.</param>
		/// <param name="shell">Opens the documentation in the browser.</param>
		/// <param name="workspace">The open documents, whose unsaved text is read before the files; may be <see langword="null"/>.</param>
		/// <param name="checkInterval">How often the stamp of <c>opcodes.txt</c> is looked at; one second when <see langword="null"/>.</param>
		public FunctionHelpService(ISettingsService settings, IFunctionCatalogService? functions, IShellService shell, IWorkspace? workspace = null, TimeSpan? checkInterval = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(shell);
			this._settings = settings;
			this._functions = functions;
			this._shell = shell;
			this._workspace = workspace;
			this._checkInterval = checkInterval ?? _defaultCheckInterval;
			if (functions is not null)
			{
				functions.CatalogChanged += this.OnCatalogChanged;
			}
		}

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public FunctionHelpCatalog Catalog
		{
			get
			{
				lock (this._gate)
				{
					DateTime now = DateTime.UtcNow;
					if (this._catalog is not null && now - this._checkedAt < _checkInterval)
					{
						return this._catalog;
					}

					this._checkedAt = now;
					Int64 stamp = GetStamp(this.GetOpcodesPath());
					if (this._catalog is null || stamp != this._opcodesStamp)
					{
						this._catalog = this.CreateCatalog();
						this._opcodesStamp = stamp;
					}

					return this._catalog;
				}
			}
		}

		/// <inheritdoc />
		public HelpTopic? Find(String word, ProgramInfo? info, String? file, Int32 line)
		{
			ArgumentNullException.ThrowIfNull(word);
			if (word.Length == 0)
			{
				return null;
			}

			if (this.Catalog.TryGet(word, out HelpTopic? topic))
			{
				return topic;
			}

			IScriptSymbol? symbol = info?.Lookup(word, file ?? String.Empty, line);
			return symbol switch
			{
				ScriptMacro macro => ProjectHelp.FromMacro(macro, this.ReadComment(macro.DeclaredFile, macro.DeclaredLine)),
				ScriptProcedure procedure => ProjectHelp.FromProcedure(procedure, this.ReadProcedureComment(procedure)),
				ScriptVariable variable => ProjectHelp.FromVariable(variable),
				_ => null,
			};
		}

		/// <inheritdoc />
		public IReadOnlyList<HelpTopic> Search(String? filter, Int32 maxResults)
		{
			return this.Catalog.Search(filter, maxResults);
		}

		/// <inheritdoc />
		public Uri? OpenDocumentation(HelpTopic topic)
		{
			ArgumentNullException.ThrowIfNull(topic);

			Uri? uri = SfallDocumentation.GetUri(topic);
			if (uri is null || !SfallDocumentation.IsAllowed(uri))
			{
				return null;
			}

			return this._shell.OpenWithShell(uri.AbsoluteUri) ? uri : null;
		}

		/// <summary>Stops following the functions tree.</summary>
		public void Dispose()
		{
			if (this._functions is not null)
			{
				this._functions.CatalogChanged -= this.OnCatalogChanged;
			}
		}

		private static Int64 GetStamp(String path)
		{
			try
			{
				FileInfo info = new(path);
				return info.Exists ? info.LastWriteTimeUtc.Ticks ^ info.Length : 0;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return 0;
			}
		}

		private String GetOpcodesPath()
		{
			return Path.Combine(this._settings.Paths.DescriptionsDirectory, "opcodes.txt");
		}

		private FunctionHelpCatalog CreateCatalog()
		{
			String path = this.GetOpcodesPath();
			String[] lines = Array.Empty<String>();
			try
			{
				if (File.Exists(path))
				{
					lines = DocumentTextCodec.Decode(SharedFile.ReadAllBytes(path), this._settings.Settings.Editor.FileEncoding.ToEncoding()).Text.Split('\n');
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// A description file that cannot be read gives fewer topics, never a failure.
			}

			return FunctionHelpCatalog.Create(lines, this._functions?.Catalog ?? FunctionCatalog.Empty);
		}

		private void OnCatalogChanged(Object? sender, EventArgs e)
		{
			lock (this._gate)
			{
				this._catalog = null;
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		private String? ReadProcedureComment(ScriptProcedure procedure)
		{
			String? comment = procedure.StartLine > 0 ? this.ReadComment(procedure.StartFile, procedure.StartLine) : null;
			if (comment is null && procedure.DeclaredLine > 0 && procedure.DeclaredLine != procedure.StartLine)
			{
				comment = this.ReadComment(procedure.DeclaredFile, procedure.DeclaredLine);
			}

			return comment;
		}

		private String? ReadComment(String file, Int32 line)
		{
			if (String.IsNullOrEmpty(file) || line < 2)
			{
				return null;
			}

			String? text = this.ReadText(file);
			return text is null ? null : ProjectHelp.ReadLeadingComment(text, line);
		}

		private String? ReadText(String file)
		{
			if (this._workspace is not null && this._workspace.GetOpenDocumentTexts().TryGetValue(file, out String? open))
			{
				return open;
			}

			try
			{
				FileInfo info = new(file);
				if (!info.Exists)
				{
					return null;
				}

				lock (this._gate)
				{
					if (this._files.TryGetValue(file, out CachedFile? cached) && cached.Length == info.Length && cached.LastWriteTicks == info.LastWriteTimeUtc.Ticks)
					{
						return cached.Text;
					}
				}

				String text = DocumentTextCodec.Decode(SharedFile.ReadAllBytes(file), this._settings.Settings.Editor.FileEncoding.ToEncoding()).Text;
				lock (this._gate)
				{
					if (this._files.Count >= MaxCachedFiles)
					{
						this._files.Remove(this._files.Keys.First());
					}

					this._files[file] = new CachedFile(info.Length, info.LastWriteTimeUtc.Ticks, text);
				}

				return text;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				return null;
			}
		}

		private sealed record CachedFile(Int64 Length, Int64 LastWriteTicks, String Text);
	}
}