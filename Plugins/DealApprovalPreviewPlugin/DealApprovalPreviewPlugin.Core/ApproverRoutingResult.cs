using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Outcome of ApproverRouter.Resolve: either a resolved approver systemuser ID, or a
    /// configuration error that must block submission entirely (the fallback approver itself is
    /// not usable).
    /// </summary>
    public sealed class ApproverRoutingResult
    {
        public bool IsConfigurationError { get; private set; }
        public string ConfigurationErrorMessage { get; private set; }
        public Guid ApproverId { get; private set; }

        public static ApproverRoutingResult ConfigurationError(string message)
        {
            return new ApproverRoutingResult
            {
                IsConfigurationError = true,
                ConfigurationErrorMessage = message
            };
        }

        public static ApproverRoutingResult Resolved(Guid approverId)
        {
            return new ApproverRoutingResult
            {
                ApproverId = approverId
            };
        }
    }
}
