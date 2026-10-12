namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;

	/// <summary>The preprocessor definition chosen in the toolbar.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Gets or sets the selected preprocessor definition of the toolbar.</summary>
		public String SelectedPreprocessorDefine
		{
			get => this._settings.Effective.Settings.Compiler.PreprocessorDefine is { Length: > 0 } value ? value : this._noPreprocessorDefine;
			set
			{
				if (this._loadingDefines)
				{
					return;
				}

				String? define = value is null || value.Length == 0 || String.Equals(value, this._noPreprocessorDefine, StringComparison.Ordinal) ? null : value;
				if (!String.Equals(define, this._settings.Effective.Settings.Compiler.PreprocessorDefine, StringComparison.Ordinal))
				{
					// The project owns the selected definition when it overrides it; otherwise the global setting is changed, as without a project.
					if (this._projects.Settings is { PreprocessorDefine: not null } project)
					{
						ProjectSettings changed = project.Clone();
						changed.PreprocessorDefine = define ?? String.Empty;
						if (this._projects.SaveSettings(changed))
						{
							// The project change re-parses the open documents.
							this.OnPropertyChanged();
						}

						return;
					}

					this._settings.Update(settings => settings.Compiler.PreprocessorDefine = define);
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.Title));
					foreach (DocumentViewModel document in this._workspace.Documents.Where(d => d.IsParsable))
					{
						_ = this._workspace.RequestParseAsync(document, false);
					}
				}
			}
		}

		private void LoadPreprocessorDefines()
		{
			EffectiveSettings effective = this._settings.Effective;
			List<String> values = new List<String> { this._noPreprocessorDefine };
			values.AddRange(effective.DefineChoices);
			String? current = effective.Settings.Compiler.PreprocessorDefine;
			if (!String.IsNullOrEmpty(current) && !values.Contains(current))
			{
				values.Add(current);
			}

			// The list is rebuilt only when it changed: the selection of the combo box must not be disturbed while it is being made.
			if (!values.SequenceEqual(this.PreprocessorDefineItems))
			{
				this._loadingDefines = true;
				try
				{
					this.PreprocessorDefineOptions.Clear();
					foreach (String define in values)
					{
						this.PreprocessorDefineOptions.Add(new PreprocessorDefineOption(define));
					}
				}
				finally
				{
					this._loadingDefines = false;
				}
			}

			this.OnPropertyChanged(nameof(this.SelectedPreprocessorDefine));
		}
	}
}
