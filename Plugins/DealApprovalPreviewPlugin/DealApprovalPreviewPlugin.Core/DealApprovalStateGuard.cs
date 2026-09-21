namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Whether a decision may be made on a Deal Approval given its current status. A decision
    /// must never be applied twice, so only a currently-Pending record is eligible.
    /// </summary>
    public static class DealApprovalStateGuard
    {
        public static bool CanDecide(int? currentApprovalStatus)
        {
            return currentApprovalStatus == DealApprovalStatus.Pending;
        }
    }
}
