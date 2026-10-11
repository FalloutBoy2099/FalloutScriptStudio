namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Editor.Highlighting;
	using ICSharpCode.AvalonEdit.Highlighting;

	/// <summary>
	/// Keeps the syntax highlighting definition of the editor current: the theme colors, the macros defined by the user
	/// and the names of the procedures of the script (legacy <c>HighlightProcedures</c> / <c>ExtraWordList</c>).
	/// </summary>
	public sealed class HighlightingController : IDisposable
	{
		private readonly EditorContext _context;
		private HashSet<String> _macros = new HashSet<String>(StringComparer.Ordinal);
		private HashSet<String> _procedures = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
		private SyntaxTheme? _theme;
		private HighlightingKind? _kind;
		private Boolean _attached;

		public HighlightingController(EditorContext context)
		{
			this._context = context;
		}

		public void Attach()
		{
			this._attached = true;
			this.Update(true);
			if (this._context.Services.UserMacros is not null)
			{
				this._context.Services.UserMacrosChanged += this.OnUserMacrosChanged;
			}
		}

		public void Dispose()
		{
			this._attached = false;
			this._context.Services.UserMacrosChanged -= this.OnUserMacrosChanged;
		}

		/// <summary>Rebuilds the highlighting definition when something it depends on changed.</summary>
		/// <param name="force">Rebuild even when nothing changed (for example after the theme changed).</param>
		public void Update(Boolean force = false)
		{
			SyntaxTheme theme = this._context.Services.Theme.SyntaxTheme;
			HighlightingKind kind = this._context.IsMessage ? HighlightingKind.Message : HighlightingKind.Ssl;
			HashSet<String> macros = this.CollectMacros();
			HashSet<String> procedures = this.CollectProcedures();

			Boolean changed = force
				|| !Object.ReferenceEquals(theme, this._theme)
				|| kind != this._kind
				|| !macros.SetEquals(this._macros)
				|| !procedures.SetEquals(this._procedures);
			if (!changed)
			{
				return;
			}

			this._theme = theme;
			this._kind = kind;
			this._macros = macros;
			this._procedures = procedures;

			this.Definition = this._context.Services.Highlighting.Create(kind, theme, macros, procedures);
			foreach (ICSharpCode.AvalonEdit.TextEditor editor in this._context.AllEditors)
			{
				editor.SyntaxHighlighting = this.Definition;
			}
		}

		/// <summary>Gets the current highlighting definition.</summary>
		public IHighlightingDefinition? Definition { get; private set; }

		private HashSet<String> CollectMacros()
		{
			HashSet<String> words = new HashSet<String>(StringComparer.Ordinal);
			if (this._context.IsMessage)
			{
				return words;
			}

			if (this._context.Services.UserMacros is IUserMacroSource source)
			{
				foreach (String word in source.GetWords())
				{
					words.Add(word);
				}
			}

			ProgramInfo? info = this._context.ProgramInfo;
			if (info is not null)
			{
				String? path = this._context.DocumentModel.FilePath;
				foreach (ScriptMacro macro in info.ScriptMacros.Values)
				{
					Boolean local = path is null
						? macro.DeclaredFile.Length == 0
						: String.Equals(macro.DeclaredFile, path, StringComparison.OrdinalIgnoreCase);
					if (local)
					{
						words.Add(macro.Token);
					}
				}
			}

			return words;
		}

		private HashSet<String> CollectProcedures()
		{
			HashSet<String> names = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			ProgramInfo? info = this._context.ProgramInfo;
			if (info is not null && !this._context.IsMessage)
			{
				foreach (ScriptProcedure procedure in info.Procedures.Where(p => !p.IsStandard))
				{
					names.Add(procedure.Name);
				}
			}

			return names;
		}

		private void OnUserMacrosChanged(Object? sender, EventArgs e)
		{
			this._context.Editor.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (this._attached)
				{
					this.Update();
				}
			}));
		}
	}
}
