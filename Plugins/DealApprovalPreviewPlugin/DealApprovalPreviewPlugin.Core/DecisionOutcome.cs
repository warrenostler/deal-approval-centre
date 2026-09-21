namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Pure mapping from a decision to the resulting fmi_dealapproval.fmi_approvalstatus and
    /// Opportunity.fmi_currentapprovalstatus values it produces. Both target option sets have
    /// their own independent numbering (see DealApprovalStatus) - this is the single place that
    /// translates a decision into both of them together, so the two updates can never drift out
    /// of sync with each other.
    /// </summary>
    public static class DecisionOutcome
    {
        // Opportunity.fmi_currentapprovalstatus values, as given by the existing implementation
        // (mirrors SubmitBuilder's own OpportunityApprovalStatusPending = 2).
        public const int OpportunityApprovalStatusApproved = 3;
        public const int OpportunityApprovalStatusRejected = 4;

        public static int ResultingDealApprovalStatus(DealApprovalDecision decision)
        {
            return decision == DealApprovalDecision.Approve
                ? DealApprovalStatus.Approved
                : DealApprovalStatus.Rejected;
        }

        public static int ResultingOpportunityApprovalStatus(DealApprovalDecision decision)
        {
            return decision == DealApprovalDecision.Approve
                ? OpportunityApprovalStatusApproved
                : OpportunityApprovalStatusRejected;
        }
    }
}
