namespace FalloutScriptStudio.Editor.Highlighting
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows;
	using System.Windows.Media;
	using System.Xml;
	using ICSharpCode.AvalonEdit.Highlighting;
	using ICSharpCode.AvalonEdit.Highlighting.Xshd;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// Builds AvalonEdit highlighting definitions from the embedded <c>.xshd</c> files, applying a
	/// <see cref="SyntaxTheme"/> and the user-defined macro words. Like in the legacy editor (which copied its syntax files to the
	/// <c>resources</c> folder), a user can replace a definition: a file with the name of the embedded one (<c>Ssl.xshd</c>,
	/// <c>Msg.xshd</c>) in the override folder is loaded instead, and the embedded one is used again when that file is missing or invalid.
	/// </summary>
	public sealed class HighlightingProvider
	{
		private const String UserMacrosColor = "UserMacros";
		private const String SpecMacrosColor = "SpecMacros";
		private const String ProcedureNamesColor = "ProcedureNames";

		/// <summary>The macro words that are always highlighted as special debug macros.</summary>
		private static readonly String[] _specMacros = { "DEBUGMSG" };

		private readonly String? _overrideDirectory;
		private readonly ILogger? _logger;

		/// <summary>Initializes a new instance of the <see cref="HighlightingProvider"/> class that only uses the embedded definitions.</summary>
		public HighlightingProvider()
			: this(null, null)
		{
		}

		/// <summary>Initializes a new instance of the <see cref="HighlightingProvider"/> class.</summary>
		/// <param name="overrideDirectory">
		/// The folder where the user can put <c>Ssl.xshd</c> / <c>Msg.xshd</c> files that replace the embedded ones, or <see langword="null"/>.
		/// </param>
		/// <param name="logger">Receives the warning about an override file that could not be used.</param>
		public HighlightingProvider(String? overrideDirectory, ILogger? logger)
		{
			this._overrideDirectory = overrideDirectory;
			this._logger = logger;
		}

		/// <summary>
		/// Creates the highlighting definition for a document kind.
		/// </summary>
		/// <param name="kind">Document kind.</param>
		/// <param name="theme">Color scheme to apply.</param>
		/// <param name="userMacros">Extra macro names to highlight (SSL only).</param>
		/// <param name="procedureNames">Names of the user-defined procedures to highlight with the theme's procedure color (SSL only).</param>
		public IHighlightingDefinition Create(
			HighlightingKind kind,
			SyntaxTheme theme,
			IEnumerable<String>? userMacros = null,
			IEnumerable<String>? procedureNames = null)
		{
			String[] macros = (userMacros ?? Enumerable.Empty<String>()).ToArray();
			String[] procedures = (procedureNames ?? Enumerable.Empty<String>()).ToArray();
			if (this.LoadOverride(kind) is { } custom)
			{
				try
				{
					return Build(custom, kind, theme, macros, procedures);
				}
				catch (Exception ex) when (ex is XmlException or HighlightingDefinitionInvalidException or InvalidOperationException or ArgumentException)
				{
					this._logger?.SyntaxFileUnusable(ex, GetFileName(kind));
				}
			}

			return Build(LoadEmbedded(kind), kind, theme, macros, procedures);
		}

		/// <summary>Gets the name of the file that replaces the definition of a kind in the override folder.</summary>
		/// <param name="kind">The kind.</param>
		/// <returns>The file name.</returns>
		public static String GetFileName(HighlightingKind kind)
		{
			return kind == HighlightingKind.Ssl ? "Ssl.xshd" : "Msg.xshd";
		}

		private static IHighlightingDefinition Build(
			XshdSyntaxDefinition xshd,
			HighlightingKind kind,
			SyntaxTheme theme,
			IEnumerable<String> userMacros,
			IEnumerable<String> procedureNames)
		{
			IReadOnlyDictionary<String, TokenStyle> styles = kind == HighlightingKind.Ssl ? theme.SslStyles : theme.MessageStyles;

			foreach (XshdColor color in xshd.Elements.OfType<XshdColor>())
			{
				if (color.Name is not null && styles.TryGetValue(color.Name, out TokenStyle? style))
				{
					ApplyStyle(color, style);
				}
				else
				{
					color.Foreground = CreateBrush(theme.Foreground);
				}
			}

			if (kind == HighlightingKind.Ssl)
			{
				XshdRuleSet mainRules = xshd.Elements.OfType<XshdRuleSet>().First(rules => rules.Name is null);

				String[] words = userMacros
					.Where(word => !String.IsNullOrWhiteSpace(word))
					.Distinct(StringComparer.Ordinal)
					.ToArray();
				if (words.Length > 0)
				{
					AddKeywords(mainRules, UserMacrosColor, words);
				}

				AddKeywords(mainRules, SpecMacrosColor, _specMacros);

				String[] procedures = procedureNames
					.Where(word => !String.IsNullOrWhiteSpace(word))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToArray();
				if (procedures.Length > 0)
				{
					xshd.Elements.Add(new XshdColor { Name = ProcedureNamesColor, Foreground = CreateBrush(theme.ProcedureName) });
					AddKeywords(mainRules, ProcedureNamesColor, procedures);
				}
			}

			return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
		}

		private static void AddKeywords(XshdRuleSet ruleSet, String colorName, IEnumerable<String> words)
		{
			XshdKeywords keywords = new XshdKeywords
			{
				ColorReference = new XshdReference<XshdColor>(null, colorName),
			};
			foreach (String word in words)
			{
				keywords.Words.Add(word);
			}

			// User words must win over the generic keyword groups, so insert them first.
			ruleSet.Elements.Insert(0, keywords);
		}

		/// <summary>Reads the override file of a kind.</summary>
		/// <returns>The definition, or <see langword="null"/> when there is no override or it cannot be read (then a warning is logged).</returns>
		private XshdSyntaxDefinition? LoadOverride(HighlightingKind kind)
		{
			if (String.IsNullOrEmpty(this._overrideDirectory))
			{
				return null;
			}

			String path = Path.Combine(this._overrideDirectory, GetFileName(kind));
			if (!File.Exists(path))
			{
				return null;
			}

			try
			{
				using XmlReader reader = XmlReader.Create(path);
				return HighlightingLoader.LoadXshd(reader);
			}
			catch (Exception ex) when (ex is XmlException or HighlightingDefinitionInvalidException or IOException or UnauthorizedAccessException)
			{
				this._logger?.SyntaxFileUnreadable(ex, path);
				return null;
			}
		}

		private static XshdSyntaxDefinition LoadEmbedded(HighlightingKind kind)
		{
			String resource = GetFileName(kind);
			if (!UriParser.IsKnownScheme("pack"))
			{
				// The pack scheme is registered by the first use of WPF's packaging types (done by Application in the app).
				System.IO.Packaging.PackUriHelper.Create(new Uri("file:///c:/"));
			}

			String assembly = typeof(HighlightingProvider).Assembly.GetName().Name ?? String.Empty;
			Uri uri = new Uri($"pack://application:,,,/{assembly};component/Assets/Syntax/{resource}", UriKind.Absolute);

			using Stream stream = Application.GetResourceStream(uri).Stream;
			using XmlReader reader = XmlReader.Create(stream);
			return HighlightingLoader.LoadXshd(reader);
		}

		private static void ApplyStyle(XshdColor color, TokenStyle style)
		{
			color.Foreground = style.Foreground is null ? null : CreateBrush(style.Foreground);
			color.Background = style.Background is null ? null : CreateBrush(style.Background);
			color.FontWeight = style.Bold ? FontWeights.Bold : FontWeights.Normal;
			color.FontStyle = style.Italic ? FontStyles.Italic : FontStyles.Normal;
		}

		private static SimpleHighlightingBrush CreateBrush(String color)
		{
			return new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(color));
		}
	}
}
