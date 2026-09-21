using System;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Read-only preview of Deal Approval financials for one Opportunity, computed server-side
    /// using the same Content-&gt;BWG, Target Territory-&gt;BWT and Budget/Forecast resolution
    /// rules already proven in the fmi_DealApprovalCommands JavaScript. Performs no Dataverse
    /// writes. Intended to back a Custom API named fmi_GetDealApprovalPreview
    /// (Input: OpportunityId Guid, Output: PreviewJson String) - not yet registered.
    /// </summary>
    public class GetDealApprovalPreview : PluginBase
    {
        public GetDealApprovalPreview(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GetDealApprovalPreview))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            if (!context.InputParameters.Contains("OpportunityId") ||
                !(context.InputParameters["OpportunityId"] is Guid opportunityId))
            {
                throw new InvalidPluginExecutionException("OpportunityId is required.");
            }

            var builder = new PreviewBuilder(localPluginContext.InitiatingUserService);

            context.OutputParameters["PreviewJson"] = builder.Build(opportunityId);
        }
    }
}
