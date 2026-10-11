namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>Gives every option of the settings sections a value different from its default (used to prove that nothing is forgotten).</summary>
	internal static class SettingsOptionMutator
	{
		public static void ChangeEveryOption(AppSettings settings)
		{
			foreach (Object section in new Object[] { settings.Paths, settings.Compiler, settings.Editor, settings.Messages, settings.Flowchart })
			{
				foreach (PropertyInfo property in section.GetType().GetProperties().Where(p => p.CanRead && p.CanWrite))
				{
					property.SetValue(section, Change(property));
				}
			}
		}

		private static Object? Change(PropertyInfo property)
		{
			Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
			if (type == typeof(Boolean))
			{
				return !(Boolean)GetDefault(property, type)!;
			}

			if (type == typeof(Int32))
			{
				return (Int32)GetDefault(property, type)! + 7;
			}

			if (type == typeof(String))
			{
				return "changed-" + property.Name;
			}

			if (type.IsEnum)
			{
				Object current = GetDefault(property, type)!;
				return Enum.GetValues(type).Cast<Object>().First(value => !value.Equals(current));
			}

			if (type == typeof(List<String>))
			{
				return new List<String> { "changed-" + property.Name };
			}

			throw new NotSupportedException("Unsupported option type " + type + " for " + property.Name);
		}

		private static Object? GetDefault(PropertyInfo property, Type type)
		{
			Object section = Activator.CreateInstance(property.DeclaringType!)!;
			return property.GetValue(section) ?? (type.IsValueType ? Activator.CreateInstance(type) : null);
		}
	}
}
