using System;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Synchronous server-side Approve/Reject decision on an Opportunity's Pending Deal Approval.
    /// Backs the Custom API fmi_ProcessDealApprovalDecision (Input: fmi_DealApprovalId Guid
    /// [required], fmi_Decision Integer [required, Approve=1/Reject=2], fmi_DecisionComment String
    /// [optional]; Output: fmi_DealApprovalId Guid, fmi_ApprovalStatus Integer). These are the
    /// live UniqueNames confirmed from the environment's own OData WADL - this Custom API was
    /// created via Studio's Custom API designer (unlike fmi_SubmitDealApproval, which was
    /// pac-authored with matching unprefixed Name/UniqueName), so its parameters' UniqueName -
    /// the actual SDK message field key, not the cosmetic display Name - came out fmi_-prefixed.
    /// UniqueNames are immutable after creation, so this plugin's parameter keys must match them.
    /// </summary>
    public class ProcessDealApprovalDecision : PluginBase
    {
        public ProcessDealApprovalDecision(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(ProcessDealApprovalDecision))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            if (!context.InputParameters.Contains("fmi_DealApprovalId") ||
                !(context.InputParameters["fmi_DealApprovalId"] is Guid dealApprovalId))
            {
                throw new InvalidPluginExecutionException("fmi_DealApprovalId is required.");
            }

            if (!context.InputParameters.Contains("fmi_Decision") ||
                !(context.InputParameters["fmi_Decision"] is int decisionValue) ||
                !Enum.IsDefined(typeof(DealApprovalDecision), decisionValue))
            {
                throw new InvalidPluginExecutionException(
                    "fmi_Decision is required and must be Approve (1) or Reject (2).");
            }

            var decisionComment = context.InputParameters.Contains("fmi_DecisionComment")
                ? context.InputParameters["fmi_DecisionComment"] as string
                : null;

            // context.InitiatingUserId is that actual human caller's identity: it is who invoked
            // this request, not a plugin/application service account, not the Deal Approval's
            // owner, and not the assigned fmi_approver. That is the identity checked against
            // fmi_approver/Super Approver membership, and the identity written into
            // fmi_decisionby - it never changes, regardless of which service performs the writes.
            //
            // DecisionBuilder's own Dataverse reads (the Deal Approval record, Super Approver team
            // membership) and writes are all performed via an elevated service
            // (OrgSvcFactory.CreateOrganizationService(null), the standard SDK mechanism for a
            // plugin to run with full server-side privilege rather than the calling user's own).
            // This is what lets the Decision API keep working once ordinary approvers' and the
            // Deal SuperApprover role's own table privileges are reduced to Read-only: the human
            // caller is authorized first, purely by identity comparison, and only afterwards does
            // a privilege-independent service perform the actual write. Direct table Update by an
            // ordinary caller is prevented by that same security-privilege model, not by a
            // marker-authenticating guard.
            var elevatedService = localPluginContext.OrgSvcFactory.CreateOrganizationService(null);
            var builder = new DecisionBuilder(elevatedService, context.InitiatingUserId);
            var result = builder.Process(dealApprovalId, (DealApprovalDecision)decisionValue, decisionComment);

            context.OutputParameters["fmi_DealApprovalId"] = result.DealApprovalId;
            context.OutputParameters["fmi_ApprovalStatus"] = result.ApprovalStatus;
        }
    }
}
