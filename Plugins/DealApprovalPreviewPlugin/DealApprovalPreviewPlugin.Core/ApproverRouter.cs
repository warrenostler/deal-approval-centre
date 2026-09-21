using System;
using System.Collections.Generic;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Decides which single systemuser a whole Opportunity's Deal Approval routes to, from each
    /// Opportunity Item's Target Territory Default Approver plus the environment-variable
    /// fallback approver. Dataverse-independent: the caller resolves the fallback's configuration
    /// validity and each candidate approver's active status before calling Resolve, so this class
    /// contains only the routing decision itself and is fully unit-testable.
    ///
    /// Rule: route to a specific approver only when every Opportunity Item's Target Territory
    /// resolves to a Default Approver, all of those are the same user, and that user is not the
    /// fallback approver. Any blank territory routing, any mix of different approvers, or a
    /// unanimous approver who turns out to be inactive all fall back to the environment-variable
    /// approver instead. The fallback approver itself must still be configured and active - if
    /// not, that is a hard configuration error, not a silent no-op.
    /// </summary>
    public static class ApproverRouter
    {
        public static ApproverRoutingResult Resolve(
            Guid? fallbackApproverId,
            bool fallbackApproverIsActive,
            IEnumerable<Guid?> territoryApproverIds,
            Func<Guid, bool> isApproverActive)
        {
            if (territoryApproverIds == null)
            {
                throw new ArgumentNullException(nameof(territoryApproverIds));
            }

            if (isApproverActive == null)
            {
                throw new ArgumentNullException(nameof(isApproverActive));
            }

            if (!fallbackApproverId.HasValue)
            {
                return ApproverRoutingResult.ConfigurationError(
                    "Deal Approval Default Approver is not configured correctly. The " +
                    "fmi_DealApprovalDefaultApprover environment variable is missing, blank, or " +
                    "does not contain a valid user ID.");
            }

            if (!fallbackApproverIsActive)
            {
                return ApproverRoutingResult.ConfigurationError(
                    "Deal Approval Default Approver is not configured correctly. The configured " +
                    "user (" + fallbackApproverId.Value.ToString("D") + ") does not exist or is not active.");
            }

            var unanimousTerritoryApprover = ResolveUnanimousTerritoryApprover(territoryApproverIds);

            if (unanimousTerritoryApprover.HasValue &&
                unanimousTerritoryApprover.Value != fallbackApproverId.Value &&
                isApproverActive(unanimousTerritoryApprover.Value))
            {
                return ApproverRoutingResult.Resolved(unanimousTerritoryApprover.Value);
            }

            return ApproverRoutingResult.Resolved(fallbackApproverId.Value);
        }

        /// <summary>
        /// Null if any Opportunity Item's Target Territory has no Default Approver, or if the
        /// resolved approvers are not all the same user. Otherwise the single unanimous approver.
        /// </summary>
        private static Guid? ResolveUnanimousTerritoryApprover(IEnumerable<Guid?> territoryApproverIds)
        {
            Guid? candidate = null;

            foreach (var territoryApproverId in territoryApproverIds)
            {
                if (!territoryApproverId.HasValue)
                {
                    return null;
                }

                if (!candidate.HasValue)
                {
                    candidate = territoryApproverId;
                }
                else if (candidate.Value != territoryApproverId.Value)
                {
                    return null;
                }
            }

            return candidate;
        }
    }
}
