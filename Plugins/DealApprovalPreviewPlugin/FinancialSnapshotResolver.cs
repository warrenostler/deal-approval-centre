using System;
using System.Collections.Generic;
using System.Linq;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Resolves the current Dataverse-persisted financial snapshot for every Opportunity Item on
    /// an Opportunity: Content-&gt;BWG, Target Territory-&gt;BWT and Budget/Forecast resolution,
    /// using the same bulk FetchXml approach originally proven in PreviewBuilder. Shared by
    /// GetDealApprovalPreview and SubmitDealApproval so both resolve identical financial results
    /// for the same persisted Opportunity state.
    ///
    /// Two tiers of data problem are handled differently:
    /// - Genuinely invalid/ambiguous data (missing Content/Target Territory/Business Written
    ///   Year, or a Content mapped to more than one Business Written Group) is still a hard
    ///   failure for both callers - nothing is silently skipped or defaulted.
    /// - Missing financial configuration (Content with no BWG, Target Territory with no BWT, or
    ///   no matching Goal/Budget record for a resolved BWG+BWT+BWY combination) no longer throws.
    ///   The row is still produced with everything that could be resolved, but with
    ///   ItemFinancialResult.FinancialComparisonAvailable = false and a human-readable
    ///   FinancialWarning explaining why - a missing Budget/Forecast record can be entirely
    ///   legitimate (e.g. Finance has not yet budgeted a future Business Written Year).
    ///
    /// Returns an empty list when the Opportunity has no Opportunity Items rather than throwing -
    /// callers phrase that failure differently ("nothing to preview" vs "nothing to submit"), so
    /// the empty-list decision belongs to them, not to this shared component.
    /// </summary>
    internal sealed class FinancialSnapshotResolver
    {
        private const string OpportunityItemLogicalName = "fmi_opportunityitem";
        private const string ContentLogicalName = "fmi_content";
        private const string BusinessWrittenGroupLogicalName = "fmi_businesswrittengroup";
        private const string TargetTerritoryLogicalName = "fmi_targetterritory";
        private const string BusinessWrittenTerritoryLogicalName = "fmi_businesswrittenterritory";
        private const string GoalLogicalName = "goal";
        private const string ContentToBwgRelationshipName = "fmi_fmi_businesswrittengroup_fmi_content";

        private readonly IOrganizationService _service;

        /// <summary>
        /// Populated as a side effect of ResolveContentToBusinessWrittenGroup. Keyed by Business
        /// Written Group id rather than Content id because the same Business Written Group is
        /// typically shared by many Content records, and the flag lives on the Business Written
        /// Group itself, not on the Content->BWG link.
        /// </summary>
        private readonly Dictionary<Guid, bool> _includeInVariancesByBusinessWrittenGroupId =
            new Dictionary<Guid, bool>();

        public FinancialSnapshotResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public List<ResolvedFinancialItem> Resolve(Guid opportunityId)
        {
            var items = RetrieveOpportunityItems(opportunityId);

            if (items.Count == 0)
            {
                return new List<ResolvedFinancialItem>();
            }

            var rows = items.Select(ReadItemRow).ToList();

            ValidateRequiredLookups(rows);

            var bwgByContentId = ResolveContentToBusinessWrittenGroup(
                rows.Select(r => r.ContentRef.Id).Distinct());

            var bwtByTerritoryId = ResolveTerritoryToBusinessWrittenTerritory(
                rows.Select(r => r.TerritoryRef.Id).Distinct());

            var resolved = rows
                .Select(r => Resolve(r, bwgByContentId, bwtByTerritoryId))
                .ToList();

            var budgetsByCombination = ResolveBudgets(resolved);

            return resolved
                .Select(r => BuildResolvedFinancialItem(r, budgetsByCombination))
                .ToList();
        }

        // --- Retrieval ------------------------------------------------------------------

        private List<Entity> RetrieveOpportunityItems(Guid opportunityId)
        {
            var fetchXml = $@"
                <fetch>
                  <entity name='{OpportunityItemLogicalName}'>
                    <attribute name='fmi_opportunityitemid' />
                    <attribute name='fmi_name' />
                    <attribute name='fmi_content' />
                    <attribute name='fmi_targetterritory' />
                    <attribute name='fmi_businesswrittenyear' />
                    <attribute name='fmi_actualrevenue_base' />
                    <attribute name='fmi_licensestartdate' />
                    <attribute name='fmi_licenseenddate' />
                    <filter>
                      <condition attribute='fmi_opportunityid' operator='eq' value='{opportunityId:D}' />
                                            <condition attribute='statecode' operator='eq' value='0' />
                    </filter>
                  </entity>
                </fetch>";

            return _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.ToList();
        }

        private ItemRow ReadItemRow(Entity item)
        {
            return new ItemRow
            {
                OpportunityItemId = item.Id,
                Name = item.GetAttributeValue<string>("fmi_name") ?? string.Empty,
                ContentRef = item.GetAttributeValue<EntityReference>("fmi_content"),
                TerritoryRef = item.GetAttributeValue<EntityReference>("fmi_targetterritory"),
                BusinessWrittenYearRef = item.GetAttributeValue<EntityReference>("fmi_businesswrittenyear"),
                Sale = item.GetAttributeValue<Money>("fmi_actualrevenue_base")?.Value ?? 0m,
                LicenceStart = item.GetAttributeValue<DateTime?>("fmi_licensestartdate"),
                LicenceEnd = item.GetAttributeValue<DateTime?>("fmi_licenseenddate")
            };
        }

        private void ValidateRequiredLookups(IEnumerable<ItemRow> rows)
        {
            foreach (var row in rows)
            {
                var identity = string.IsNullOrEmpty(row.Name) ? "An Opportunity Item" : $"Opportunity Item '{row.Name}'";

                if (row.ContentRef == null)
                {
                    throw new InvalidPluginExecutionException($"{identity} does not have Content.");
                }

                if (row.TerritoryRef == null)
                {
                    throw new InvalidPluginExecutionException($"{identity} does not have a Target Territory.");
                }

                if (row.BusinessWrittenYearRef == null)
                {
                    throw new InvalidPluginExecutionException($"{identity} does not have a Business Written Year.");
                }
            }
        }

        /// <summary>
        /// Resolves each distinct Content to at most one Business Written Group via the
        /// fmi_fmi_businesswrittengroup_fmi_content many-to-many relationship. More than one
        /// matching Business Written Group is a hard failure (genuinely ambiguous data); a
        /// Content record that cannot be found at all is also a hard failure (dangling
        /// reference). A Content with zero matching Business Written Groups is not a failure -
        /// it simply has no entry in the returned dictionary, and callers treat that as a
        /// missing-financial-configuration warning rather than blocking submission.
        /// The links are outer joins so that every Content row is returned even with zero BWG
        /// matches - otherwise a Content with no BWG at all would vanish from the result set
        /// entirely, and its name would not be available for the missing-record error below.
        /// </summary>
        private Dictionary<Guid, EntityReference> ResolveContentToBusinessWrittenGroup(
            IEnumerable<Guid> distinctContentIds)
        {
            var contentIds = distinctContentIds.ToList();
            var matchesByContentId = new Dictionary<Guid, List<EntityReference>>();
            var contentNameById = new Dictionary<Guid, string>();

            if (contentIds.Count > 0)
            {
                var fetchXml = $@"
                    <fetch>
                      <entity name='{ContentLogicalName}'>
                        <attribute name='fmi_contentid' />
                        <attribute name='fmi_name' />
                        <filter>
                          {InCondition("fmi_contentid", contentIds)}
                        </filter>
                        <link-entity name='{ContentToBwgRelationshipName}' from='fmi_contentid' to='fmi_contentid' intersect='true' visible='false' link-type='outer'>
                          <link-entity name='{BusinessWrittenGroupLogicalName}' from='fmi_businesswrittengroupid' to='fmi_businesswrittengroupid' alias='bwg' link-type='outer'>
                            <attribute name='fmi_businesswrittengroupid' />
                            <attribute name='fmi_name' />
                            <attribute name='fmi_includeinvariances' />
                          </link-entity>
                        </link-entity>
                      </entity>
                    </fetch>";

                foreach (var content in _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities)
                {
                    var contentId = content.Id;
                    contentNameById[contentId] = content.GetAttributeValue<string>("fmi_name") ?? contentId.ToString();

                    var bwgIdValue = content.GetAttributeValue<AliasedValue>("bwg.fmi_businesswrittengroupid")?.Value;

                    if (bwgIdValue == null)
                    {
                        continue; // this row is the outer-join placeholder for "no BWG match"
                    }

                    var bwgId = (Guid)bwgIdValue;
                    var bwgName = (string)content.GetAttributeValue<AliasedValue>("bwg.fmi_name").Value;

                    // Two Options fields always carry a default once a record is saved, so this is
                    // realistically never null - true (i.e. not suppressed) is the defensive fallback.
                    var includeInVariancesValue = content.GetAttributeValue<AliasedValue>("bwg.fmi_includeinvariances")?.Value;
                    _includeInVariancesByBusinessWrittenGroupId[bwgId] = includeInVariancesValue as bool? ?? true;

                    if (!matchesByContentId.TryGetValue(contentId, out var list))
                    {
                        list = new List<EntityReference>();
                        matchesByContentId[contentId] = list;
                    }

                    list.Add(new EntityReference(BusinessWrittenGroupLogicalName, bwgId) { Name = bwgName });
                }
            }

            var result = new Dictionary<Guid, EntityReference>();

            foreach (var contentId in contentIds)
            {
                if (!contentNameById.TryGetValue(contentId, out var contentName))
                {
                    throw new InvalidPluginExecutionException(
                        "A Content record referenced by an Opportunity Item could not be found.");
                }

                if (!matchesByContentId.TryGetValue(contentId, out var matches) || matches.Count == 0)
                {
                    continue; // no BWG mapping - left out of the result; caller treats as a warning
                }

                if (matches.Count > 1)
                {
                    throw new InvalidPluginExecutionException(
                        $"Content '{contentName}' is assigned to more than one Business Written Group. " +
                        "Approval cannot be submitted until the mapping is corrected.");
                }

                result[contentId] = matches[0];
            }

            return result;
        }

        /// <summary>
        /// Resolves each distinct Target Territory to its Business Written Territory lookup.
        /// A Target Territory record that cannot be found at all is a hard failure (dangling
        /// reference). A Target Territory with no Business Written Territory mapping is not a
        /// failure - it simply has no entry in the returned dictionary, and callers treat that
        /// as a missing-financial-configuration warning rather than blocking submission.
        /// </summary>
        private Dictionary<Guid, EntityReference> ResolveTerritoryToBusinessWrittenTerritory(
            IEnumerable<Guid> distinctTerritoryIds)
        {
            var territoryIds = distinctTerritoryIds.ToList();
            var result = new Dictionary<Guid, EntityReference>();

            if (territoryIds.Count == 0)
            {
                return result;
            }

            var fetchXml = $@"
                <fetch>
                  <entity name='{TargetTerritoryLogicalName}'>
                    <attribute name='fmi_targetterritoryid' />
                    <attribute name='fmi_name' />
                    <attribute name='fmi_businesswrittenterritory' />
                    <filter>
                      {InCondition("fmi_targetterritoryid", territoryIds)}
                    </filter>
                  </entity>
                </fetch>";

            var seen = new HashSet<Guid>();

            foreach (var territory in _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities)
            {
                seen.Add(territory.Id);

                var bwtRef = territory.GetAttributeValue<EntityReference>("fmi_businesswrittenterritory");

                if (bwtRef == null)
                {
                    continue; // no BWT mapping - left out of the result; caller treats as a warning
                }

                result[territory.Id] = bwtRef;
            }

            var missing = territoryIds.Except(seen).FirstOrDefault();

            if (missing != default)
            {
                throw new InvalidPluginExecutionException("A Target Territory referenced by an Opportunity Item could not be retrieved.");
            }

            return result;
        }

        private ResolvedItem Resolve(
            ItemRow row,
            Dictionary<Guid, EntityReference> bwgByContentId,
            Dictionary<Guid, EntityReference> bwtByTerritoryId)
        {
            // TryGetValue rather than the indexer: a Content/Territory with no BWG/BWT mapping
            // has no entry in these dictionaries by design (see the resolvers above), and that is
            // a warning condition here, not a KeyNotFoundException.
            bwgByContentId.TryGetValue(row.ContentRef.Id, out var businessWrittenGroup);
            bwtByTerritoryId.TryGetValue(row.TerritoryRef.Id, out var businessWrittenTerritory);

            var includeInVariance = businessWrittenGroup != null
                && _includeInVariancesByBusinessWrittenGroupId.TryGetValue(businessWrittenGroup.Id, out var include)
                ? include
                : true;

            return new ResolvedItem
            {
                Row = row,
                BusinessWrittenGroup = businessWrittenGroup,
                BusinessWrittenTerritory = businessWrittenTerritory,
                IncludeInVariance = includeInVariance
            };
        }

        /// <summary>
        /// Resolves the single matching Budget ('goal') record for each distinct
        /// BWG + BWT + BWY combination present in the resolved items - only for items that
        /// actually have both a BWG and a BWT (an item missing either has no valid combination
        /// to look up at all, and is already a warning by that point). More than one matching
        /// Budget for a combination is a hard failure (genuinely ambiguous data). Zero matching
        /// Budget records is not a failure - the combination simply has no entry in the returned
        /// dictionary, and the caller treats that as a missing-financial-configuration warning
        /// (a future Business Written Year may legitimately have no Budget yet).
        /// </summary>
        private Dictionary<BudgetCombinationKey, BudgetRow> ResolveBudgets(IEnumerable<ResolvedItem> resolvedItems)
        {
            var descriptionByCombination = new Dictionary<BudgetCombinationKey, string>();

            foreach (var item in resolvedItems)
            {
                if (item.BusinessWrittenGroup == null || item.BusinessWrittenTerritory == null)
                {
                    continue;
                }

                var key = new BudgetCombinationKey(
                    item.BusinessWrittenGroup.Id,
                    item.BusinessWrittenTerritory.Id,
                    item.Row.BusinessWrittenYearRef.Id);

                if (!descriptionByCombination.ContainsKey(key))
                {
                    descriptionByCombination[key] =
                        $"{item.BusinessWrittenGroup.Name} / {item.BusinessWrittenTerritory.Name} / {item.Row.BusinessWrittenYearRef.Name}";
                }
            }

            var combinations = descriptionByCombination.Keys.ToList();

            if (combinations.Count == 0)
            {
                return new Dictionary<BudgetCombinationKey, BudgetRow>();
            }

            var matchesByCombination = new Dictionary<BudgetCombinationKey, List<BudgetRow>>();

            // A per-combination OR'd filter (3 conditions per distinct BWG+BWT+BWY combination)
            // scales with the number of distinct combinations across an Opportunity's items, not
            // with a fixed bound - for a large Opportunity (e.g. 168 items with mostly distinct
            // combinations) this exceeds Dataverse's per-query condition limit and the whole
            // request fails with "Number of conditions in query exceeded maximum limit", found
            // live. Three IN conditions over the distinct BWG/BWT/BWY id sets instead bounds the
            // query by the number of distinct *configuration values* (typically a small, curated
            // set), matching the same IN-based pattern already used by
            // ResolveContentToBusinessWrittenGroup/ResolveTerritoryToBusinessWrittenTerritory
            // above. This can retrieve a superset of Goal rows (any row matching one of each
            // dimension's values, not only the exact combinations actually present), but the
            // matching loop below keys strictly on the real (BWG, BWT, BWY) triplet read from
            // each returned row, and the final result is only ever read for combinations that are
            // genuinely present in `combinations` - so the superset cannot produce a false match.
            var businessWrittenGroupIds = combinations.Select(c => c.BusinessWrittenGroupId).Distinct();
            var businessWrittenTerritoryIds = combinations.Select(c => c.BusinessWrittenTerritoryId).Distinct();
            var businessWrittenYearIds = combinations.Select(c => c.BusinessWrittenYearId).Distinct();

            var fetchXml = $@"
                <fetch>
                  <entity name='{GoalLogicalName}'>
                    <attribute name='goalid' />
                    <attribute name='fmi_businesswrittengroup' />
                    <attribute name='fmi_bwterritory' />
                    <attribute name='fmi_businesswrittenyear' />
                    <attribute name='fmi_currentyearbudget' />
                    <attribute name='fmi_fc1' />
                    <attribute name='fmi_fc2' />
                    <attribute name='fmi_fc3' />
                    <filter type='and'>
                      {InCondition("fmi_businesswrittengroup", businessWrittenGroupIds)}
                      {InCondition("fmi_bwterritory", businessWrittenTerritoryIds)}
                      {InCondition("fmi_businesswrittenyear", businessWrittenYearIds)}
                    </filter>
                  </entity>
                </fetch>";

            foreach (var goal in _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities)
            {
                var key = new BudgetCombinationKey(
                    goal.GetAttributeValue<EntityReference>("fmi_businesswrittengroup").Id,
                    goal.GetAttributeValue<EntityReference>("fmi_bwterritory").Id,
                    goal.GetAttributeValue<EntityReference>("fmi_businesswrittenyear").Id);

                var row = new BudgetRow
                {
                    BudgetId = goal.Id,
                    CurrentYearBudget = goal.GetAttributeValue<Money>("fmi_currentyearbudget")?.Value ?? 0m,
                    Fc1 = goal.GetAttributeValue<Money>("fmi_fc1")?.Value,
                    Fc2 = goal.GetAttributeValue<Money>("fmi_fc2")?.Value,
                    Fc3 = goal.GetAttributeValue<Money>("fmi_fc3")?.Value
                };

                if (!matchesByCombination.TryGetValue(key, out var list))
                {
                    list = new List<BudgetRow>();
                    matchesByCombination[key] = list;
                }

                list.Add(row);
            }

            var result = new Dictionary<BudgetCombinationKey, BudgetRow>();

            foreach (var combination in combinations)
            {
                if (!matchesByCombination.TryGetValue(combination, out var matches) || matches.Count == 0)
                {
                    continue; // no Budget/Forecast record - left out of the result; caller treats as a warning
                }

                if (matches.Count > 1)
                {
                    var description = descriptionByCombination[combination];

                    throw new InvalidPluginExecutionException(
                        $"More than one Budget exists for {description}. " +
                        "The Budget configuration must be corrected before approval can be submitted.");
                }

                result[combination] = matches[0];
            }

            return result;
        }

        private ResolvedFinancialItem BuildResolvedFinancialItem(
            ResolvedItem resolved,
            Dictionary<BudgetCombinationKey, BudgetRow> budgetsByCombination)
        {
            var row = resolved.Row;
            var financials = ResolveFinancials(resolved, budgetsByCombination);

            return new ResolvedFinancialItem
            {
                OpportunityItemId = row.OpportunityItemId,
                OpportunityItemName = row.Name,
                ContentRef = row.ContentRef,
                TargetTerritoryRef = row.TerritoryRef,
                BusinessWrittenYearRef = row.BusinessWrittenYearRef,
                BusinessWrittenGroupRef = resolved.BusinessWrittenGroup,
                IncludeInVariance = resolved.IncludeInVariance,
                BusinessWrittenTerritoryRef = resolved.BusinessWrittenTerritory,
                LicenceStart = row.LicenceStart,
                LicenceEnd = row.LicenceEnd,
                Financials = financials
            };
        }

        /// <summary>
        /// Decides between a genuine financial comparison and a warning-only result, in priority
        /// order: missing BWG, then missing BWT, then no matching Budget/Forecast record for the
        /// resolved BWG+BWT+BWY combination. Only reaches FinancialCalculator.Calculate when all
        /// three are present.
        /// </summary>
        private static ItemFinancialResult ResolveFinancials(
            ResolvedItem resolved,
            Dictionary<BudgetCombinationKey, BudgetRow> budgetsByCombination)
        {
            var row = resolved.Row;

            if (resolved.BusinessWrittenGroup == null)
            {
                return ItemFinancialResult.Unavailable(
                    row.Sale,
                    "Content is not assigned to a Business Written Group.");
            }

            if (resolved.BusinessWrittenTerritory == null)
            {
                return ItemFinancialResult.Unavailable(
                    row.Sale,
                    "Target Territory is not mapped to a Business Written Territory.");
            }

            var key = new BudgetCombinationKey(
                resolved.BusinessWrittenGroup.Id,
                resolved.BusinessWrittenTerritory.Id,
                row.BusinessWrittenYearRef.Id);

            if (!budgetsByCombination.TryGetValue(key, out var budget))
            {
                var description =
                    $"{resolved.BusinessWrittenGroup.Name} / {resolved.BusinessWrittenTerritory.Name} / {row.BusinessWrittenYearRef.Name}";

                return ItemFinancialResult.Unavailable(
                    row.Sale,
                    $"No Budget/Forecast record was found for {description}.");
            }

            return FinancialCalculator.Calculate(row.Sale, budget);
        }

        private static string InCondition(string attributeName, IEnumerable<Guid> ids)
        {
            var values = string.Join(string.Empty, ids.Select(id => $"<value>{id:D}</value>"));
            return $"<condition attribute='{attributeName}' operator='in'>{values}</condition>";
        }

        private sealed class ItemRow
        {
            public Guid OpportunityItemId { get; set; }
            public string Name { get; set; } = string.Empty;
            public EntityReference ContentRef { get; set; }
            public EntityReference TerritoryRef { get; set; }
            public EntityReference BusinessWrittenYearRef { get; set; }
            public decimal Sale { get; set; }
            public DateTime? LicenceStart { get; set; }
            public DateTime? LicenceEnd { get; set; }
        }

        private sealed class ResolvedItem
        {
            public ItemRow Row { get; set; }
            public EntityReference BusinessWrittenGroup { get; set; }
            public EntityReference BusinessWrittenTerritory { get; set; }
            public bool IncludeInVariance { get; set; } = true;
        }

        private readonly struct BudgetCombinationKey : IEquatable<BudgetCombinationKey>
        {
            public BudgetCombinationKey(Guid businessWrittenGroupId, Guid businessWrittenTerritoryId, Guid businessWrittenYearId)
            {
                BusinessWrittenGroupId = businessWrittenGroupId;
                BusinessWrittenTerritoryId = businessWrittenTerritoryId;
                BusinessWrittenYearId = businessWrittenYearId;
            }

            public Guid BusinessWrittenGroupId { get; }
            public Guid BusinessWrittenTerritoryId { get; }
            public Guid BusinessWrittenYearId { get; }

            public bool Equals(BudgetCombinationKey other) =>
                BusinessWrittenGroupId.Equals(other.BusinessWrittenGroupId) &&
                BusinessWrittenTerritoryId.Equals(other.BusinessWrittenTerritoryId) &&
                BusinessWrittenYearId.Equals(other.BusinessWrittenYearId);

            public override bool Equals(object obj) => obj is BudgetCombinationKey other && Equals(other);

            public override int GetHashCode() =>
                BusinessWrittenGroupId.GetHashCode() ^
                BusinessWrittenTerritoryId.GetHashCode() ^
                BusinessWrittenYearId.GetHashCode();
        }
    }
}
