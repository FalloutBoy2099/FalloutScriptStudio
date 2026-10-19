namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;

	/// <summary>Answers the small dialogs of the flowchart with configured values.</summary>
	internal sealed class FakeFlowchartDialogService : IFlowchartDialogService
	{
		public List<String> Captions { get; } = [];

		public String? MessageAnswer { get; set; }

		/// <summary>Gets or sets what happens when the new node dialog is shown; it returns the name of the created node.</summary>
		public Func<DocumentViewModel, Task<String?>>? NewNode { get; set; }

		public Task<String?> ShowNewNodeAsync(DocumentViewModel script)
		{
			return this.NewNode is null ? Task.FromResult<String?>(null) : this.NewNode(script);
		}

		public Task<String?> EditMessageTextAsync(String caption, String text)
		{
			this.Captions.Add(caption);
			return Task.FromResult(this.MessageAnswer);
		}
	}
}