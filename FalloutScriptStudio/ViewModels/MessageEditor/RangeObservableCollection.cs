namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Collections.Specialized;
	using System.ComponentModel;

	/// <summary>
	/// An <see cref="ObservableCollection{T}"/> that can replace its whole content with a single change notification,
	/// which keeps loading thousands of rows fast.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	public sealed class RangeObservableCollection<T> : ObservableCollection<T>
	{
		/// <summary>
		/// Replaces all items and raises one <see cref="NotifyCollectionChangedAction.Reset"/> notification.
		/// </summary>
		/// <param name="items">The new items.</param>
		public void ReplaceAll(IEnumerable<T> items)
		{
			ArgumentNullException.ThrowIfNull(items);

			this.CheckReentrancy();
			this.Items.Clear();
			foreach (T item in items)
			{
				this.Items.Add(item);
			}

			this.OnPropertyChanged(new PropertyChangedEventArgs(nameof(this.Count)));
			this.OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
			this.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}
}