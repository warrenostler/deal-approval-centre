using System;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// One Opportunity Item resolved against Content-&gt;BWG, Target Territory-&gt;BWT and
    /// Budget/Forecast, as produced by FinancialSnapshotResolver. Shared read model consumed by
    /// both GetDealApprovalPreview (for its JSON response) and SubmitDealApproval (for the
    /// fmi_dealapprovalitem deep-insert children and the fmi_submitteddealitems snapshot text).
    /// </summary>
    internal sealed class ResolvedFinancialItem
    {
        public Guid OpportunityItemId { get; set; }
        public string OpportunityItemName { get; set; }
        public EntityReference ContentRef { get; set; }
        public EntityReference TargetTerritoryRef { get; set; }
        public EntityReference BusinessWrittenGroupRef { get; set; }

        /// <summary>
        /// The resolved Business Written Group's fmi_includeinvariances flag. True when there is
        /// no Business Written Group to read it from (Unavailable financial results already
        /// suppress the numbers this would otherwise gate) - see FinancialSnapshotResolver.
        /// </summary>
        public bool IncludeInVariance { get; set; } = true;

        public EntityReference BusinessWrittenTerritoryRef { get; set; }
        public EntityReference BusinessWrittenYearRef { get; set; }

        /// <summary>
        /// Only used to reconstruct the legacy fmi_submitteddealitems snapshot text - never
        /// exposed in the Preview API's JSON contract or on fmi_dealapprovalitem.
        /// </summary>
        public DateTime? LicenceStart { get; set; }

        public DateTime? LicenceEnd { get; set; }

        public ItemFinancialResult Financials { get; set; }
    }
}
