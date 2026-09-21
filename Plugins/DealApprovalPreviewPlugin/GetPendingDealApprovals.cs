using System;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Read-only Pending Deal Approval queue for the Approval Centre landing page. Global
    /// unbound, no input parameters. Backs the live Custom API fmi_GetPendingDealApprovals
    /// (Output: fmi_ApprovalsJson String). This Custom API was created via Maker, not pac/raw
    /// XML - its response property's UniqueName (the actual SDK/OData key) is fmi_ApprovalsJson,
    /// confirmed live from the org, not the cosmetic display Name "ApprovalsJson" - the exact
    /// same uniquename-vs-Name split already found and fixed for fmi_ProcessDealApprovalDecision.
    ///
    /// Scope (assigned-approver-only vs. every Pending record) is decided entirely server-side
    /// by PendingDealApprovalsBuilder/SuperApproverResolver, from context.InitiatingUserId - the
    /// real human caller, exactly as ProcessDealApprovalDecision already authorizes decisions.
    /// There is no client-side team-membership logic anywhere in this path; the Custom API
    /// itself is the authorization boundary for what a caller's Approval Centre can see, the
    /// same way fmi_ProcessDealApprovalDecision is the boundary for what they can decide.
    ///
    /// Uses the elevated service (OrgSvcFactory.CreateOrganizationService(null)) for retrieval,
    /// not InitiatingUserService: a Deal SuperApprover must see every Pending Deal Approval
    /// regardless of what row-level Read privilege their own security role happens to grant on
    /// fmi_dealapproval/fmi_dealapprovalitem, mirroring the elevation pattern already established
    /// for Submit/Decision's writes, just applied to a read.
    /// </summary>
    public class GetPendingDealApprovals : PluginBase
    {
        public GetPendingDealApprovals(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GetPendingDealApprovals))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;
            var elevatedService = localPluginContext.OrgSvcFactory.CreateOrganizationService(null);
            var builder = new PendingDealApprovalsBuilder(elevatedService);

            context.OutputParameters["fmi_ApprovalsJson"] = builder.Build(context.InitiatingUserId);
        }
    }
}
