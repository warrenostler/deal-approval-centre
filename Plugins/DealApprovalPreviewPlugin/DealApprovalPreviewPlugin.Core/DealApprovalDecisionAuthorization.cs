using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// The Deal Approval decision authorization rule: the caller may decide only if they are the
    /// assigned approver, or a member of the Deal SuperApprover team. Dataverse-independent - the
    /// caller resolves both inputs (does fmi_approver match? is the caller a Super Approver?)
    /// before calling IsAuthorized, so this class contains only the decision itself and is fully
    /// unit-testable.
    /// </summary>
    public static class DealApprovalDecisionAuthorization
    {
        public static bool IsAuthorized(Guid? approverId, Guid callerId, bool callerIsSuperApprover)
        {
            return (approverId.HasValue && approverId.Value == callerId) || callerIsSuperApprover;
        }
    }
}
