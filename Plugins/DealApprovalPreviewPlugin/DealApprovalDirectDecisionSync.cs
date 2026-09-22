using System;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// PreOperation guard+sync on fmi_dealapproval Update, filtered to fmi_approvalstatus. Lets a
    /// System Administrator change fmi_approvalstatus directly on the form/grid in the model-driven
    /// app - not just through the fmi_ProcessDealApprovalDecision Custom API - while keeping that
    /// edit fully equivalent to going through Approve/Reject: same Pending-only state check, same
    /// comment-required-on-reject rule, same fmi_decisionby/fmi_decisionon stamp, and the same
    /// synchronous Opportunity.fmi_currentapprovalstatus update in the same transaction.
    ///
    /// Skipped entirely when the incoming Target already carries fmi_decisionby or
    /// fmi_cancelledby: those are the markers DecisionBuilder and CancelDealApprovalRequest always
    /// write in the same call that sets fmi_approvalstatus, so this guard recognises the write as
    /// already having come through one of those controlled paths and does not reprocess it. This
    /// avoids double-stamping/double Opportunity-updating when DecisionBuilder's own elevated
    /// Update call reaches this same Update-message step.
    ///
    /// Everyone else's direct Update to fmi_approvalstatus is still blocked, exactly as before this
    /// change: table Update privilege on fmi_dealapproval is what stops ordinary users reaching
    /// this plugin at all, and the explicit SystemAdministratorResolver check below is
    /// defense-in-depth on top of that, consistent with the pattern used in
    /// DealApprovalParentLockGuard/DealApprovalItemImmutabilityGuard.
    ///
    /// Registration requirement: this step needs a PreImage aliased "PreImage" with columns
    /// fmi_approvalstatus and fmi_opportunity, registered on the same Update step (PreOperation,
    /// synchronous, filtered to the fmi_approvalstatus attribute).
    /// </summary>
    public class DealApprovalDirectDecisionSync : PluginBase
    {
        private const string OpportunityLogicalName = "opportunity";
        private const string SystemUserLogicalName = "systemuser";
        private const string PreImageAlias = "PreImage";

        public DealApprovalDirectDecisionSync(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(DealApprovalDirectDecisionSync))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;

            if (!(context.InputParameters["Target"] is Entity target) ||
                !target.Contains("fmi_approvalstatus"))
            {
                return;
            }

            if (target.Contains("fmi_decisionby") || target.Contains("fmi_cancelledby"))
            {
                // Already fully processed by DecisionBuilder or CancelDealApprovalRequest.
                return;
            }

            var elevatedService = localPluginContext.OrgSvcFactory.CreateOrganizationService(null);

            if (!new SystemAdministratorResolver(elevatedService).IsSystemAdministrator(context.InitiatingUserId))
            {
                throw new InvalidPluginExecutionException(
                    "Only a System Administrator can change the Approval Status field directly. " +
                    "Use Approve/Reject instead.");
            }

            if (!context.PreEntityImages.Contains(PreImageAlias) ||
                !(context.PreEntityImages[PreImageAlias] is Entity preImage))
            {
                throw new InvalidPluginExecutionException(
                    "The Deal Approval's prior state could not be read; this direct status change was not applied.");
            }

            var currentStatus = preImage.GetAttributeValue<OptionSetValue>("fmi_approvalstatus")?.Value;

            if (!DealApprovalStateGuard.CanDecide(currentStatus))
            {
                throw new InvalidPluginExecutionException(
                    "This Deal Approval is not Pending, so its Approval Status cannot be changed directly.");
            }

            var opportunityRef = preImage.GetAttributeValue<EntityReference>("fmi_opportunity");

            if (opportunityRef == null)
            {
                throw new InvalidPluginExecutionException(
                    "This Deal Approval has no Opportunity, so its Approval Status cannot be changed directly.");
            }

            var newStatus = target.GetAttributeValue<OptionSetValue>("fmi_approvalstatus").Value;
            var decision = DirectDecisionMapper.FromApprovalStatus(newStatus);

            if (decision == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Approval Status can only be changed directly to Approved or Rejected.");
            }

            var decisionComment = target.Contains("fmi_decisioncomments")
                ? target.GetAttributeValue<string>("fmi_decisioncomments")
                : null;

            if (!DecisionCommentRule.IsSatisfied(decision.Value, decisionComment))
            {
                throw new InvalidPluginExecutionException(
                    "A decision comment is required when rejecting a Deal Approval.");
            }

            var draft = DecisionUpdateBuilder.Build(decision.Value, context.InitiatingUserId, decisionComment, DateTime.UtcNow);

            target["fmi_decisionon"] = draft.DecisionOn;
            target["fmi_decisionby"] = new EntityReference(SystemUserLogicalName, draft.DecisionBy);

            if (draft.DecisionComment != null)
            {
                target["fmi_decisioncomments"] = draft.DecisionComment;
            }

            var opportunityUpdate = new Entity(OpportunityLogicalName, opportunityRef.Id);
            opportunityUpdate["fmi_currentapprovalstatus"] = new OptionSetValue(draft.OpportunityApprovalStatus);

            elevatedService.Update(opportunityUpdate);
        }
    }
}
