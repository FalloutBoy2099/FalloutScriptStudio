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
		private IReadOnlyList<String> _fileLines = Array.Empty<String>();
		private Boolean _loaded;

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
				await this.ReadFileAsync(cancellationToken).ConfigureAwait(false);
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
				if (!this._loaded)
				{
					await this.ReadFileAsync(cancellationToken).ConfigureAwait(false);
				}

				if (!this._loaded)
				{
					// The file exists but could not be read: writing now would replace the user's rules by an empty list.
					throw new IOException("The dialogue rules file could not be read, so it was not overwritten: " + this._filePath);
				}

				var rules = new DialogueFunctionRules(userRules.Select(r => r.IsDefault ? r.AsUserRule() : r).ToList());
				IReadOnlyList<String> lines = DialogueFunctionRulesFile.Format(rules.Rules, this._fileLines);
				String content = String.Join("\r\n", lines) + "\r\n";
				await AtomicFile.WriteAllBytesAsync(this._filePath, new UTF8Encoding(false).GetBytes(content), cancellationToken).ConfigureAwait(false);
				this._fileLines = lines;
				Volatile.Write(ref this._current, rules);
			}
			finally
			{
				this._gate.Release();
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		private async Task ReadFileAsync(CancellationToken cancellationToken)
		{
			IReadOnlyList<String> lines = Array.Empty<String>();
			Boolean readable = true;
			if (File.Exists(this._filePath))
			{
				try
				{
					lines = await File.ReadAllLinesAsync(this._filePath, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					readable = false;
				}
			}

			if (readable)
			{
				this._fileLines = lines;
				Volatile.Write(ref this._current, new DialogueFunctionRules(DialogueFunctionRulesFile.Parse(lines)));
			}
			else if (!this._loaded)
			{
				Volatile.Write(ref this._current, new DialogueFunctionRules());
			}

			this._loaded = this._loaded || readable;
		}

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}
