namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;

	/// <summary>A script tool service with a scripted procedure prompt answer.</summary>
	internal sealed class FakeScriptToolService : IScriptToolService
	{
		public ProcedureNameResult? ProcedureAnswer { get; set; }

		/// <summary>Gets the answers given one after the other before <see cref="ProcedureAnswer"/> is used.</summary>
		public Queue<ProcedureNameResult?> ProcedureAnswers { get; } = new Queue<ProcedureNameResult?>();

		/// <summary>Gets every request of the procedure prompt.</summary>
		public List<ProcedureNameRequest> Requests { get; } = [];

		public ProcedureNameRequest? LastRequest { get; private set; }

		public List<String> Calls { get; } = [];

		public Task<String?> PromptProcedureNameAsync(String initialName, ProcedureNameDialogMode mode, IReadOnlyCollection<String> existingNames)
		{
			return Task.FromResult<String?>(this.ProcedureAnswer?.Name);
		}

		public Task<ProcedureNameResult?> PromptProcedureAsync(ProcedureNameRequest request)
		{
			this.LastRequest = request;
			this.Requests.Add(request);
			return Task.FromResult(this.ProcedureAnswers.Count > 0 ? this.ProcedureAnswers.Dequeue() : this.ProcedureAnswer);
		}

		public Task ShowRegisterScriptAsync(DocumentViewModel? script)
		{
			this.Calls.Add("register:" + (script?.FileName ?? "<none>"));
			return Task.CompletedTask;
		}

		public Task ShowAdvancedSearchAsync(String? initialText)
		{
			this.Calls.Add("search:" + initialText);
			return Task.CompletedTask;
		}

		public Task ShowReplaceInFilesAsync(String? initialText)
		{
			this.Calls.Add("replaceInFiles:" + initialText);
			return Task.CompletedTask;
		}

		public Task UndoReplaceInFilesAsync()
		{
			this.Calls.Add("undoReplaceInFiles");
			return Task.CompletedTask;
		}

		/// <summary>Gets or sets a value indicating whether the last Replace in Files can be undone; call <see cref="RaiseUndoReplaceInFilesStateChanged"/> after changing it.</summary>
		public Boolean CanUndoReplaceInFiles { get; set; }

		public event EventHandler? UndoReplaceInFilesStateChanged;

		/// <summary>Raises <see cref="UndoReplaceInFilesStateChanged"/>.</summary>
		public void RaiseUndoReplaceInFilesStateChanged()
		{
			this.UndoReplaceInFilesStateChanged?.Invoke(this, EventArgs.Empty);
		}

		public Task ShowFlowchartAsync(DocumentViewModel script)
		{
			this.Calls.Add("flowchart:" + script.FileName);
			return Task.CompletedTask;
		}

		public Task ShowDialoguePreviewAsync(DocumentViewModel script)
		{
			this.Calls.Add("preview:" + script.FileName);
			return Task.CompletedTask;
		}

		/// <summary>Gets every request to open the dialogue preview with options.</summary>
		public List<(DocumentViewModel Script, DialoguePreviewOpenOptions Options)> PreviewRequests { get; } = [];

		public Task ShowDialoguePreviewAsync(DocumentViewModel script, DialoguePreviewOpenOptions options)
		{
			this.PreviewRequests.Add((script, options));
			this.Calls.Add("preview:" + script.FileName + "@" + options.StartAtNode);
			return Task.CompletedTask;
		}

		public Task ShowFunctionRulesAsync()
		{
			this.Calls.Add("rules");
			return Task.CompletedTask;
		}

		public Task CloseToolWindowsAsync(DocumentViewModel script)
		{
			this.Calls.Add("closeWindows:" + script.FileName);
			return Task.CompletedTask;
		}
	}
}