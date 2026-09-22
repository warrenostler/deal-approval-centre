namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Maps a raw fmi_dealapproval.fmi_approvalstatus value back to the DealApprovalDecision that
    /// would produce it, for the one path where a decision arrives as a direct status edit rather
    /// than through the fmi_ProcessDealApprovalDecision Custom API's own Decision input. Null for
    /// any status that is not a valid direct-edit decision target (Pending/Cancelled/Failed).
    /// </summary>
    public static class DirectDecisionMapper
    {
        public static DealApprovalDecision? FromApprovalStatus(int approvalStatus)
        {
            if (approvalStatus == DealApprovalStatus.Approved) return DealApprovalDecision.Approve;
            if (approvalStatus == DealApprovalStatus.Rejected) return DealApprovalDecision.Reject;
            return null;
        }
    }
}
