using System.Collections.Generic;
using System.Linq;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Server-side enforcement of the coordinator-comment rule: if any resolved item is below
    /// the latest Forecast, a non-blank coordinator comment is mandatory before an Opportunity
    /// can be submitted for approval. The custom page enforces this too, but the server must not
    /// trust the client to have done so.
    /// </summary>
    public static class CoordinatorCommentRule
    {
        public static bool IsRequired(IEnumerable<ItemFinancialResult> items)
        {
            return items.Any(item => item.BelowForecast);
        }

        public static bool IsSatisfied(IEnumerable<ItemFinancialResult> items, string coordinatorComment)
        {
            var materialized = items as IList<ItemFinancialResult> ?? items.ToList();
            return !IsRequired(materialized) || !string.IsNullOrWhiteSpace(coordinatorComment);
        }
    }
}
