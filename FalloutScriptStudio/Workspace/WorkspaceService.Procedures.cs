namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading.Tasks;
	using Documents;

	/// <summary>Procedure operations, delegated to <see cref="ProcedureOperations"/>.</summary>
	public sealed partial class WorkspaceService
	{
		public Task CreateProcedureAsync(DocumentViewModel document, String? suggestedName = null, String? anchorProcedure = null)
		{
			return this._procedures.CreateAsync(this, document, suggestedName, anchorProcedure);
		}

		public Task CreateHandlerAsync(DocumentViewModel document, String handlerName, String? anchorProcedure = null)
		{
			return this._procedures.CreateHandlerAsync(this, document, handlerName, anchorProcedure);
		}

		public Task<Boolean> DeleteProcedureAsync(DocumentViewModel document, String name)
		{
			return this._procedures.DeleteAsync(document, name);
		}

		public Task<Boolean> MoveProcedureAsync(DocumentViewModel document, String name, String? beforeProcedure)
		{
			return this._procedures.MoveAsync(document, name, beforeProcedure);
		}
	}
}