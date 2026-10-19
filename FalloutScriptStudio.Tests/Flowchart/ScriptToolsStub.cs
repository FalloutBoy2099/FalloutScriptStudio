namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services.ScriptTools;

	/// <summary>A stand-in for <see cref="IScriptToolService"/> that records the calls; it keeps compiling when members are added.</summary>
	public class ScriptToolsStub : DispatchProxy
	{
		public List<String> Calls { get; } = [];

		public static (IScriptToolService Service, ScriptToolsStub Recorder) Create()
		{
			IScriptToolService service = DispatchProxy.Create<IScriptToolService, ScriptToolsStub>();
			return (service, (ScriptToolsStub)(Object)service);
		}

		protected override Object? Invoke(MethodInfo? targetMethod, Object?[]? args)
		{
			if (targetMethod is null)
			{
				return null;
			}

			this.Calls.Add(targetMethod.Name);
			Type returnType = targetMethod.ReturnType;
			if (returnType == typeof(Task))
			{
				return Task.CompletedTask;
			}

			if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				Type result = returnType.GetGenericArguments()[0];
				Object? value = result.IsValueType ? Activator.CreateInstance(result) : null;
				return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result).Invoke(null, new[] { value });
			}

			return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
		}
	}
}