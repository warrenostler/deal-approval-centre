using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Builds the plain-data field values a decision produces, from already-validated inputs
    /// (state, authorization and the comment rule are checked before this is called - this class
    /// only computes what to write). Dataverse-independent and fully unit-testable; DecisionBuilder
    /// turns the result into an Entity update.
    /// </summary>
    public static class DecisionUpdateBuilder
    {
        public static DecisionUpdateDraft Build(
            DealApprovalDecision decision,
            Guid callerId,
            string decisionComment,
            DateTime decisionOn)
        {
            var trimmedComment = string.IsNullOrWhiteSpace(decisionComment) ? null : decisionComment.Trim();

            return new DecisionUpdateDraft
            {
                ApprovalStatus = DecisionOutcome.ResultingDealApprovalStatus(decision),
                DecisionOn = decisionOn,
                DecisionBy = callerId,
                DecisionComment = trimmedComment,
                OpportunityApprovalStatus = DecisionOutcome.ResultingOpportunityApprovalStatus(decision)
            };
        }
    }
}
