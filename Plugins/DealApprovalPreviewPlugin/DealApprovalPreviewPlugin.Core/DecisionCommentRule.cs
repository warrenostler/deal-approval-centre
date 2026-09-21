namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Server-side enforcement of the decision-comment rule: optional when approving, mandatory
    /// when rejecting. Mirrors CoordinatorCommentRule's shape for the Submit flow.
    /// </summary>
    public static class DecisionCommentRule
    {
        public static bool IsRequired(DealApprovalDecision decision)
        {
            return decision == DealApprovalDecision.Reject;
        }

        public static bool IsSatisfied(DealApprovalDecision decision, string decisionComment)
        {
            return !IsRequired(decision) || !string.IsNullOrWhiteSpace(decisionComment);
        }
    }
}
