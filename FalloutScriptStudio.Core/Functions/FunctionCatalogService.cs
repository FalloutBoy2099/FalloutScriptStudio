namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>
	/// <see cref="IFunctionCatalogService"/> working on the two ini files.
	/// </summary>
	public sealed class FunctionCatalogService : IFunctionCatalogService, IDisposable
	{
		/// <summary>The content of a newly created user functions file.</summary>
		public static readonly String UserFileTemplate = String.Join(
			"\r\n",
			"// The file contains a description of the user functions.",
			"//",
			"// Structure:",
			"// \"function name\"<d>\"short description of the function\"<s>\"code that will be inserted into the script\"<cr>",
			"// <m> \"node name menu\"",
			"// <m+> \"name for sub node\"",
			"// <m-> close menu node.",
			"// <-> delimiter (only for the top level)",
			"// <cr> carriage return to a new line (optional)",
			"//",
			"// In the \"Menu Node\", \"function name\", \"short description of the function\" and",
			"// \"code that will be inserted into the script\" can contain any characters, except control characters.",
			"//",
			"<m>User defined macros",
			"DEBUGMSG<d>Macros of debugging messages with conditional compilation 'DEBUG' (this example)<s>DEBUGMSG({string})<cr>",
			"<m+>Example node",
			"Macros<d>Macros descriptions.<s>Macros({example})<cr>",
			"<m->",
			String.Empty);

		private static readonly UTF8Encoding _utf8 = new UTF8Encoding(false);

		private readonly FunctionCatalogPaths _paths;
		private readonly IUserKeywordStore? _keywords;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
		private FunctionCatalog _catalog = FunctionCatalog.Empty;

		/// <summary>
		/// Initializes a new instance of the <see cref="FunctionCatalogService"/> class.
		/// </summary>
		/// <param name="paths">The ini files.</param>
		/// <param name="keywords">The user keyword store kept in sync with user functions; may be <c>null</c>.</param>
		public FunctionCatalogService(FunctionCatalogPaths paths, IUserKeywordStore? keywords = null)
		{
			ArgumentNullException.ThrowIfNull(paths);
			this._paths = paths;
			this._keywords = keywords;
		}

		/// <inheritdoc />
		public event EventHandler? CatalogChanged;

		/// <inheritdoc />
		public FunctionCatalog Catalog => Volatile.Read(ref this._catalog);

		/// <inheritdoc />
		public async Task<FunctionCatalog> LoadAsync(CancellationToken cancellationToken = default)
		{
			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				FunctionCatalog catalog = await this.ReadCatalogAsync(cancellationToken).ConfigureAwait(false);
				Volatile.Write(ref this._catalog, catalog);
			}
			finally
			{
				this._gate.Release();
			}

			this.CatalogChanged?.Invoke(this, EventArgs.Empty);
			return this.Catalog;
		}

		/// <inheritdoc />
		public Boolean CanAddFunction(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return node.IsUser && node.Kind != FunctionNodeKind.Separator;
		}

		/// <inheritdoc />
		public Boolean CanEditFunction(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return node.Kind == FunctionNodeKind.Function;
		}

		/// <inheritdoc />
		public Boolean CanAddCategory(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return node.IsUser && node.Kind == FunctionNodeKind.Category && node.Depth < FunctionCatalog.MaxCategoryDepth;
		}

		/// <inheritdoc />
		public Boolean CanRenameCategory(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			return node.IsUser && node.Kind == FunctionNodeKind.Category;
		}

		/// <inheritdoc />
		public Boolean CanDelete(FunctionNode node)
		{
			ArgumentNullException.ThrowIfNull(node);
			if (!node.IsUser)
			{
				return false;
			}

			return node.Kind == FunctionNodeKind.Function
				|| (node.Kind == FunctionNodeKind.Category && node.Depth > 0 && node.Children.Count == 0);
		}

		/// <inheritdoc />
		public async Task<FunctionOperationResult> AddFunctionAsync(FunctionNode target, FunctionDefinition definition, Boolean highlightKeyword = false, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(target);
			ArgumentNullException.ThrowIfNull(definition);

			if (!this.CanAddFunction(target))
			{
				return FunctionOperationResult.Fail(FunctionOperationError.NotAllowed, "Functions can only be added to the user's categories.");
			}

			if (!definition.TryValidate(out String? error))
			{
				return FunctionOperationResult.Fail(FunctionOperationError.InvalidDefinition, error ?? "The function is not valid.");
			}

			FunctionOperationResult result = await this.ModifyAsync(
				target.Source,
				lines =>
				{
					if (!IsAt(lines, target))
					{
						return NodeChanged();
					}

					lines.Insert(target.SourceLine + 1, definition.ToIniLine());
					return FunctionOperationResult.Success;
				},
				cancellationToken).ConfigureAwait(false);

			if (result.Succeeded && highlightKeyword && this._keywords is not null)
			{
				String keyword = FunctionKeyword.Extract(definition.Code);
				if (keyword.Length > 0)
				{
					await this._keywords.AddAsync(keyword, cancellationToken).ConfigureAwait(false);
				}
			}

			return result;
		}

		/// <inheritdoc />
		public Task<FunctionOperationResult> EditFunctionAsync(FunctionNode node, FunctionDefinition definition, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(node);
			ArgumentNullException.ThrowIfNull(definition);

			if (!this.CanEditFunction(node))
			{
				return Task.FromResult(FunctionOperationResult.Fail(FunctionOperationError.NotAllowed, "The node is not a function."));
			}

			if (!definition.TryValidate(out String? error))
			{
				return Task.FromResult(FunctionOperationResult.Fail(FunctionOperationError.InvalidDefinition, error ?? "The function is not valid."));
			}

			return this.ModifyAsync(
				node.Source,
				lines =>
				{
					if (!IsAt(lines, node))
					{
						return NodeChanged();
					}

					lines[node.SourceLine] = LeadingWhiteSpace(lines[node.SourceLine]) + definition.ToIniLine();
					return FunctionOperationResult.Success;
				},
				cancellationToken);
		}

		/// <inheritdoc />
		public Task<FunctionOperationResult> AddCategoryAsync(FunctionNode? parent, String name, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(name);

			String title = SingleLine(name);
			if (title.Length == 0)
			{
				return Task.FromResult(FunctionOperationResult.Fail(FunctionOperationError.InvalidDefinition, "The title is empty."));
			}

			if (parent is not null && !this.CanAddCategory(parent))
			{
				return Task.FromResult(FunctionOperationResult.Fail(FunctionOperationError.NotAllowed, "A category cannot be added to that node."));
			}

			return this.ModifyAsync(
				FunctionSource.User,
				lines =>
				{
					if (parent is null)
					{
						Int32 index = lines.Count > 0 && lines[lines.Count - 1].Length == 0 ? lines.Count - 1 : lines.Count;
						lines.Insert(index, FunctionCatalogParser.CategoryMarker + title);
						return FunctionOperationResult.Success;
					}

					if (!IsAt(lines, parent))
					{
						return NodeChanged();
					}

					Boolean closed = parent.Depth > 0 && parent.LastLine < lines.Count
						&& lines[parent.LastLine].TrimStart().StartsWith(FunctionCatalogParser.EndCategoryMarker, StringComparison.Ordinal);
					Int32 position = closed ? parent.LastLine : parent.LastLine + 1;
					lines.Insert(position, FunctionCatalogParser.SubCategoryMarker + title);
					lines.Insert(position + 1, FunctionCatalogParser.EndCategoryMarker);
					return FunctionOperationResult.Success;
				},
				cancellationToken);
		}

		/// <inheritdoc />
		public Task<FunctionOperationResult> RenameCategoryAsync(FunctionNode category, String newName, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(category);
			ArgumentNullException.ThrowIfNull(newName);

			if (!this.CanRenameCategory(category))
			{
				return Task.FromResult(FunctionOperationResult.Fail(FunctionOperationError.NotAllowed, "Only the user's categories can be renamed."));
			}

			String title = SingleLine(newName);
			if (title.Length == 0)
			{
				return Task.FromResult(FunctionOperationResult.Fail(FunctionOperationError.InvalidDefinition, "The title is empty."));
			}

			return this.ModifyAsync(
				FunctionSource.User,
				lines =>
				{
					if (!IsAt(lines, category))
					{
						return NodeChanged();
					}

					String marker = category.Depth == 0 ? FunctionCatalogParser.CategoryMarker : FunctionCatalogParser.SubCategoryMarker;
					lines[category.SourceLine] = LeadingWhiteSpace(lines[category.SourceLine]) + marker + title;
					return FunctionOperationResult.Success;
				},
				cancellationToken);
		}

		/// <inheritdoc />
		public async Task<FunctionOperationResult> DeleteAsync(FunctionNode node, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(node);

			if (!this.CanDelete(node))
			{
				return FunctionOperationResult.Fail(FunctionOperationError.NotAllowed, "The node cannot be deleted.");
			}

			FunctionOperationResult result = await this.ModifyAsync(
				node.Source,
				lines =>
				{
					if (!IsAt(lines, node) || node.LastLine >= lines.Count)
					{
						return NodeChanged();
					}

					lines.RemoveRange(node.SourceLine, node.LastLine - node.SourceLine + 1);
					return FunctionOperationResult.Success;
				},
				cancellationToken).ConfigureAwait(false);

			if (result.Succeeded && node.Kind == FunctionNodeKind.Function && this._keywords is not null)
			{
				String keyword = FunctionKeyword.Extract(node.Code);
				if (keyword.Length > 0)
				{
					await this._keywords.RemoveAsync(keyword, cancellationToken).ConfigureAwait(false);
				}
			}

			return result;
		}

		private static Boolean IsAt(List<String> lines, FunctionNode node)
		{
			return node.SourceLine >= 0 && node.SourceLine < lines.Count && String.Equals(lines[node.SourceLine].Trim(), node.RawLine, StringComparison.Ordinal);
		}

		private static FunctionOperationResult NodeChanged()
		{
			return FunctionOperationResult.Fail(FunctionOperationError.NodeChanged, "The file was changed after the tree was loaded; reload the functions.");
		}

		private static String SingleLine(String text)
		{
			return text.Replace("\r\n", " ", StringComparison.Ordinal).Replace('\n', ' ').Replace('\r', ' ').Trim();
		}

		private static String LeadingWhiteSpace(String line)
		{
			return line.Substring(0, line.Length - line.TrimStart().Length);
		}

		private static List<String> SplitLines(String text)
		{
			return text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
		}

		private async Task<FunctionOperationResult> ModifyAsync(FunctionSource source, Func<List<String>, FunctionOperationResult> change, CancellationToken cancellationToken)
		{
			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				String path = source == FunctionSource.User ? this._paths.UserFile : this._paths.BuiltInFile;
				String text = await ReadFileTextAsync(path, source == FunctionSource.User, cancellationToken).ConfigureAwait(false);
				String newLine = text.Contains("\r\n", StringComparison.Ordinal) || !text.Contains('\n', StringComparison.Ordinal) ? "\r\n" : "\n";
				List<String> lines = SplitLines(text);

				FunctionOperationResult result = change(lines);
				if (!result.Succeeded)
				{
					return result;
				}

				await AtomicFile.WriteAllBytesAsync(path, _utf8.GetBytes(String.Join(newLine, lines)), cancellationToken).ConfigureAwait(false);
				Volatile.Write(ref this._catalog, await this.ReadCatalogAsync(cancellationToken).ConfigureAwait(false));
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return FunctionOperationResult.Fail(FunctionOperationError.IoError, ex.Message);
			}
			finally
			{
				this._gate.Release();
			}

			this.CatalogChanged?.Invoke(this, EventArgs.Empty);
			return FunctionOperationResult.Success;
		}

		private static async Task<String> ReadFileTextAsync(String path, Boolean createFromTemplate, CancellationToken cancellationToken)
		{
			if (!File.Exists(path))
			{
				if (!createFromTemplate)
				{
					throw new FileNotFoundException("The functions file was not found.", path);
				}

				await AtomicFile.WriteAllBytesAsync(path, _utf8.GetBytes(UserFileTemplate), cancellationToken).ConfigureAwait(false);
			}

			return await File.ReadAllTextAsync(path, _utf8, cancellationToken).ConfigureAwait(false);
		}

		private async Task<FunctionCatalog> ReadCatalogAsync(CancellationToken cancellationToken)
		{
			var roots = new List<FunctionNode>();
			var issues = new List<FunctionCatalogIssue>();

			foreach (FunctionSource source in new[] { FunctionSource.BuiltIn, FunctionSource.User })
			{
				String path = source == FunctionSource.User ? this._paths.UserFile : this._paths.BuiltInFile;
				String text;
				try
				{
					text = await ReadFileTextAsync(path, source == FunctionSource.User, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					issues.Add(new FunctionCatalogIssue(source, 0, "The file could not be read: " + ex.Message));
					continue;
				}

				ParsedFunctionFile parsed = FunctionCatalogParser.Parse(SplitLines(text), source);
				roots.AddRange(parsed.Roots);
				issues.AddRange(parsed.Issues);
			}

			return new FunctionCatalog(roots, issues);
		}

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}
