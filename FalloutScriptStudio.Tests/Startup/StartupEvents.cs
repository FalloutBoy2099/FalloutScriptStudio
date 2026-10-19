namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Startup;

	/// <summary>The ordered record of everything the fakes of a start-up test were asked to do.</summary>
	internal sealed class StartupEvents
	{
		private readonly List<String> _items = [];

		public IReadOnlyList<String> Items => this._items;

		public void Add(String item)
		{
			this._items.Add(item);
		}

		public Int32 Count(String item)
		{
			return this._items.FindAll(candidate => candidate == item).Count;
		}
	}
}