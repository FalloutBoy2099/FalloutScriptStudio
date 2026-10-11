namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;

	/// <summary>
	/// A message source that forwards to another source which can be replaced at any time. A long-lived tool window keeps one of
	/// these, and swaps the inner source after a message file was edited, so the files are read again without restarting the tool.
	/// </summary>
	public sealed class ReloadableMessageSource : IDialogueMessageSource
	{
		private IDialogueMessageSource _inner;

		/// <summary>
		/// Initializes a new instance of the <see cref="ReloadableMessageSource"/> class.
		/// </summary>
		/// <param name="inner">The source to forward to.</param>
		public ReloadableMessageSource(IDialogueMessageSource inner)
		{
			ArgumentNullException.ThrowIfNull(inner);
			this._inner = inner;
		}

		/// <summary>Replaces the source that calls are forwarded to.</summary>
		/// <param name="inner">The new source.</param>
		public void Replace(IDialogueMessageSource inner)
		{
			ArgumentNullException.ThrowIfNull(inner);
			this._inner = inner;
		}

		/// <inheritdoc />
		public Task EnsureLoadedAsync(IEnumerable<Int32> messageFileNumbers, CancellationToken cancellationToken = default)
		{
			return this._inner.EnsureLoadedAsync(messageFileNumbers, cancellationToken);
		}

		/// <inheritdoc />
		public DialogueMessageLookup GetMessage(Int32 messageFileNumber, Int32 messageNumber)
		{
			return this._inner.GetMessage(messageFileNumber, messageNumber);
		}

		/// <inheritdoc />
		public String? GetMessageFilePath(Int32 messageFileNumber)
		{
			return this._inner.GetMessageFilePath(messageFileNumber);
		}
	}
}
