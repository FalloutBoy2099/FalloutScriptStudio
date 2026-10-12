namespace FalloutScriptStudio.Core.Parsing.Interop.Host
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Converts between the domain model and the wire DTOs of the host protocol.
	/// </summary>
	internal static class HostMapper
	{
		/// <summary>Creates the response that carries a native parse result.</summary>
		public static HostResponse ToResponse(Int64 id, NativeParseResult result)
		{
			var response = new HostResponse
			{
				Id = id,
				Status = result.Status.ToString(),
				ExitCode = result.ExitCode,
				Message = result.Message,
				Log = result.Log,
			};

			foreach (ScriptProcedure procedure in result.Procedures)
			{
				response.Procedures.Add(ToDto(procedure));
			}

			foreach (ScriptVariable variable in result.Variables)
			{
				response.Variables.Add(ToDto(variable));
			}

			return response;
		}

		/// <summary>Creates the request for a parse.</summary>
		public static HostRequest ToRequest(Int64 id, String scriptText, String filePath, ParserOptions options)
		{
			return new HostRequest
			{
				Id = id,
				Command = HostProtocol.ParseCommand,
				ScriptText = scriptText,
				FilePath = filePath,
				ParserLibraryPath = options.NativeParserPath,
				PreprocessorDefinitions = options.PreprocessorDefinitions,
				HeadersDirectory = options.HeadersDirectory,
				BackwardCompatibilityMode = options.BackwardCompatibilityMode,
				ScratchDirectory = options.ScratchDirectory,
				ScratchFileName = options.ScratchFileName,
				NativeLogPath = options.NativeLogPath,
			};
		}

		/// <summary>Creates the parser options the host hands to the in-process native parser.</summary>
		public static ParserOptions ToOptions(HostRequest request)
		{
			return new ParserOptions
			{
				NativeParserPath = request.ParserLibraryPath,
				PreprocessorDefinitions = request.PreprocessorDefinitions,
				HeadersDirectory = request.HeadersDirectory,
				BackwardCompatibilityMode = request.BackwardCompatibilityMode,
				ScratchDirectory = request.ScratchDirectory,
				ScratchFileName = String.IsNullOrWhiteSpace(request.ScratchFileName) ? ParserOptions.DefaultScratchFileName : request.ScratchFileName,
				NativeLogPath = request.NativeLogPath,
			};
		}

		/// <summary>
		/// Converts a response to a result.
		/// </summary>
		/// <exception cref="FormatException">The response contains an unknown status.</exception>
		public static NativeParseResult ToResult(HostResponse response)
		{
			if (!Enum.TryParse(response.Status, false, out NativeParseStatus status) || !Enum.IsDefined(status))
			{
				throw new FormatException("Unknown status '" + response.Status + "'.");
			}

			var procedures = new List<ScriptProcedure>(response.Procedures.Count);
			foreach (HostProcedureDto procedure in response.Procedures)
			{
				procedures.Add(ToProcedure(procedure));
			}

			var variables = new List<ScriptVariable>(response.Variables.Count);
			foreach (HostVariableDto variable in response.Variables)
			{
				variables.Add(ToVariable(variable));
			}

			return new NativeParseResult(status)
			{
				ExitCode = response.ExitCode,
				Message = response.Message,
				Log = response.Log ?? String.Empty,
				Procedures = procedures,
				Variables = variables,
			};
		}

		private static HostProcedureDto ToDto(ScriptProcedure procedure)
		{
			var dto = new HostProcedureDto
			{
				Name = procedure.Name,
				Flags = (Int32)procedure.Flags,
				DeclaredFile = procedure.DeclaredFile,
				StartFile = procedure.StartFile,
				FileName = procedure.FileName,
				DeclaredLine = procedure.DeclaredLine,
				DefinedLine = procedure.DefinedLine,
				StartLine = procedure.StartLine,
				EndLine = procedure.EndLine,
				ArgumentCount = procedure.ArgumentCount,
				TimeValue = procedure.TimeValue,
				References = ToDtos(procedure.References),
			};

			foreach (ScriptVariable variable in procedure.Variables)
			{
				dto.Variables.Add(ToDto(variable));
			}

			return dto;
		}

		private static HostVariableDto ToDto(ScriptVariable variable)
		{
			return new HostVariableDto
			{
				Name = variable.Name,
				Scope = (Int32)variable.Scope,
				DeclaredFile = variable.DeclaredFile,
				FileName = variable.FileName,
				DeclaredLine = variable.DeclaredLine,
				ArgumentDeclaredLine = variable.ArgumentDeclaredLine,
				InitialValue = variable.InitialValue,
				InitialValueKind = (Int32)variable.InitialValueKind,
				ArrayLength = variable.ArrayLength,
				References = ToDtos(variable.References),
			};
		}

		private static List<HostReferenceDto> ToDtos(IReadOnlyList<SourceReference> references)
		{
			var dtos = new List<HostReferenceDto>(references.Count);
			foreach (SourceReference reference in references)
			{
				dtos.Add(new HostReferenceDto { File = reference.File, Line = reference.Line });
			}

			return dtos;
		}

		private static ScriptProcedure ToProcedure(HostProcedureDto dto)
		{
			var variables = new List<ScriptVariable>(dto.Variables.Count);
			foreach (HostVariableDto variable in dto.Variables)
			{
				variables.Add(ToVariable(variable));
			}

			return new ScriptProcedure(dto.Name)
			{
				Flags = (ProcedureFlags)dto.Flags,
				DeclaredFile = dto.DeclaredFile ?? String.Empty,
				StartFile = dto.StartFile ?? String.Empty,
				FileName = dto.FileName ?? String.Empty,
				DeclaredLine = dto.DeclaredLine,
				DefinedLine = dto.DefinedLine,
				StartLine = dto.StartLine,
				EndLine = dto.EndLine,
				ArgumentCount = dto.ArgumentCount,
				TimeValue = dto.TimeValue,
				Variables = variables,
				References = ToReferences(dto.References),
			};
		}

		private static ScriptVariable ToVariable(HostVariableDto dto)
		{
			return new ScriptVariable(dto.Name, (VariableScope)dto.Scope)
			{
				DeclaredFile = dto.DeclaredFile,
				FileName = dto.FileName,
				DeclaredLine = dto.DeclaredLine,
				ArgumentDeclaredLine = dto.ArgumentDeclaredLine,
				InitialValue = dto.InitialValue,
				InitialValueKind = (ValueKind)dto.InitialValueKind,
				ArrayLength = dto.ArrayLength,
				References = ToReferences(dto.References),
			};
		}

		private static SourceReference[] ToReferences(List<HostReferenceDto>? dtos)
		{
			if (dtos is null || dtos.Count == 0)
			{
				return Array.Empty<SourceReference>();
			}

			var references = new SourceReference[dtos.Count];
			for (Int32 i = 0; i < references.Length; i++)
			{
				references[i] = new SourceReference(dtos[i].File, dtos[i].Line);
			}

			return references;
		}
	}
}
