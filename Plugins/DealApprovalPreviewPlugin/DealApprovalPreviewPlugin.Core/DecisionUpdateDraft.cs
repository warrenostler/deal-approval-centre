using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// The complete, plain-data set of field values a decision produces for fmi_dealapproval and
    /// its Opportunity. Deliberately has no Approver property at all - "the assigned approver is
    /// never overwritten by a decision" is guaranteed by this type's shape, not by remembering
    /// not to set a field.
    /// </summary>
    public sealed class DecisionUpdateDraft
    {
        public int ApprovalStatus { get; set; }
        public DateTime DecisionOn { get; set; }
        public Guid DecisionBy { get; set; }

        /// <summary>Null when no comment was supplied - the caller leaves the field untouched.</summary>
        public string DecisionComment { get; set; }

        public int OpportunityApprovalStatus { get; set; }
    }
}
