namespace FalloutScriptStudio.Localization
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Globalization;
	using System.Linq;
	using System.Windows;

	/// <summary>
	/// Access to the localized texts of the interface.
	/// </summary>
	/// <remarks>
	/// The texts live in <c>Localization\Strings\&lt;Area&gt;.&lt;language&gt;.xaml</c> resource dictionaries (keys <c>Str.&lt;Area&gt;.&lt;Name&gt;</c>).
	/// The English dictionaries are always loaded; the ones of the chosen language are merged over them, so a missing
	/// translation shows the English text. XAML binds with <c>{DynamicResource Str.&lt;Area&gt;.&lt;Name&gt;}</c> and follows a language change at once;
	/// code uses <see cref="Get"/> / <see cref="Format"/>. This class is the one deliberate piece of global state of the interface:
	/// the resources of a WPF application are global themselves. Without a running <see cref="Application"/> (unit tests) the English
	/// texts are read from a private copy.
	/// </remarks>
	public static class Loc
	{
		/// <summary>The code of the language the texts are written in first.</summary>
		public const String DefaultLanguage = "en";

		/// <summary>The areas of the interface, one string dictionary per area and language.</summary>
		public static readonly IReadOnlyList<String> Areas = new[]
		{
			"Common", "Shell", "Panels", "ToolWindows", "MessageEditor", "Flowchart", "ScriptTools", "Editor",
		};

		private static readonly Object _gate = new Object();
		private static ResourceDictionary? _english;
		private static Dictionary<String, String>? _fallbackTexts;
		private static ResourceDictionary? _translation;
		private static ResourceDictionary? _applicationEnglish;

		/// <summary>Raised after <see cref="SetLanguage"/> changed the language.</summary>
		public static event EventHandler? LanguageChanged;

		/// <summary>Gets the languages that can be chosen.</summary>
		public static IReadOnlyList<LanguageInfo> Languages { get; } = new[]
		{
			new LanguageInfo("en", "English"),
			new LanguageInfo("de", "Deutsch"),
			new LanguageInfo("es", "Español"),
			new LanguageInfo("fr", "Français"),
			new LanguageInfo("it", "Italiano"),
			new LanguageInfo("pt", "Português"),
			new LanguageInfo("ru", "Русский"),
			new LanguageInfo("sv", "Svenska"),
			new LanguageInfo("pl", "Polski"),
			new LanguageInfo("cs", "Čeština"),
			new LanguageInfo("zh", "简体中文"),
		};

		/// <summary>Gets the code of the language in use.</summary>
		public static String Language { get; private set; } = DefaultLanguage;

		/// <summary>Gets the language of the operating system when it is one of ours, English otherwise.</summary>
		public static String DetectSystemLanguage()
		{
			return Normalize(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
		}

		/// <summary>Loads the English texts into the application resources, then the ones of <paramref name="code"/> over them.</summary>
		public static void Initialize(String code)
		{
			Application? application = Application.Current;
			if (application is not null && _applicationEnglish is null)
			{
				_applicationEnglish = Load(DefaultLanguage);
				application.Resources.MergedDictionaries.Add(_applicationEnglish);
			}

			SetLanguage(code);
		}

		/// <summary>Changes the language. Everything bound with <c>DynamicResource</c> follows at once.</summary>
		public static void SetLanguage(String code)
		{
			String language = Normalize(code);
			Application? application = Application.Current;

			if (application is not null && _translation is not null)
			{
				application.Resources.MergedDictionaries.Remove(_translation);
				_translation = null;
			}

			Language = language;
			if (application is not null && language != DefaultLanguage)
			{
				_translation = Load(language);
				application.Resources.MergedDictionaries.Add(_translation);
			}

			CultureInfo culture = CreateCulture(language);
			CultureInfo.DefaultThreadCurrentUICulture = culture;
			CultureInfo.CurrentUICulture = culture;
			LanguageChanged?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>Gets a text; the key itself is returned when no text has that key.</summary>
		public static String Get(String key)
		{
			return TryGet(key, out String? text) ? text : key;
		}

		/// <summary>Looks a text up without falling back to its key.</summary>
		public static Boolean TryGet(String key, [NotNullWhen(true)] out String? text)
		{
			text = Application.Current?.TryFindResource(key) as String;
			if (text is null && Application.Current is null)
			{
				text = GetFallbackEnglish().TryGetValue(key, out String? english) ? english : null;
			}

			return text is not null;
		}

		/// <summary>Gets a text and formats it with the arguments (a broken format of a translation is shown as it is).</summary>
		public static String Format(String key, params Object?[] arguments)
		{
			String format = Get(key);
			try
			{
				return String.Format(CultureInfo.CurrentCulture, format, arguments);
			}
			catch (FormatException)
			{
				return format;
			}
		}

		private static CultureInfo CreateCulture(String language)
		{
			try
			{
				return CultureInfo.GetCultureInfo(language);
			}
			catch (CultureNotFoundException)
			{
				return CultureInfo.InvariantCulture;
			}
		}

		private static String Normalize(String? code)
		{
			String lower = (code ?? String.Empty).ToLowerInvariant();
			return Languages.Any(language => language.Code == lower) ? lower : DefaultLanguage;
		}

		/// <summary>Loads the dictionaries of every area for one language into a single dictionary.</summary>
		private static ResourceDictionary Load(String language)
		{
			ResourceDictionary container = new ResourceDictionary();
			foreach (String area in Areas)
			{
				Uri source = new Uri($"pack://application:,,,/FalloutScriptStudio;component/Localization/Strings/{area}.{language}.xaml", UriKind.Absolute);
				try
				{
					container.MergedDictionaries.Add(new ResourceDictionary { Source = source });
				}
				catch (Exception ex) when (ex is System.IO.IOException or System.Windows.Markup.XamlParseException)
				{
					// An area that has no file for this language yet: its English texts show instead.
				}
			}

			return container;
		}

		private static Dictionary<String, String> GetFallbackEnglish()
		{
			lock (_gate)
			{
				if (_fallbackTexts is not null)
				{
					return _fallbackTexts;
				}

				_english = Load(DefaultLanguage);
				Dictionary<String, String> texts = new Dictionary<String, String>(StringComparer.Ordinal);
				foreach (ResourceDictionary area in _english.MergedDictionaries)
				{
					foreach (System.Collections.DictionaryEntry entry in area)
					{
						if (entry.Key is String key && entry.Value is String value)
						{
							texts[key] = value;
						}
					}
				}

				_fallbackTexts = texts;
				return texts;
			}
		}
	}
}
