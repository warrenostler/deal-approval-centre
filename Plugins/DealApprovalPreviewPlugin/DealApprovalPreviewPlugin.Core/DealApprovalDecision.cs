namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// The requested action on a Pending Deal Approval, as accepted by the
    /// fmi_ProcessDealApprovalDecision Custom API's Decision input parameter. Deliberately a
    /// distinct numbering scheme from fmi_dealapproval.fmi_approvalstatus's own option values
    /// (Pending=1/Approved=2/Rejected=3/...) and from Opportunity.fmi_currentapprovalstatus's
    /// values - the three must never be conflated.
    /// </summary>
    public enum DealApprovalDecision
    {
        Approve = 1,
        Reject = 2
    }
}
