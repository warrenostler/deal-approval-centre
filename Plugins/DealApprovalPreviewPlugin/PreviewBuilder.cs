using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Builds the read-only Deal Approval financial preview for one Opportunity. Financial
    /// resolution itself (validateRequiredLookups, Content-&gt;BWG, Target Territory-&gt;BWT,
    /// Budget/Forecast matching) now lives in the shared FinancialSnapshotResolver, reused by
    /// SubmitDealApproval so both APIs resolve identical results for the same persisted
    /// Opportunity state. This class only adds the Preview-specific JSON response shape on top.
    /// Every branch that is a hard failure in the resolver is a hard failure here too - nothing
    /// is silently skipped or defaulted.
    /// </summary>
    internal sealed class PreviewBuilder
    {
        private const string OpportunityLogicalName = "opportunity";

        /// <summary>
        /// Ancillary Opportunities never have Opportunity Items - this is the one Sales Type
        /// where zero resolved items is expected, not a data problem.
        /// </summary>
        private const int SalesTypeAncillary = 797300008;

        /// <summary>
        /// Sales Types with no Budget/Forecast comparison concept at all. Content used for these
        /// can still incidentally map to a Business Written Group/Budget meant for other Sales
        /// Types, so the comparison must be explicitly skipped rather than left to fall through
        /// as "no matching Budget found".
        /// </summary>
        private const int SalesTypeInflight = 797300006;
        private const int SalesTypeHomeEntertainment = 797300007;

        /// <summary>
        /// Format Sale resolves Business Written Group from the Opportunity's Parent Format
        /// (shared by every item), not per-item Content - see
        /// FinancialSnapshotResolver.ResolveBusinessWrittenGroupForFormat.
        /// </summary>
        private const int SalesTypeFormatSale = 797300001;

        private readonly IOrganizationService _service;
        private readonly FinancialSnapshotResolver _resolver;

        public PreviewBuilder(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _resolver = new FinancialSnapshotResolver(service);
        }

        public string Build(Guid opportunityId)
        {
            if (opportunityId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("A valid Opportunity is required.");
            }

            var opportunity = RetrieveOpportunity(opportunityId);
            var salesType = opportunity.GetAttributeValue<OptionSetValue>("fmi_salestype")?.Value;
            var skipFinancialComparison = salesType == SalesTypeInflight ||
                salesType == SalesTypeHomeEntertainment ||
                salesType == SalesTypeAncillary;

            List<ResolvedFinancialItem> resolvedItems;

            if (salesType == SalesTypeFormatSale)
            {
                var parentFormatRef = opportunity.GetAttributeValue<EntityReference>("fmi_parentformat");

                if (parentFormatRef == null)
                {
                    throw new InvalidPluginExecutionException(
                        "This Opportunity's Sales Type is Format Sale, so a Parent Format must be set before it can be previewed.");
                }

                var formatBusinessWrittenGroup = _resolver.ResolveBusinessWrittenGroupForFormat(parentFormatRef.Id);
                resolvedItems = _resolver.Resolve(opportunityId, useFixedBusinessWrittenGroup: true, fixedBusinessWrittenGroup: formatBusinessWrittenGroup);
            }
            else
            {
                resolvedItems = _resolver.Resolve(opportunityId, skipFinancialComparison);
            }

            if (resolvedItems.Count == 0 && salesType != SalesTypeAncillary)
            {
                throw new InvalidPluginExecutionException(
                    "This Opportunity has no Opportunity Items, so there is nothing to preview.");
            }

            var previewItems = resolvedItems.Select(BuildPreviewItem).ToList();

            return BuildResponseJson(opportunityId, opportunity, previewItems, salesType);
        }

        // --- Retrieval ------------------------------------------------------------------

        private Entity RetrieveOpportunity(Guid opportunityId)
        {
            try
            {
                return _service.Retrieve(OpportunityLogicalName, opportunityId, new ColumnSet("name", "fmi_salestype", "fmi_parentformat"));
            }
            catch (Exception)
            {
                throw new InvalidPluginExecutionException(
                    "The Opportunity could not be found, or you do not have permission to view it.");
            }
        }

        // --- Response assembly -----------------------------------------------------------

        private Dictionary<string, object> BuildPreviewItem(ResolvedFinancialItem item)
        {
            var financials = item.Financials;

            return new Dictionary<string, object>
            {
                ["opportunityItemId"] = item.OpportunityItemId.ToString(),
                ["contentId"] = item.ContentRef.Id.ToString(),
                ["title"] = item.ContentRef.Name ?? item.OpportunityItemName,
                ["targetTerritoryId"] = item.TargetTerritoryRef.Id.ToString(),
                ["territory"] = item.TargetTerritoryRef.Name,
                ["businessWrittenGroupId"] = item.BusinessWrittenGroupRef?.Id.ToString(),
                ["businessWrittenGroup"] = item.BusinessWrittenGroupRef?.Name,
                ["includeInVariance"] = item.IncludeInVariance,
                ["businessWrittenTerritoryId"] = item.BusinessWrittenTerritoryRef?.Id.ToString(),
                ["businessWrittenTerritory"] = item.BusinessWrittenTerritoryRef?.Name,
                ["businessWrittenYearId"] = item.BusinessWrittenYearRef.Id.ToString(),
                ["businessWrittenYear"] = item.BusinessWrittenYearRef.Name,
                ["sale"] = financials.Sale,
                ["saleGp"] = (object)financials.SaleGp ?? null,
                ["budget"] = (object)financials.Budget ?? null,
                ["budgetGp"] = (object)financials.BudgetGp ?? null,
                ["fc1"] = (object)financials.Fc1 ?? null,
                ["fc2"] = (object)financials.Fc2 ?? null,
                ["fc3"] = (object)financials.Fc3 ?? null,
                ["latestForecast"] = (object)financials.LatestForecast ?? null,
                ["latestForecastType"] = financials.LatestForecastType,
                ["varianceToForecast"] = (object)financials.VarianceToForecast ?? null,
                ["varianceToBudget"] = (object)financials.VarianceToBudget ?? null,
                ["belowForecast"] = financials.BelowForecast,
                ["financialComparisonAvailable"] = financials.FinancialComparisonAvailable,
                ["financialWarning"] = financials.FinancialWarning
            };
        }

        private static string BuildResponseJson(Guid opportunityId, Entity opportunity, List<Dictionary<string, object>> items, int? salesType)
        {
            var response = new Dictionary<string, object>
            {
                ["opportunityId"] = opportunityId.ToString(),
                ["opportunityName"] = opportunity.GetAttributeValue<string>("name") ?? string.Empty,
                ["salesType"] = (object)salesType ?? null,
                ["salesTypeLabel"] = opportunity.FormattedValues.Contains("fmi_salestype") ? opportunity.FormattedValues["fmi_salestype"] : null,
                ["items"] = items
            };

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return serializer.Serialize(response);
        }
    }
}
