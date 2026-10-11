namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;

	/// <summary>A snippet store whose change notification the test raises itself, from any thread.</summary>
	internal sealed class FakeNodeCodeSnippetStore : INodeCodeSnippetStore
	{
		private EventHandler? _changed;

		public event EventHandler? Changed
		{
			add => this._changed += value;
			remove => this._changed -= value;
		}

		/// <summary>Gets the number of handlers subscribed to <see cref="Changed"/>.</summary>
		public Int32 SubscriberCount => this._changed?.GetInvocationList().Length ?? 0;

		public IReadOnlyList<NodeCodeSnippet> Snippets { get; set; } = Array.Empty<NodeCodeSnippet>();

		/// <summary>Raises <see cref="Changed"/> on the calling thread.</summary>
		public void RaiseChanged()
		{
			this._changed?.Invoke(this, EventArgs.Empty);
		}

		public Task LoadAsync(CancellationToken cancellationToken = default)
		{
			return Task.CompletedTask;
		}

		public Task<Boolean> AddAsync(String name, String code, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}

		public Task<Boolean> RemoveAsync(String name, CancellationToken cancellationToken = default)
		{
			return Task.FromResult(true);
		}
	}
}
