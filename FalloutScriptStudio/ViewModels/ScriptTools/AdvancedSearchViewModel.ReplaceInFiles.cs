namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using Core.Compilation;
	using Core.Configuration;
	using Documents;
	using Localization;
	using Services.ScriptTools.Replace;
	using Services.ScriptTools.Search;

	/// <summary>"Replace in Files": the replacement over the current script, the open scripts or a folder, with a preview and a choice of the replacements.</summary>
	public sealed partial class AdvancedSearchViewModel
	{
		/// <summary>Gets a value indicating whether "Replace in Files" is available (the service that runs it was supplied).</summary>
		public Boolean CanReplaceInFiles => this._replaceInFiles is not null;

		/// <summary>Chooses the folder scope when a folder to work on is known: the entry point of the command Replace in Files.</summary>
		public void PrepareReplaceInFiles()
		{
			if (this.Scope != SearchScope.Folder && Directory.Exists(this.FolderPath.Trim()))
			{
				this.Scope = SearchScope.Folder;
			}
		}

		[RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanReplaceInFiles))]
		private async Task ReplaceInFilesAsync(CancellationToken cancellationToken)
		{
			if (this._replaceInFiles is null || !this.TryCreateMatcher(out SearchMatcher? matcher) || matcher is null)
			{
				return;
			}

			ReplaceInFilesRequest? request = this.BuildReplaceRequest(matcher);
			if (request is null)
			{
				return;
			}

			this.IsSearching = true;
			this.Results = Array.Empty<SearchResultItem>();
			this.ProgressValue = 0;
			this.ProgressMaximum = 1;
			this.StatusText = Loc.Get("Str.ScriptTools.SearchSearching");
			Progress<SearchProgress> progress = new(this.OnProgress);
			try
			{
				ReplaceOutcome outcome = await this._replaceInFiles.RunAsync(request, progress, cancellationToken);
				this.StatusText = outcome.Kind switch
				{
					ReplaceOutcomeKind.NothingFound => Loc.Get("Str.ScriptTools.SearchNotFound"),
					ReplaceOutcomeKind.Cancelled => Loc.Get("Str.ScriptTools.ReplCancelled"),
					_ => DescribeResult(outcome.Result),
				};
			}
			catch (OperationCanceledException)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchCancelled");
			}
			catch (RegexMatchTimeoutException)
			{
				this.StatusText = String.Empty;
				this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrRegexTimeout");
			}
			finally
			{
				this.IsSearching = false;
			}
		}

		[RelayCommand]
		private void CancelRunning()
		{
			// The Cancel button stops whichever run is going: the search, or the search and replacement of "Replace in Files".
			if (this.FindAllCancelCommand.CanExecute(null))
			{
				this.FindAllCancelCommand.Execute(null);
			}

			if (this.ReplaceInFilesCancelCommand.CanExecute(null))
			{
				this.ReplaceInFilesCancelCommand.Execute(null);
			}
		}

		private static String DescribeResult(ReplaceResult? result)
		{
			if (result is null)
			{
				return String.Empty;
			}

			String text = result.Replacements == 0 && result.Failures.Count == 0
				? Loc.Get("Str.ScriptTools.ReplNothingReplaced")
				: Loc.Format("Str.ScriptTools.ReplDone", result.Replacements, result.FilesChanged);
			return result.Failures.Count == 0 ? text : text + " " + Loc.Format("Str.ScriptTools.ReplDoneFailed", result.Failures.Count);
		}

		private ReplaceInFilesRequest? BuildReplaceRequest(SearchMatcher matcher)
		{
			System.Text.Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			switch (this.Scope)
			{
				case SearchScope.CurrentScript:
					if (this._workspace.ActiveDocument is not { } active)
					{
						this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoScript");
						return null;
					}

					return new ReplaceInFilesRequest
					{
						Matcher = matcher,
						Replacement = this.ReplaceText,
						Sources = new[] { ToReplaceSource(active) },
						FallbackEncoding = fallback,
					};

				case SearchScope.OpenScripts:
					List<ReplaceTextSource> sources = this._workspace.Documents.Select(ToReplaceSource).ToList();
					if (sources.Count == 0)
					{
						this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoOpenScripts");
						return null;
					}

					return new ReplaceInFilesRequest
					{
						Matcher = matcher,
						Replacement = this.ReplaceText,
						Sources = sources,
						FallbackEncoding = fallback,
					};

				default:
					String path = this.FolderPath.Trim();
					if (path.Length == 0 || !Directory.Exists(path))
					{
						this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoPath");
						return null;
					}

					String fullPath = Path.GetFullPath(path);
					this.RememberFolder(fullPath);
					Dictionary<String, ReplaceTextSource> open = new(StringComparer.OrdinalIgnoreCase);
					foreach (DocumentViewModel document in this._workspace.Documents.Where(document => document.FilePath is not null))
					{
						open[Path.GetFullPath(document.FilePath!)] = ToReplaceSource(document);
					}

					return new ReplaceInFilesRequest
					{
						Matcher = matcher,
						Replacement = this.ReplaceText,
						Folder = new SearchFolder(fullPath, SearchFileMasks.Parse(this.FileMask), this.IncludeSubfolders, new Dictionary<String, String>()),
						OpenDocuments = open,
						FallbackEncoding = fallback,
					};
			}
		}

		private static ReplaceTextSource ToReplaceSource(DocumentViewModel document)
		{
			return new ReplaceTextSource(document.FilePath ?? BuildError.NoFile, document.TextDocument.Text, document);
		}
	}
}