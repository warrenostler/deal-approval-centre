using System;
using System.Collections.Generic;
using System.Linq;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// Builds exactly one DealApprovalItemDraft per resolved Opportunity Item - deliberately a
    /// 1:1 map with no grouping/aggregation, unlike the retired JS's groupItemsByGoal. The
    /// business requirement is one fmi_dealapprovalitem per Opportunity Item even when several
    /// items share the same Goal/Budget combination.
    /// </summary>
    public static class DealApprovalItemDraftBuilder
    {
        public static List<DealApprovalItemDraft> Build(IEnumerable<ResolvedOpportunityItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            return items.Select(item => new DealApprovalItemDraft
            {
                OpportunityItemId = item.OpportunityItemId,
                ContentId = item.ContentId,
                TargetTerritoryId = item.TargetTerritoryId,
                BusinessWrittenGroupId = item.BusinessWrittenGroupId,
                BusinessWrittenTerritoryId = item.BusinessWrittenTerritoryId,
                BusinessWrittenYearId = item.BusinessWrittenYearId,
                Name = BuildName(item),
                Financials = item.Financials
            }).ToList();
        }

        private static string BuildName(ResolvedOpportunityItem item)
        {
            return string.Join(
                " - ",
                new[] { item.ContentName, item.TargetTerritoryName, item.BusinessWrittenYearName }
                    .Where(part => !string.IsNullOrEmpty(part)));
        }
    }
}
