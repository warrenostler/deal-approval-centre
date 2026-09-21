using System;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Synchronous server-side Approve/Reject decision on a Pending fmi_dealapproval, backing the
    /// fmi_ProcessDealApprovalDecision Custom API. Re-validates state and authorization itself
    /// (never trusts client-supplied assumptions), then updates the Deal Approval and its
    /// Opportunity in the same synchronous plugin execution. Both IOrganizationService.Update
    /// calls below run inside the single database transaction that Dataverse opens for the
    /// Custom API's own message request - synchronous plugins registered on a message all
    /// execute within that same ambient transaction. No ExecuteTransactionRequest or compensating
    /// delete/update logic is introduced; if either Update throws, the platform rolls back both.
    ///
    /// Direct table Update by ordinary callers is prevented by Dataverse security privileges
    /// (business users get Read-only on fmi_dealapproval), not by a marker-authenticating guard -
    /// an earlier SharedVariables/ParentContext design, and later an HMAC request-signing design,
    /// were both tried and abandoned: the former was proven live in FM TEST not to work for this
    /// project's nested Custom-API-triggered calls at any pipeline stage, and the latter was a
    /// working but unwanted increase in complexity once the security-privilege model made an
    /// authenticating guard unnecessary in the first place.
    ///
    /// The caller (ProcessDealApprovalDecision) passes an elevated service - see
    /// IOrganizationServiceFactory.CreateOrganizationService(null) - not the InitiatingUserService
    /// used elsewhere in this project. Every Dataverse call this class makes (retrieving the Deal
    /// Approval, the Super Approver membership check, and the final writes) uses that same
    /// service, so none of them depend on what privilege the actual human caller's own security
    /// role happens to grant. Authorization is entirely separate from that and is never weakened
    /// by it: it is decided purely by comparing callerId (the real human, from
    /// context.InitiatingUserId) against the retrieved fmi_approver and Super Approver membership,
    /// before any write is attempted.
    /// </summary>
    internal sealed class DecisionBuilder
    {
        private const string DealApprovalLogicalName = "fmi_dealapproval";
        private const string OpportunityLogicalName = "opportunity";
        private const string SystemUserLogicalName = "systemuser";

        private readonly IOrganizationService _service;
        private readonly Guid _callerId;
        private readonly SuperApproverResolver _superApproverResolver;

        public DecisionBuilder(IOrganizationService service, Guid callerId)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _callerId = callerId;
            _superApproverResolver = new SuperApproverResolver(service);
        }

        public ProcessDealApprovalDecisionResult Process(
            Guid dealApprovalId,
            DealApprovalDecision decision,
            string decisionComment)
        {
            if (dealApprovalId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("A valid Deal Approval is required.");
            }

            var dealApproval = RetrieveDealApproval(dealApprovalId);

            var opportunityRef = dealApproval.GetAttributeValue<EntityReference>("fmi_opportunity");

            if (opportunityRef == null)
            {
                throw new InvalidPluginExecutionException(
                    "This Deal Approval has no Opportunity, so a decision cannot be recorded.");
            }

            var currentStatus = dealApproval.GetAttributeValue<OptionSetValue>("fmi_approvalstatus")?.Value;

            if (!DealApprovalStateGuard.CanDecide(currentStatus))
            {
                throw new InvalidPluginExecutionException(
                    "This Deal Approval is not Pending, so a decision cannot be recorded on it. " +
                    "A decision can only be made once.");
            }

            var approverRef = dealApproval.GetAttributeValue<EntityReference>("fmi_approver");
            var callerIsSuperApprover = _superApproverResolver.IsSuperApprover(_callerId);

            if (!DealApprovalDecisionAuthorization.IsAuthorized(approverRef?.Id, _callerId, callerIsSuperApprover))
            {
                throw new InvalidPluginExecutionException(
                    "You are not authorised to make a decision on this Deal Approval.");
            }

            if (!DecisionCommentRule.IsSatisfied(decision, decisionComment))
            {
                throw new InvalidPluginExecutionException(
                    "A decision comment is required when rejecting a Deal Approval.");
            }

            var draft = DecisionUpdateBuilder.Build(decision, _callerId, decisionComment, DateTime.UtcNow);

            var dealApprovalUpdate = new Entity(DealApprovalLogicalName, dealApprovalId);
            dealApprovalUpdate["fmi_approvalstatus"] = new OptionSetValue(draft.ApprovalStatus);
            dealApprovalUpdate["fmi_decisionon"] = draft.DecisionOn;
            dealApprovalUpdate["fmi_decisionby"] = new EntityReference(SystemUserLogicalName, draft.DecisionBy);

            if (draft.DecisionComment != null)
            {
                dealApprovalUpdate["fmi_decisioncomments"] = draft.DecisionComment;
            }

            _service.Update(dealApprovalUpdate);

            // fmi_currentdealapproval on the Opportunity already points at this exact Deal
            // Approval (Submit set it when this record was created) - only the status changes.
            var opportunityUpdate = new Entity(OpportunityLogicalName, opportunityRef.Id);
            opportunityUpdate["fmi_currentapprovalstatus"] = new OptionSetValue(draft.OpportunityApprovalStatus);

            _service.Update(opportunityUpdate);

            return new ProcessDealApprovalDecisionResult
            {
                DealApprovalId = dealApprovalId,
                ApprovalStatus = draft.ApprovalStatus
            };
        }

        private Entity RetrieveDealApproval(Guid dealApprovalId)
        {
            try
            {
                return _service.Retrieve(
                    DealApprovalLogicalName,
                    dealApprovalId,
                    new ColumnSet("fmi_approvalstatus", "fmi_approver", "fmi_opportunity"));
            }
            catch (Exception)
            {
                throw new InvalidPluginExecutionException(
                    "The Deal Approval could not be found, or you do not have permission to view it.");
            }
        }
    }

    /// <summary>
    /// Output shape for ProcessDealApprovalDecision: the Deal Approval decided, and its resulting
    /// fmi_approvalstatus value.
    /// </summary>
    internal sealed class ProcessDealApprovalDecisionResult
    {
        public Guid DealApprovalId { get; set; }
        public int ApprovalStatus { get; set; }
    }
}
