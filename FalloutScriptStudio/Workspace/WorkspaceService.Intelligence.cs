namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading.Tasks;
	using Core.Parsing;
	using Documents;

	/// <summary>Navigation across the files of the project, the "Go to symbol" list and the refactorings that use the project index.</summary>
	public sealed partial class WorkspaceService
	{
		public Boolean HasProjectSymbol(String name)
		{
			return this._symbols is not null && this._symbols.HasSymbol(name);
		}

		public Task NavigateToProjectSymbolAsync(DocumentViewModel from, String name, ProjectNavigationKind kind)
		{
			return this._intelligence is null ? Task.CompletedTask : this._intelligence.NavigateAsync(this, from, name, kind);
		}

		public Task ShowProjectReferencesAsync(DocumentViewModel from, IScriptSymbol symbol, String name)
		{
			return this._intelligence is null ? Task.CompletedTask : this._intelligence.ShowReferencesAsync(this, symbol, name);
		}

		public Task GoToSymbolAsync()
		{
			return this._intelligence is null ? Task.CompletedTask : this._intelligence.GoToSymbolAsync(this);
		}

		public Task<Boolean> DeleteUnusedProcedureAsync(DocumentViewModel document, String name)
		{
			return this._intelligence is null ? Task.FromResult(false) : this._intelligence.DeleteUnusedProcedureAsync(this, document, name);
		}

		public Task<Int32> FindUnusedVariablesAsync(DocumentViewModel document)
		{
			return this._intelligence is null ? Task.FromResult(0) : this._intelligence.FindUnusedVariablesAsync(this, document);
		}

		public Task RemoveUnusedVariableAsync(String file, Int32 line, Int32 column)
		{
			return this._intelligence is null ? Task.CompletedTask : this._intelligence.RemoveUnusedVariableAsync(this, file, line, column);
		}

		public Task ApplyQuickFixAsync(String file, Int32 line, Int32 column)
		{
			return this._intelligence is null ? Task.CompletedTask : this._intelligence.ApplyQuickFixAsync(this, file, line, column);
		}

		public Task<Boolean> ApplyQuickFixAtAsync(DocumentViewModel document, Int32 offset)
		{
			return this._intelligence is null ? Task.FromResult(false) : this._intelligence.ApplyQuickFixAtAsync(this, document, offset);
		}

		public Task<FalloutScriptStudio.Core.Snippets.Snippet?> PickSnippetAsync(FalloutScriptStudio.Services.ToolWindows.SnippetPickerRequest request)
		{
			return this._intelligence is null ? Task.FromResult<FalloutScriptStudio.Core.Snippets.Snippet?>(null) : this._intelligence.PickSnippetAsync(request);
		}

		public Task ExtractProcedureAsync(DocumentViewModel document)
		{
			return this._intelligence is null ? Task.CompletedTask : this._intelligence.ExtractProcedureAsync(this, document);
		}
	}
}