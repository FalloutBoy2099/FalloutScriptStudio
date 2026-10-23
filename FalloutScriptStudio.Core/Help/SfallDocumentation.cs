namespace FalloutScriptStudio.Core.Help
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Where the online documentation of sfall is (https://sfall-team.github.io/sfall/) and which of its pages describes a function. A page is
	/// named from the category of the function in <c>functions.ini</c> (the pages of the site are the categories of its reference); the
	/// function is the anchor of its page. Only that host is ever opened.
	/// </summary>
	public static class SfallDocumentation
	{
		/// <summary>The host of the documentation.</summary>
		public const String Host = "sfall-team.github.io";

		/// <summary>The path every address of the documentation starts with.</summary>
		public const String PathPrefix = "/sfall/";

		/// <summary>The address of the documentation.</summary>
		public const String BaseAddress = "https://" + Host + PathPrefix;

		private static readonly (String Category, String Page)[] _categoryPages =
		[
			("Global script functions", "global-script-functions"),
			("Hook handlers", "hook-types"),
			("Hook arguments", "hook-functions"),
			("Arrays", "array-functions"),
			("String functions", "strings"),
			("Math functions", "math"),
			("Map functions", "maps-and-encounters"),
			("Combat functions", "combat"),
			("Animation functions", "animations"),
			("Perks/Traits functions", "perks-and-traits"),
			("Graphics functions", "graphics"),
			("Working with ini files", "ini-settings"),
			("Read/write/call functions", "direct-memory-access"),
			("Virtual files", "virtual-file-system"),
			("Keyboard/Mouse/Screen functions", "keyboard-and-mouse"),
			("Interface/Sound/Video functions", "interface"),
			("Critter/Player functions", "objects-and-scripts"),
			("Functions for working with proto", "objects-and-scripts"),
			("Common funcX", "sfall-funcx-macros"),
			("Game functions", "other"),
		];

		/// <summary>Gets the address of the documentation.</summary>
		public static Uri BaseUri { get; } = new Uri(BaseAddress, UriKind.Absolute);

		/// <summary>Finds the page of the documentation that describes a function.</summary>
		/// <param name="name">The name of the function.</param>
		/// <param name="categories">The places of the function in the functions tree (<c>Category &gt; Sub category</c>).</param>
		/// <returns>The name of the page, or <see langword="null"/> when it cannot be told.</returns>
		public static String? FindPage(String name, IEnumerable<String> categories)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(categories);

			if (name.Contains("sfall_global", StringComparison.OrdinalIgnoreCase))
			{
				return "global-variables";
			}

			if (name.StartsWith("HOOK_", StringComparison.Ordinal))
			{
				return "hook-types";
			}

			if (name.Contains("sfall_arg", StringComparison.OrdinalIgnoreCase) || name.Contains("sfall_return", StringComparison.OrdinalIgnoreCase)
				|| name.StartsWith("register_hook", StringComparison.OrdinalIgnoreCase))
			{
				return "hook-functions";
			}

			foreach (String path in categories)
			{
				foreach ((String category, String page) in _categoryPages)
				{
					if (path.Contains(category, StringComparison.OrdinalIgnoreCase))
					{
						return page;
					}
				}
			}

			return null;
		}

		/// <summary>Builds the address of the documentation of a topic: the page of its category with its name as the anchor, otherwise the index of the site.</summary>
		/// <param name="topic">The topic.</param>
		/// <returns>The address, or <see langword="null"/> when the topic is not documented on the site (a macro or a procedure of the project).</returns>
		public static Uri? GetUri(HelpTopic topic)
		{
			ArgumentNullException.ThrowIfNull(topic);

			if (!topic.HasDocumentation)
			{
				return null;
			}

			if (!topic.IsSfall || String.IsNullOrEmpty(topic.DocumentationPage))
			{
				return BaseUri;
			}

			String name = topic.Name.ToLowerInvariant();
			return TryCreate(BaseAddress + topic.DocumentationPage + "/#" + Uri.EscapeDataString(name), out Uri? uri) ? uri : BaseUri;
		}

		/// <summary>Determines whether an address is on the documentation site: <c>https</c>, the host of the site, a path below <c>/sfall/</c>, no user information and no other port.</summary>
		/// <param name="uri">The address.</param>
		/// <returns><see langword="true"/> when it may be opened.</returns>
		public static Boolean IsAllowed(Uri? uri)
		{
			return uri is not null
				&& uri.IsAbsoluteUri
				&& String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
				&& String.Equals(uri.Host, Host, StringComparison.OrdinalIgnoreCase)
				&& uri.IsDefaultPort
				&& uri.UserInfo.Length == 0
				&& uri.AbsolutePath.StartsWith(PathPrefix, StringComparison.Ordinal)
				&& !uri.AbsolutePath.Contains("..", StringComparison.Ordinal);
		}

		/// <summary>Creates an address from text and checks it with <see cref="IsAllowed"/>.</summary>
		/// <param name="text">The address.</param>
		/// <param name="uri">Receives the address.</param>
		/// <returns><see langword="true"/> when the text is an address of the documentation.</returns>
		public static Boolean TryCreate(String? text, out Uri? uri)
		{
			uri = null;
			if (String.IsNullOrWhiteSpace(text) || !Uri.TryCreate(text, UriKind.Absolute, out Uri? created) || !IsAllowed(created))
			{
				return false;
			}

			uri = created;
			return true;
		}
	}
}