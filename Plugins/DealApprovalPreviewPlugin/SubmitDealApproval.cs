using System;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Synchronous server-side Opportunity Deal Approval submission. Re-resolves the current
    /// financial snapshot itself (never trusts client-supplied values), deep-inserts one
    /// fmi_dealapproval parent with one fmi_dealapprovalitem child per Opportunity Item, and
    /// updates the Opportunity to Pending/current approval. Intended to back a Custom API named
    /// fmi_SubmitDealApproval (Input: OpportunityId Guid, CoordinatorComment String [optional];
    /// Output: DealApprovalId Guid, ItemCount Integer).
    /// </summary>
    public class SubmitDealApproval : PluginBase
    {
        public SubmitDealApproval(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(SubmitDealApproval))
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

            var coordinatorComment = context.InputParameters.Contains("CoordinatorComment")
                ? context.InputParameters["CoordinatorComment"] as string
                : null;

            // Elevated service (OrgSvcFactory.CreateOrganizationService(null)) performs the
            // actual controlled write - the standard SDK mechanism for a plugin to run with full
            // server-side privilege rather than the calling user's own, mirroring the pattern
            // already established for ProcessDealApprovalDecision/DecisionBuilder. This is what
            // lets Submit keep working once ordinary coordinators' direct Create privilege on
            // fmi_dealapproval/fmi_dealapprovalitem is reduced or removed, per the intended
            // security model (business users get Read-only on both tables; Submit and Decision
            // are the only write paths).
            //
            // context.InitiatingUserId is kept entirely separate from that execution identity: it
            // is the actual human who called this API, and is what SubmitBuilder writes into
            // fmi_requestedby - never SYSTEM, never the elevated service's own identity.
            var elevatedService = localPluginContext.OrgSvcFactory.CreateOrganizationService(null);
            new ReadOnlyTeamResolver(elevatedService).EnsureCanSubmit(context.InitiatingUserId);
            var builder = new SubmitBuilder(elevatedService, context.InitiatingUserId);
            var result = builder.Submit(opportunityId, coordinatorComment);

            context.OutputParameters["DealApprovalId"] = result.DealApprovalId;
            context.OutputParameters["ItemCount"] = result.ItemCount;
        }
    }
}
