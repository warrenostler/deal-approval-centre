namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// fmi_dealapproval.fmi_approvalstatus option values, confirmed from the FM TEST solution
    /// export's Entities/fmi_DealApproval/Entity.xml optionset
    /// (fmi_dealapproval_fmi_approvalstatus) - not assumed. Deliberately a distinct numbering
    /// scheme from Opportunity.fmi_currentapprovalstatus and from DealApprovalDecision.
    /// </summary>
    public static class DealApprovalStatus
    {
        public const int Pending = 1;
        public const int Approved = 2;
        public const int Rejected = 3;
        public const int Cancelled = 4;
        public const int Failed = 5;
    }
}
