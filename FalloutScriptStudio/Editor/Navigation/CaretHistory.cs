namespace FalloutScriptStudio.Editor.Navigation
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The Back / Forward history of the caret (legacy <c>TabInfo.history</c> and <c>Caret_PositionChanged</c>).
	/// A new entry is recorded only when the caret jumps more than one line; smaller moves update the current entry.
	/// </summary>
	public sealed class CaretHistory
	{
		private readonly List<CaretLocation> _positions = [];
		private Int32 _current = -1;
		private Int32 _end = -1;
		private Int32 _previousLine;

		/// <summary>Gets a value indicating whether Back can navigate.</summary>
		public Boolean CanGoBack => this._current > 0;

		/// <summary>Gets a value indicating whether Forward can navigate.</summary>
		public Boolean CanGoForward => this._current >= 0 && this._current < this._end;

		/// <summary>Gets the number of stored positions.</summary>
		public Int32 Count => this._end + 1;

		/// <summary>Reports a caret move.</summary>
		/// <param name="location">The new caret location.</param>
		/// <param name="record">
		/// <see langword="false"/> when the move was caused by history navigation or a search and must not create an entry.
		/// </param>
		public void OnCaretMoved(CaretLocation location, Boolean record = true)
		{
			Int32 difference = Math.Abs(location.Line - this._previousLine);
			this._previousLine = location.Line;
			if (!record)
			{
				return;
			}

			if (difference > 1)
			{
				this._current++;
				if (this._current >= this._positions.Count)
				{
					this._positions.Add(location);
				}
				else
				{
					this._positions[this._current] = location;
				}

				this._end = this._current;
			}
			else if (this._current >= 0)
			{
				this._positions[this._current] = location;
			}
		}

		/// <summary>Moves one entry back.</summary>
		/// <returns>The location to go to, or <see langword="null"/> when there is no earlier entry.</returns>
		public CaretLocation? GoBack()
		{
			if (!this.CanGoBack)
			{
				return null;
			}

			this._current--;
			return this._positions[this._current];
		}

		/// <summary>Moves one entry forward.</summary>
		/// <returns>The location to go to, or <see langword="null"/> when there is no later entry.</returns>
		public CaretLocation? GoForward()
		{
			if (!this.CanGoForward)
			{
				return null;
			}

			this._current++;
			return this._positions[this._current];
		}
	}
}