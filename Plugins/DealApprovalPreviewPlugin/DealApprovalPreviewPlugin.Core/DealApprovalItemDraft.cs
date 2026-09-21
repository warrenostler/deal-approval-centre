using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// The intended one-per-Opportunity-Item fmi_dealapprovalitem submission shape, still
    /// Dataverse-independent. SubmitBuilder turns each draft into an Entity for the deep insert.
    /// </summary>
    public class DealApprovalItemDraft
    {
        public Guid OpportunityItemId { get; set; }
        public Guid ContentId { get; set; }
        public Guid TargetTerritoryId { get; set; }

        /// <summary>Null when the Content has no Business Written Group mapping.</summary>
        public Guid? BusinessWrittenGroupId { get; set; }

        /// <summary>Null when the Target Territory has no Business Written Territory mapping.</summary>
        public Guid? BusinessWrittenTerritoryId { get; set; }

        public Guid BusinessWrittenYearId { get; set; }
        public string Name { get; set; }
        public ItemFinancialResult Financials { get; set; }
    }
}
