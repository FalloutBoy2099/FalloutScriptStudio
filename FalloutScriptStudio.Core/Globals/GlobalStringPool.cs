namespace FalloutScriptStudio.Core.Globals
{
	using System;
	using System.Collections.Concurrent;
	using System.Threading;

	/// <summary>
	/// Makes equal texts one object, so that 5 000 files that all write <c>GVAR_PLAYER_REPUTATION</c> keep one string. It belongs to one
	/// index, is thread safe, and stops growing at a limit (the texts beyond it are just not shared).
	/// </summary>
	internal sealed class GlobalStringPool
	{
		private const Int32 Limit = 200_000;

		private readonly ConcurrentDictionary<String, String> _strings = new(StringComparer.Ordinal);
		private Int32 _count;

		/// <summary>Gets the shared instance of a text.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The shared text.</returns>
		internal String Get(String text)
		{
			if (this._strings.TryGetValue(text, out String? known))
			{
				return known;
			}

			if (Volatile.Read(ref this._count) >= Limit)
			{
				return text;
			}

			String shared = this._strings.GetOrAdd(text, text);
			if (ReferenceEquals(shared, text))
			{
				Interlocked.Increment(ref this._count);
			}

			return shared;
		}

		/// <summary>Forgets every text.</summary>
		internal void Clear()
		{
			this._strings.Clear();
			Volatile.Write(ref this._count, 0);
		}
	}
}