using System;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Cancels a pending Deal Approval request created in error for an Opportunity at a supported approval stage.
    /// Intended to back the fmi_CancelDealApprovalRequest Custom API with an OpportunityId Guid
    /// request parameter and no response properties.
    /// </summary>
    public class CancelDealApprovalRequest : PluginBase
    {
        private const string OpportunityLogicalName = "opportunity";
        private const string DealApprovalLogicalName = "fmi_dealapproval";
        private const int OpportunityApprovalStatusNotRequested = 1;
        private const int OpportunityApprovalStatusPending = 2;
        private const int OfferStage = 797300003;
        private const int UnauthorisedStage = 797300000;
        private const int AuthorisedStage = 797300001;
        private const int DealApprovalStatusPending = 1;
        private const int DealApprovalStatusCancelled = 4;

        public CancelDealApprovalRequest(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(CancelDealApprovalRequest))
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

            var service = localPluginContext.OrgSvcFactory.CreateOrganizationService(null);
            var opportunity = RetrieveOpportunity(service, opportunityId);
            var stage = opportunity.GetAttributeValue<OptionSetValue>("fmi_stage")?.Value;
            var opportunityApprovalStatus = stage == OfferStage
                ? OpportunityApprovalStatusNotRequested
                : stage == UnauthorisedStage || stage == AuthorisedStage
                    ? OpportunityApprovalStatusPending
                    : throw new InvalidPluginExecutionException(
                        "Approval cancellation is only supported at Offer, Unauthorised, or Authorised stage.");

            var approvalRef = opportunity.GetAttributeValue<EntityReference>("fmi_currentdealapproval");
            if (approvalRef == null)
            {
                throw new InvalidPluginExecutionException(
                    "This Opportunity does not have a current Deal Approval request to cancel.");
            }

            var approval = RetrieveApproval(service, approvalRef.Id);
            var approvalStatus = approval.GetAttributeValue<OptionSetValue>("fmi_approvalstatus")?.Value;
            if (approvalStatus != DealApprovalStatusPending)
            {
                throw new InvalidPluginExecutionException(
                    "The current Deal Approval is no longer Pending and cannot be cancelled.");
            }

            var requesterRef = approval.GetAttributeValue<EntityReference>("fmi_requestedby");
            if (requesterRef == null || requesterRef.Id != context.InitiatingUserId)
            {
                throw new InvalidPluginExecutionException(
                    "Only the user who submitted this approval request can cancel it.");
            }

            var approvalUpdate = new Entity(DealApprovalLogicalName, approvalRef.Id);
            approvalUpdate["fmi_approvalstatus"] = new OptionSetValue(DealApprovalStatusCancelled);
            approvalUpdate["fmi_cancelledon"] = DateTime.UtcNow;
            approvalUpdate["fmi_cancelledby"] = new EntityReference("systemuser", context.InitiatingUserId);
            service.Update(approvalUpdate);

            var opportunityUpdate = new Entity(OpportunityLogicalName, opportunityId);
            opportunityUpdate["fmi_currentapprovalstatus"] = new OptionSetValue(opportunityApprovalStatus);
            opportunityUpdate["fmi_currentdealapproval"] = null;
            service.Update(opportunityUpdate);
        }

        private static Entity RetrieveOpportunity(IOrganizationService service, Guid opportunityId)
        {
            try
            {
                return service.Retrieve(
                    OpportunityLogicalName,
                    opportunityId,
                    new ColumnSet("fmi_stage", "fmi_currentapprovalstatus", "fmi_currentdealapproval"));
            }
            catch (Exception)
            {
                throw new InvalidPluginExecutionException(
                    "The Opportunity could not be found, or you do not have permission to view it.");
            }
        }

        private static Entity RetrieveApproval(IOrganizationService service, Guid approvalId)
        {
            try
            {
                return service.Retrieve(
                    DealApprovalLogicalName,
                    approvalId,
                    new ColumnSet("fmi_approvalstatus", "fmi_requestedby"));
            }
            catch (Exception)
            {
                throw new InvalidPluginExecutionException(
                    "The current Deal Approval could not be retrieved.");
            }
        }
    }
}
