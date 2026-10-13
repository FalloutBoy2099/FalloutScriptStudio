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
		public List<ProcedureNameRequest> Requests { get; } = new List<ProcedureNameRequest>();

		public ProcedureNameRequest? LastRequest { get; private set; }

		public List<String> Calls { get; } = new List<String>();

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
