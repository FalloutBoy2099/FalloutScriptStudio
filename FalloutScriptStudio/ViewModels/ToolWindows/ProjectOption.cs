namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// One option of the project settings: the global value, and the value of the project that applies while the option is overridden.
	/// While the option inherits, <see cref="Value"/> shows the global value.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	public sealed class ProjectOption<T> : OverrideMode
	{
		private T _value;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectOption{T}"/> class.
		/// </summary>
		/// <param name="globalValue">The value of the global settings.</param>
		/// <param name="isOverridden">Whether the project overrides the global value.</param>
		/// <param name="projectValue">The value of the project (used when <paramref name="isOverridden"/> is <see langword="true"/>).</param>
		public ProjectOption(T globalValue, Boolean isOverridden, T projectValue)
			: base(isOverridden)
		{
			this.GlobalValue = globalValue;
			this._value = isOverridden ? projectValue : globalValue;
		}

		/// <summary>Gets the value of the global settings.</summary>
		public T GlobalValue { get; }

		/// <summary>Gets or sets the value shown and edited: the global one while inheriting, the one of the project while overriding.</summary>
		public T Value
		{
			get => this._value;
			set
			{
				if (!Equals(this._value, value))
				{
					this._value = value;
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>Gets the value that is in effect: the one of the project while overriding, the global one otherwise.</summary>
		public T EffectiveValue => this.IsOverridden ? this.Value : this.GlobalValue;

		/// <inheritdoc/>
		protected override void OnOverrideChanged(Boolean isOverridden)
		{
			if (!isOverridden)
			{
				this.Value = this.GlobalValue;
			}

			this.OnPropertyChanged(nameof(this.EffectiveValue));
		}
	}
}