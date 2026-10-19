namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>One entry of the "create standard handler" menu (an item, a sub menu or a separator).</summary>
	public sealed class HandlerMenuItemViewModel
	{
		/// <summary>Initializes the entry from the entry of <c>prochandlers.ini</c>.</summary>
		public HandlerMenuItemViewModel(ProcedureHandlerEntry entry)
		{
			ArgumentNullException.ThrowIfNull(entry);
			this.Name = entry.Name;
			this.Description = entry.Description;
			this.IsSeparator = entry.Kind == ProcedureHandlerKind.Separator;
			this.IsGroup = entry.Kind == ProcedureHandlerKind.Group;
			this.Children = entry.Children.Select(child => new HandlerMenuItemViewModel(child)).ToList();
		}

		/// <summary>Gets the procedure name, or the title of a sub menu.</summary>
		public String Name { get; }

		/// <summary>Gets the tooltip text.</summary>
		public String Description { get; }

		/// <summary>Gets a value indicating whether the entry is a separator line.</summary>
		public Boolean IsSeparator { get; }

		/// <summary>Gets a value indicating whether the entry is a sub menu.</summary>
		public Boolean IsGroup { get; }

		/// <summary>Gets the entries of a sub menu.</summary>
		public IReadOnlyList<HandlerMenuItemViewModel> Children { get; }
	}
}