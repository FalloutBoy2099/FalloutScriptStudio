namespace FalloutScriptStudio.Localization
{
	using System;
	using System.Threading;
	using System.Windows;

	/// <summary>
	/// The one way to follow <see cref="Loc.LanguageChanged"/>. The event is static, so a plain <c>+=</c> keeps the subscriber
	/// alive for the life of the process unless it unsubscribes. This subscription only holds its target weakly: a collected target
	/// ends the subscription at the next language change, and <see cref="Dispose"/> ends it at once.
	/// </summary>
	/// <remarks>
	/// The callback must not capture the target (use the target it receives, for example <c>static panel =&gt; panel.Rebuild()</c>),
	/// otherwise the callback itself keeps the target alive.
	/// </remarks>
	public sealed class LanguageChangedSubscription : IDisposable
	{
		private readonly WeakReference<Object>? _target;
		private readonly Action<Object>? _targetCallback;
		private readonly Action? _callback;
		private Int32 _active = 1;

		private LanguageChangedSubscription(Object target, Action<Object> callback)
		{
			this._target = new WeakReference<Object>(target);
			this._targetCallback = callback;
		}

		private LanguageChangedSubscription(Action callback)
		{
			this._callback = callback;
		}

		/// <summary>Gets a value indicating whether the subscription still follows the language: it was not disposed and its target is alive.</summary>
		public Boolean IsActive => Volatile.Read(ref this._active) == 1 && (this._target is null || this._target.TryGetTarget(out _));

		/// <summary>Calls <paramref name="onLanguageChanged"/> with <paramref name="target"/> after every language change, as long as the target is alive.</summary>
		/// <typeparam name="T">The type of the target.</typeparam>
		/// <param name="target">The object that follows the language; it is only referenced weakly.</param>
		/// <param name="onLanguageChanged">The callback; it must not capture <paramref name="target"/>.</param>
		/// <returns>The subscription; dispose it when the target is disposed or closed.</returns>
		public static LanguageChangedSubscription Subscribe<T>(T target, Action<T> onLanguageChanged)
			where T : class
		{
			ArgumentNullException.ThrowIfNull(target);
			ArgumentNullException.ThrowIfNull(onLanguageChanged);

			LanguageChangedSubscription subscription = new(target, instance => onLanguageChanged((T)instance));
			Loc.LanguageChanged += subscription.OnLanguageChanged;
			return subscription;
		}

		/// <summary>Like <see cref="Subscribe{T}"/> for a window: the subscription also ends when the window is closed.</summary>
		/// <typeparam name="T">The type of the window.</typeparam>
		/// <param name="window">The window that follows the language; it is only referenced weakly.</param>
		/// <param name="onLanguageChanged">The callback; it must not capture <paramref name="window"/>.</param>
		/// <returns>The subscription.</returns>
		public static LanguageChangedSubscription SubscribeUntilClosed<T>(T window, Action<T> onLanguageChanged)
			where T : Window
		{
			ArgumentNullException.ThrowIfNull(window);

			LanguageChangedSubscription subscription = Subscribe(window, onLanguageChanged);
			window.Closed += (_, _) => subscription.Dispose();
			return subscription;
		}

		/// <summary>Calls <paramref name="onLanguageChanged"/> after every language change for the life of the process. For static services only.</summary>
		/// <param name="onLanguageChanged">The callback.</param>
		/// <returns>The subscription.</returns>
		public static LanguageChangedSubscription SubscribeForever(Action onLanguageChanged)
		{
			ArgumentNullException.ThrowIfNull(onLanguageChanged);

			LanguageChangedSubscription subscription = new(onLanguageChanged);
			Loc.LanguageChanged += subscription.OnLanguageChanged;
			return subscription;
		}

		/// <summary>Ends the subscription. It can be called more than once.</summary>
		public void Dispose()
		{
			if (Interlocked.Exchange(ref this._active, 0) == 1)
			{
				Loc.LanguageChanged -= this.OnLanguageChanged;
			}
		}

		private void OnLanguageChanged(Object? sender, EventArgs e)
		{
			if (this._callback is not null)
			{
				this._callback();
				return;
			}

			if (this._target is not null && this._target.TryGetTarget(out Object? target))
			{
				this._targetCallback?.Invoke(target);
			}
			else
			{
				this.Dispose();
			}
		}
	}
}