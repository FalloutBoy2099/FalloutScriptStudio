namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading;

	/// <summary>The default <see cref="IOperationTracker"/>. Members are called on the UI thread.</summary>
	public sealed class OperationTracker : IOperationTracker
	{
		private Operation? _current;

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public Boolean IsRunning => this._current is not null;

		/// <inheritdoc />
		public OperationInfo? Current => this._current?.Info;

		/// <inheritdoc />
		public IOperation? TryBegin(OperationKind kind, String? subject = null, Int32 total = 0)
		{
			if (this._current is not null)
			{
				return null;
			}

			Operation operation = new Operation(this, new OperationInfo(kind, subject ?? String.Empty, 0, Math.Max(0, total), false));
			this._current = operation;
			this.Changed?.Invoke(this, EventArgs.Empty);
			return operation;
		}

		/// <inheritdoc />
		public Boolean Cancel()
		{
			return this._current?.RequestCancel() ?? false;
		}

		private void Finish(Operation operation)
		{
			if (ReferenceEquals(this._current, operation))
			{
				this._current = null;
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		private void Update(Operation operation)
		{
			if (ReferenceEquals(this._current, operation))
			{
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		private sealed class Operation : IOperation
		{
			private readonly OperationTracker _owner;
			private readonly CancellationTokenSource _source = new CancellationTokenSource();
			private Boolean _disposed;

			public Operation(OperationTracker owner, OperationInfo info)
			{
				this._owner = owner;
				this.Info = info;
			}

			public OperationInfo Info { get; private set; }

			public CancellationToken Token => this._source.Token;

			public void Report(String subject, Int32 index = 0, Int32 total = 0)
			{
				if (this._disposed)
				{
					return;
				}

				this.Info = this.Info with { Subject = subject ?? String.Empty, Index = index, Total = total > 0 ? total : this.Info.Total };
				this._owner.Update(this);
			}

			public Boolean RequestCancel()
			{
				if (this._disposed || this._source.IsCancellationRequested)
				{
					return false;
				}

				this.Info = this.Info with { IsCancelling = true };
				this._source.Cancel();
				this._owner.Update(this);
				return true;
			}

			public void Dispose()
			{
				if (this._disposed)
				{
					return;
				}

				this._disposed = true;
				this._owner.Finish(this);
				this._source.Dispose();
			}
		}
	}
}
