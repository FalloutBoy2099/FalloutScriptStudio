namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using System.Globalization;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Localization;

	/// <summary>One place where a global variable is read or written, a child of <see cref="GlobalVariableNodeViewModel"/>.</summary>
	public sealed class GlobalUseNodeViewModel : TreeNodeViewModel
	{
		private GlobalUseNodeViewModel()
			: base("…", "placeholder")
		{
		}

		/// <summary>Creates the node shown under a variable until its uses are created (it only makes the expander appear).</summary>
		/// <returns>The node.</returns>
		public static GlobalUseNodeViewModel CreatePlaceholder()
		{
			return new GlobalUseNodeViewModel();
		}

		/// <summary>Initializes a node.</summary>
		/// <param name="use">The use.</param>
		/// <param name="owner">The variable the use belongs to.</param>
		/// <param name="key">A key that is stable across rebuilds of the tree.</param>
		public GlobalUseNodeViewModel(GlobalVariableUse use, GlobalVariable owner, String key)
			: base(use.FileName + ":" + use.Line.ToString(CultureInfo.InvariantCulture), key)
		{
			ArgumentNullException.ThrowIfNull(use);
			ArgumentNullException.ThrowIfNull(owner);
			this.Use = use;
			this.Owner = owner;
			this.ToolTip = use.File + ":" + use.Line.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Gets the use, or <see langword="null"/> for the placeholder.</summary>
		public GlobalVariableUse? Use { get; }

		/// <summary>Gets the variable the use belongs to, or <see langword="null"/> for the placeholder.</summary>
		public GlobalVariable? Owner { get; }

		/// <inheritdoc/>
		public override Boolean IsSymbol => this.Use is not null;

		/// <summary>Gets the procedure the use is in (or the macro), or an empty text.</summary>
		public String Procedure => this.Use?.Procedure ?? String.Empty;

		/// <summary>Gets a value indicating whether the use reads the value.</summary>
		public Boolean Reads => this.Use?.Reads == true;

		/// <summary>Gets a value indicating whether the use writes the value.</summary>
		public Boolean Writes => this.Use?.Writes == true;

		/// <summary>Gets what the use does, in the current language (read, write, read and write), with the literal that is written.</summary>
		public String AccessText
		{
			get
			{
				if (this.Use is not { } use)
				{
					return String.Empty;
				}

				String access = Loc.Get(use.Access switch
				{
					GlobalAccess.Read => "Str.Panels.GlobalAccessRead",
					GlobalAccess.Write => "Str.Panels.GlobalAccessWrite",
					_ => "Str.Panels.GlobalAccessReadWrite",
				});
				return use.Literal is null ? access : access + " = " + use.Literal;
			}
		}
	}
}