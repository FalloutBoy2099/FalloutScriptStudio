namespace FalloutScriptStudio.Core.Dialogue
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
	/// <see cref="IDialogueRulesService"/> that stores the user's rules in a text file.
	/// </summary>
	public sealed class DialogueRulesService : IDialogueRulesService, IDisposable
	{
		private readonly String _filePath;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
		private DialogueFunctionRules _current = new DialogueFunctionRules();

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueRulesService"/> class.
		/// </summary>
		/// <param name="filePath">Full path of the rules file (<c>settings\DialogFunc.cfg</c>).</param>
		public DialogueRulesService(String filePath)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);
			this._filePath = filePath;
		}

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public DialogueFunctionRules Current => Volatile.Read(ref this._current);

		/// <inheritdoc />
		public async Task<DialogueFunctionRules> LoadAsync(CancellationToken cancellationToken = default)
		{
			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				IReadOnlyList<DialogueFunctionRule> rules = Array.Empty<DialogueFunctionRule>();
				if (File.Exists(this._filePath))
				{
					try
					{
						rules = DialogueFunctionRulesFile.Parse(await File.ReadAllLinesAsync(this._filePath, cancellationToken).ConfigureAwait(false));
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						rules = Array.Empty<DialogueFunctionRule>();
					}
				}

				Volatile.Write(ref this._current, new DialogueFunctionRules(rules));
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			return this.Current;
		}

		/// <inheritdoc />
		public async Task SaveAsync(IEnumerable<DialogueFunctionRule> userRules, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(userRules);

			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var rules = new DialogueFunctionRules(userRules.Select(r => r.IsDefault ? r.AsUserRule() : r).ToList());
				String content = String.Join("\r\n", DialogueFunctionRulesFile.Format(rules.Rules)) + "\r\n";
				await AtomicFile.WriteAllBytesAsync(this._filePath, new UTF8Encoding(false).GetBytes(content), cancellationToken).ConfigureAwait(false);
				Volatile.Write(ref this._current, rules);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}
