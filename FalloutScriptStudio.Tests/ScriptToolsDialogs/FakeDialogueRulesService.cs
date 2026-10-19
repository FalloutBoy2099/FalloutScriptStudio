namespace FalloutScriptStudio.Tests.ScriptToolsDialogs
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;

	internal sealed class FakeDialogueRulesService : IDialogueRulesService
	{
		public FakeDialogueRulesService(params DialogueFunctionRule[] userRules)
		{
			this.Stored = new(userRules);
		}

		public event EventHandler? Changed;

		public List<DialogueFunctionRule> Stored { get; }

		public Int32 SaveCount { get; private set; }

		public Boolean FailOnSave { get; set; }

		public DialogueFunctionRules Current => new DialogueFunctionRules(this.Stored);

		public Task<DialogueFunctionRules> LoadAsync(CancellationToken cancellationToken = default)
		{
			return Task.FromResult(this.Current);
		}

		public Task SaveAsync(IEnumerable<DialogueFunctionRule> userRules, CancellationToken cancellationToken = default)
		{
			if (this.FailOnSave)
			{
				throw new IOException("disk is full");
			}

			this.SaveCount++;
			List<DialogueFunctionRule> copy = userRules.ToList();
			this.Stored.Clear();
			this.Stored.AddRange(copy);
			this.Changed?.Invoke(this, EventArgs.Empty);
			return Task.CompletedTask;
		}
	}
}