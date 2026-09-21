using System;

namespace DealApprovalPreviewPlugin.Core
{
    /// <summary>
    /// A Dataverse-independent view of one resolved Opportunity Item - the same identities and
    /// names PreviewBuilder/SubmitBuilder resolve via Dataverse, expressed as plain Guids/strings
    /// so the submission-item mapping can be unit tested without an IOrganizationService.
    /// </summary>
    public class ResolvedOpportunityItem
    {
        public Guid OpportunityItemId { get; set; }
        public Guid ContentId { get; set; }
        public string ContentName { get; set; }
        public Guid TargetTerritoryId { get; set; }
        public string TargetTerritoryName { get; set; }

        /// <summary>Null when the Content has no Business Written Group mapping.</summary>
        public Guid? BusinessWrittenGroupId { get; set; }
        public string BusinessWrittenGroupName { get; set; }

        /// <summary>Null when the Target Territory has no Business Written Territory mapping.</summary>
        public Guid? BusinessWrittenTerritoryId { get; set; }
        public string BusinessWrittenTerritoryName { get; set; }
        public Guid BusinessWrittenYearId { get; set; }
        public string BusinessWrittenYearName { get; set; }
        public ItemFinancialResult Financials { get; set; }
    }
}
