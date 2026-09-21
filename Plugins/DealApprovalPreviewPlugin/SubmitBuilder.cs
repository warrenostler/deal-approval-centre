using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Synchronous server-side Opportunity Deal Approval submission. Re-resolves the current
    /// financial snapshot via the shared FinancialSnapshotResolver (never trusts client-supplied
    /// values), enforces the coordinator-comment-when-below-forecast rule, and deep-inserts one
    /// fmi_dealapproval parent with one fmi_dealapprovalitem child per Opportunity Item in a
    /// single Create call. The relationship SchemaName used for the deep insert -
    /// fmi_dealapprovalitem_DealApproval_fmi_dealapproval - was confirmed from the FM TEST
    /// solution export's Other/Relationships/fmi_DealApproval.xml, not inferred or guessed.
    ///
    /// The caller (SubmitDealApproval) passes an elevated service - see
    /// IOrganizationServiceFactory.CreateOrganizationService(null) - not the InitiatingUserService
    /// used elsewhere in this project, mirroring the same pattern already established for
    /// DecisionBuilder. Every Dataverse call this class makes (financial resolution, approver
    /// routing, the deep insert, and the Opportunity update) uses that elevated service, so none
    /// of them depend on what privilege the actual human caller's own security role grants - this
    /// is what lets Submit keep working once ordinary coordinators' direct table privileges on
    /// fmi_dealapproval/fmi_dealapprovalitem are reduced or removed entirely. The actual human
    /// caller (initiatingUserId, from context.InitiatingUserId) is passed separately and is never
    /// conflated with the service identity: it is what populates fmi_requestedby, exactly as
    /// before.
    /// </summary>
    internal sealed class SubmitBuilder
    {
        private const string OpportunityLogicalName = "opportunity";
        private const string SystemUserLogicalName = "systemuser";
        private const string TransactionCurrencyLogicalName = "transactioncurrency";
        private const string DealApprovalLogicalName = "fmi_dealapproval";
        private const string DealApprovalItemLogicalName = "fmi_dealapprovalitem";

        /// <summary>
        /// The 1:N relationship SchemaName between fmi_dealapproval and fmi_dealapprovalitem,
        /// confirmed from the EntityRelationship Name attribute in the FM TEST solution export's
        /// Other/Relationships/fmi_DealApproval.xml (ReferencingEntityName=fmi_DealApprovalItem,
        /// ReferencedEntityName=fmi_DealApproval, ReferencingAttributeName=fmi_DealApproval) -
        /// not inferred from navigation-property casing.
        /// </summary>
        private const string DealApprovalItemsRelationshipSchemaName =
            "fmi_dealapprovalitem_DealApproval_fmi_dealapproval";

        private const int OpportunityApprovalStatusPending = 2;
        private const int DealApprovalStatusPending = 1;
        private const int SalesTypeTape = 797300000;
        private const int SalesTypeFormatSale = 797300001;
        private const int SalesTypeInflight = 797300006;
        private const int SalesTypeHomeEntertainment = 797300007;
        private const int SalesTypeAncillary = 797300008;

        /// <summary>
        /// Sales Types with a straight one-to-one environment-variable approver, no Target
        /// Territory or region logic involved (unlike Tape, which splits by EMEA/non-EMEA).
        /// </summary>
        private static readonly Dictionary<int, string> DirectApproverEnvironmentVariablesBySalesType =
            new Dictionary<int, string>
            {
                { SalesTypeFormatSale, "fmi_DealApproverFormat" },
                { SalesTypeInflight, "fmi_DealApproverInflight" },
                { SalesTypeHomeEntertainment, "fmi_DealApproverHomeEnt" },
                { SalesTypeAncillary, "fmi_DealApproverAncillary" },
            };

        /// <summary>
        /// Confirmed from fmi_DealApproval/Entity.xml's fmi_SubmittedDealItems attribute
        /// (MaxLength 20000) - the old JS's SUBMITTED_ITEMS_MAX_LENGTH constant of 10000 was
        /// stale and is not reused here. fmi_submitteddealitems is a legacy human-readable audit
        /// summary, never the source of truth for item data - every item's full structured data
        /// is always captured completely in the deep-inserted fmi_dealapprovalitem children
        /// regardless of this field. Exceeding this length used to hard-fail the whole Submit
        /// (found live: a 168-item Opportunity produced 37,972 characters against this 20,000
        /// limit, blocking submission entirely with nothing created); it is truncated instead -
        /// see TruncateSubmittedDealItemsText.
        /// </summary>
        private const int SubmittedDealItemsMaxLength = 20000;

        private readonly IOrganizationService _service;
        private readonly Guid _initiatingUserId;
        private readonly FinancialSnapshotResolver _resolver;
        private readonly ApproverResolver _approverResolver;

        public SubmitBuilder(IOrganizationService service, Guid initiatingUserId)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _initiatingUserId = initiatingUserId;
            _resolver = new FinancialSnapshotResolver(service);
            _approverResolver = new ApproverResolver(service);
        }

        public SubmitDealApprovalResult Submit(Guid opportunityId, string coordinatorComment)
        {
            if (opportunityId == Guid.Empty)
            {
                throw new InvalidPluginExecutionException("A valid Opportunity is required.");
            }

            var opportunity = RetrieveOpportunity(opportunityId);

            var currentStatus = opportunity.GetAttributeValue<OptionSetValue>("fmi_currentapprovalstatus")?.Value;

            if (currentStatus == OpportunityApprovalStatusPending)
            {
                throw new InvalidPluginExecutionException(
                    "This Opportunity already has an approval request pending.");
            }

            var previousApprovalRef = opportunity.GetAttributeValue<EntityReference>("fmi_currentdealapproval");
            var salesType = opportunity.GetAttributeValue<OptionSetValue>("fmi_salestype")?.Value;

            var resolvedItems = _resolver.Resolve(opportunityId);

            if (resolvedItems.Count == 0 && salesType != SalesTypeAncillary)
            {
                throw new InvalidPluginExecutionException(
                    "This Opportunity has no Opportunity Items, so there is nothing to submit for approval.");
            }

            var financials = resolvedItems.Select(i => i.Financials).ToList();

            if (!CoordinatorCommentRule.IsSatisfied(financials, coordinatorComment))
            {
                throw new InvalidPluginExecutionException(
                    "One or more submitted items are below the latest Forecast. A coordinator " +
                    "comment is required before this Opportunity can be submitted for approval.");
            }

            var approverId = ResolveApprover(resolvedItems, salesType);

            var approvalType = ResolveApprovalType(opportunity);
            var approvalVersion = ResolveApprovalVersion(previousApprovalRef);
            var baseCurrency = ResolveBaseCurrency();

            var submittedDealItemsText = BuildSubmittedDealItemsText(resolvedItems, baseCurrency.IsoCode);

            if (submittedDealItemsText.Length > SubmittedDealItemsMaxLength)
            {
                submittedDealItemsText = TruncateSubmittedDealItemsText(submittedDealItemsText, resolvedItems.Count);
            }

            var parent = BuildDealApprovalEntity(
                opportunityId,
                opportunity,
                approvalType,
                approvalVersion,
                previousApprovalRef,
                coordinatorComment,
                baseCurrency,
                submittedDealItemsText,
                approverId,
                salesType);

            var drafts = DealApprovalItemDraftBuilder.Build(resolvedItems.Select(ToResolvedOpportunityItem));

            var children = new EntityCollection
            {
                EntityName = DealApprovalItemLogicalName
            };

            foreach (var draft in drafts)
            {
                children.Entities.Add(BuildDealApprovalItemEntity(draft, baseCurrency));
            }

            parent.RelatedEntities.Add(new Relationship(DealApprovalItemsRelationshipSchemaName), children);

            var newApprovalId = _service.Create(parent);

            var approvalVersionUpdate = new Entity(DealApprovalLogicalName, newApprovalId);
            approvalVersionUpdate["fmi_approvalversion"] = approvalVersion;
            _service.Update(approvalVersionUpdate);

            var opportunityUpdate = new Entity(OpportunityLogicalName, opportunityId);
            opportunityUpdate["fmi_currentapprovalstatus"] = new OptionSetValue(OpportunityApprovalStatusPending);
            opportunityUpdate["fmi_currentdealapproval"] = new EntityReference(DealApprovalLogicalName, newApprovalId);

            _service.Update(opportunityUpdate);

            return new SubmitDealApprovalResult
            {
                DealApprovalId = newApprovalId,
                ItemCount = resolvedItems.Count
            };
        }

        // --- Approver routing ---------------------------------------------------------------

        /// <summary>
        /// Resolves the single systemuser this Opportunity's Deal Approval routes to. The
        /// decision itself (ApproverRouter.Resolve) is Dataverse-independent and unit-tested;
        /// this method only supplies the resolved primitives it needs from Dataverse.
        /// </summary>
        private Guid ResolveApprover(IReadOnlyList<ResolvedFinancialItem> resolvedItems, int? salesTypeValue)
        {
            if (salesTypeValue == SalesTypeTape)
            {
                var tapeApproverId = _approverResolver.ResolveTapeApprover(resolvedItems);
                if (!tapeApproverId.HasValue)
                {
                    throw new InvalidPluginExecutionException(
                        "Tape approvals are not configured correctly. Please ensure both the EMEA and non-EMEA Tape approver environment variables are populated with valid System User IDs.");
                }

                if (!_approverResolver.IsUserActive(tapeApproverId.Value))
                {
                    throw new InvalidPluginExecutionException(
                        "The resolved Tape approver is not active or cannot be found.");
                }

                return tapeApproverId.Value;
            }

            if (salesTypeValue.HasValue &&
                DirectApproverEnvironmentVariablesBySalesType.TryGetValue(salesTypeValue.Value, out var schemaName))
            {
                var directApproverId = _approverResolver.ResolveApproverBySchemaName(schemaName);
                if (!directApproverId.HasValue)
                {
                    throw new InvalidPluginExecutionException(
                        "This Sales Type's approver is not configured correctly. Please ensure the " +
                        schemaName + " environment variable is populated with a valid System User ID.");
                }

                if (!_approverResolver.IsUserActive(directApproverId.Value))
                {
                    throw new InvalidPluginExecutionException(
                        "The resolved approver for this Sales Type is not active or cannot be found.");
                }

                return directApproverId.Value;
            }

            var fallback = _approverResolver.ResolveFallbackApprover();

            var territoryApprovers = _approverResolver.ResolveTerritoryApprovers(
                resolvedItems.Select(i => i.TargetTerritoryRef.Id).Distinct());

            var territoryApproverIdsForItems = resolvedItems.Select(i =>
                territoryApprovers.TryGetValue(i.TargetTerritoryRef.Id, out var approverId) ? approverId : null);

            var routingResult = ApproverRouter.Resolve(
                fallback.Id,
                fallback.IsActive,
                territoryApproverIdsForItems,
                _approverResolver.IsUserActive);

            if (routingResult.IsConfigurationError)
            {
                throw new InvalidPluginExecutionException(routingResult.ConfigurationErrorMessage);
            }

            return routingResult.ApproverId;
        }

        // --- Retrieval ------------------------------------------------------------------

        private Entity RetrieveOpportunity(Guid opportunityId)
        {
            try
            {
                return _service.Retrieve(
                    OpportunityLogicalName,
                    opportunityId,
                    new ColumnSet(
                        "name",
                        "fmi_actualrevenue_base",
                        "parentaccountid",
                        "fmi_currentapprovalstatus",
                        "fmi_currentdealapproval",
                        "fmi_stage",
                        "stageid",
                        "traversedpath",
                        "fmi_salestype"));
            }
            catch (Exception)
            {
                throw new InvalidPluginExecutionException(
                    "The Opportunity could not be found, or you do not have permission to view it.");
            }
        }

        private int ResolveApprovalType(Entity opportunity)
        {
            var opportunityStage = opportunity.GetAttributeValue<OptionSetValue>("fmi_stage");
            if (opportunityStage != null)
            {
                return ApprovalTypeSelector.Determine(opportunityStage.Value);
            }

            var activeStageId = opportunity.GetAttributeValue<Guid?>("stageid");
            if (!activeStageId.HasValue || activeStageId.Value == Guid.Empty)
            {
                var traversedPath = opportunity.GetAttributeValue<string>("traversedpath");
                var stageIds = (traversedPath ?? string.Empty)
                    .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => Guid.TryParse(value.Trim(), out var parsed) ? parsed : Guid.Empty)
                    .Where(value => value != Guid.Empty)
                    .ToList();
                activeStageId = stageIds.LastOrDefault();
            }

            if (!activeStageId.HasValue || activeStageId.Value == Guid.Empty)
            {
                throw new InvalidPluginExecutionException(
                    "The active Opportunity BPF stage could not be determined.");
            }

            Entity activeStage;
            try
            {
                activeStage = _service.Retrieve(
                    "processstage",
                    activeStageId.Value,
                    new ColumnSet("stagename"));
            }
            catch (Exception)
            {
                throw new InvalidPluginExecutionException(
                    "The active Opportunity BPF stage could not be retrieved.");
            }

            return ApprovalTypeSelector.Determine(
                activeStage.GetAttributeValue<string>("stagename"));
        }

        private int ResolveApprovalVersion(EntityReference previousApprovalRef)
        {
            if (previousApprovalRef == null || previousApprovalRef.Id == Guid.Empty)
            {
                return 1;
            }

            var previousApproval = _service.Retrieve(
                DealApprovalLogicalName,
                previousApprovalRef.Id,
                new ColumnSet("fmi_approvalversion"));
            var previousVersion = previousApproval.GetAttributeValue<int?>("fmi_approvalversion") ?? 0;

            return Math.Max(1, previousVersion + 1);
        }

        private BaseCurrencyInfo ResolveBaseCurrency()
        {
            var organizations = _service.RetrieveMultiple(new QueryExpression("organization")
            {
                ColumnSet = new ColumnSet("basecurrencyid"),
                TopCount = 1
            }).Entities;

            var baseCurrencyRef = organizations.Count > 0
                ? organizations[0].GetAttributeValue<EntityReference>("basecurrencyid")
                : null;

            if (baseCurrencyRef == null)
            {
                throw new InvalidPluginExecutionException(
                    "The Dataverse organisation base currency could not be determined.");
            }

            var currency = _service.Retrieve(
                TransactionCurrencyLogicalName,
                baseCurrencyRef.Id,
                new ColumnSet("isocurrencycode"));

            return new BaseCurrencyInfo
            {
                Id = baseCurrencyRef.Id,
                IsoCode = currency.GetAttributeValue<string>("isocurrencycode") ?? "GBP"
            };
        }

        // --- Entity construction -----------------------------------------------------------

        private Entity BuildDealApprovalEntity(
            Guid opportunityId,
            Entity opportunity,
            int approvalType,
            int approvalVersion,
            EntityReference previousApprovalRef,
            string coordinatorComment,
            BaseCurrencyInfo baseCurrency,
            string submittedDealItemsText,
            Guid approverId,
            int? dealTypeValue)
        {
            var parent = new Entity(DealApprovalLogicalName);

            parent["fmi_name"] = BuildApprovalName(opportunity);
            parent["fmi_opportunity"] = new EntityReference(OpportunityLogicalName, opportunityId);
            parent["fmi_approvalstatus"] = new OptionSetValue(DealApprovalStatusPending);
            parent["fmi_approvaltype"] = new OptionSetValue(approvalType);
            if (dealTypeValue.HasValue)
            {
                parent["fmi_dealtype"] = new OptionSetValue(dealTypeValue.Value);
            }
            parent["fmi_approvalversion"] = approvalVersion;
            parent["fmi_requestedby"] = new EntityReference(SystemUserLogicalName, _initiatingUserId);
            parent["fmi_approver"] = new EntityReference(SystemUserLogicalName, approverId);
            parent["fmi_submittedcurrency"] = new EntityReference(TransactionCurrencyLogicalName, baseCurrency.Id);
            parent["transactioncurrencyid"] = new EntityReference(TransactionCurrencyLogicalName, baseCurrency.Id);
            parent["fmi_submitteddealitems"] = submittedDealItemsText;

            if (!string.IsNullOrWhiteSpace(coordinatorComment))
            {
                parent["fmi_requestorcomment"] = coordinatorComment.Trim();
            }

            if (previousApprovalRef != null)
            {
                parent["fmi_previousapproval"] = previousApprovalRef;
            }

            var actualRevenue = opportunity.GetAttributeValue<Money>("fmi_actualrevenue_base");

            if (actualRevenue != null)
            {
                parent["fmi_submitteddealvalue"] = actualRevenue;
            }

            var parentAccountRef = opportunity.GetAttributeValue<EntityReference>("parentaccountid");

            if (parentAccountRef != null)
            {
                parent["fmi_submittedcompany"] = parentAccountRef;
            }

            return parent;
        }

        private static string BuildApprovalName(Entity opportunity)
        {
            var opportunityName = opportunity.GetAttributeValue<string>("name") ?? "Opportunity";
            var timestamp = DateTime.UtcNow.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
            return $"Approval - {opportunityName} - {timestamp} UTC";
        }

        private Entity BuildDealApprovalItemEntity(DealApprovalItemDraft draft, BaseCurrencyInfo baseCurrency)
        {
            var child = new Entity(DealApprovalItemLogicalName);

            child["fmi_name"] = draft.Name;
            child["fmi_opportunityitem"] = new EntityReference("fmi_opportunityitem", draft.OpportunityItemId);
            child["fmi_content"] = new EntityReference("fmi_content", draft.ContentId);
            child["fmi_targetterritory"] = new EntityReference("fmi_targetterritory", draft.TargetTerritoryId);

            if (draft.BusinessWrittenGroupId.HasValue)
            {
                child["fmi_businesswrittengroup"] = new EntityReference("fmi_businesswrittengroup", draft.BusinessWrittenGroupId.Value);
            }

            if (draft.BusinessWrittenTerritoryId.HasValue)
            {
                child["fmi_businesswrittenterritory"] = new EntityReference("fmi_businesswrittenterritory", draft.BusinessWrittenTerritoryId.Value);
            }

            child["fmi_businesswrittenyear"] = new EntityReference("fmi_businesswrittenyear", draft.BusinessWrittenYearId);
            child["fmi_submittedsalevalue"] = new Money(draft.Financials.Sale);

            if (draft.Financials.Budget.HasValue)
            {
                child["fmi_submittedbudgetvalue"] = new Money(draft.Financials.Budget.Value);
            }

            if (draft.Financials.LatestForecast.HasValue)
            {
                child["fmi_submittedlatestforecast"] = new Money(draft.Financials.LatestForecast.Value);
            }

            var latestForecastTypeCode = LatestForecastTypeCode.Resolve(draft.Financials.LatestForecastType);

            if (latestForecastTypeCode.HasValue)
            {
                child["fmi_latestforecasttype"] = new OptionSetValue(latestForecastTypeCode.Value);
            }

            if (draft.Financials.VarianceToForecast.HasValue)
            {
                child["fmi_variancetoforecast"] = new Money(draft.Financials.VarianceToForecast.Value);
            }

            if (draft.Financials.VarianceToBudget.HasValue)
            {
                child["fmi_variancetobudget"] = new Money(draft.Financials.VarianceToBudget.Value);
            }

            child["fmi_belowforecast"] = draft.Financials.BelowForecast;
            child["transactioncurrencyid"] = new EntityReference(TransactionCurrencyLogicalName, baseCurrency.Id);

            return child;
        }

        private static ResolvedOpportunityItem ToResolvedOpportunityItem(ResolvedFinancialItem item)
        {
            return new ResolvedOpportunityItem
            {
                OpportunityItemId = item.OpportunityItemId,
                ContentId = item.ContentRef.Id,
                ContentName = item.ContentRef.Name ?? item.OpportunityItemName,
                TargetTerritoryId = item.TargetTerritoryRef.Id,
                TargetTerritoryName = item.TargetTerritoryRef.Name,
                BusinessWrittenGroupId = item.BusinessWrittenGroupRef?.Id,
                BusinessWrittenGroupName = item.BusinessWrittenGroupRef?.Name,
                BusinessWrittenTerritoryId = item.BusinessWrittenTerritoryRef?.Id,
                BusinessWrittenTerritoryName = item.BusinessWrittenTerritoryRef?.Name,
                BusinessWrittenYearId = item.BusinessWrittenYearRef.Id,
                BusinessWrittenYearName = item.BusinessWrittenYearRef.Name,
                Financials = item.Financials
            };
        }

        // --- fmi_submitteddealitems snapshot text ------------------------------------------

        private static string BuildSubmittedDealItemsText(
            IReadOnlyList<ResolvedFinancialItem> resolvedItems,
            string currencyCode)
        {
            var groups = resolvedItems
                .GroupBy(i => GroupKey(
                    i.BusinessWrittenGroupRef?.Id,
                    i.BusinessWrittenTerritoryRef?.Id,
                    i.BusinessWrittenYearRef.Id))
                .Select(g => g.ToList())
                .Select(groupItems => new SubmittedItemsGroup
                {
                    BusinessWrittenGroupName = groupItems[0].BusinessWrittenGroupRef?.Name ?? "(No Business Written Group)",
                    BusinessWrittenTerritoryName = groupItems[0].BusinessWrittenTerritoryRef?.Name ?? "(No Business Written Territory)",
                    BusinessWrittenYearName = groupItems[0].BusinessWrittenYearRef.Name,
                    Budget = groupItems[0].Financials.Budget,
                    Fc1 = groupItems[0].Financials.Fc1,
                    Fc2 = groupItems[0].Financials.Fc2,
                    Fc3 = groupItems[0].Financials.Fc3,
                    DealValue = groupItems.Sum(i => i.Financials.Sale),
                    Items = groupItems
                })
                .OrderBy(
                    g => $"{g.BusinessWrittenGroupName}|{g.BusinessWrittenTerritoryName}|{g.BusinessWrittenYearName}",
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sections = groups.Select(g =>
            {
                var lines = new List<string>
                {
                    $"{g.BusinessWrittenGroupName} - {g.BusinessWrittenTerritoryName} - {g.BusinessWrittenYearName}",
                    string.Empty,
                    $"Current Year Budget: {FormatMoney(g.Budget, currencyCode)}",
                    $"FC1: {FormatMoney(g.Fc1, currencyCode)}",
                    $"FC2: {FormatMoney(g.Fc2, currencyCode)}",
                    $"FC3: {FormatMoney(g.Fc3, currencyCode)}",
                    $"This Deal: {FormatMoney(g.DealValue, currencyCode)}",
                    string.Empty,
                    "Items:"
                };

                foreach (var item in g.Items)
                {
                    lines.Add(
                        $"- {item.ContentRef.Name ?? item.OpportunityItemName} - {item.TargetTerritoryRef.Name} - " +
                        FormatMoney(item.Financials.Sale, currencyCode));

                    var licenceLine = LicenceSnapshotFormatter.FormatLicenceLine(item.LicenceStart, item.LicenceEnd);

                    if (licenceLine != null)
                    {
                        lines.Add("  " + licenceLine);
                    }
                }

                return string.Join("\n", lines);
            });

            return string.Join("\n\n", sections);
        }

        /// <summary>
        /// Truncates the human-readable audit summary to fit fmi_submitteddealitems' column
        /// length rather than blocking Submit entirely - see the field's own remarks above.
        /// Truncating on a raw character count can land mid-line, so a trailing marker makes the
        /// cut point unambiguous to anyone reading the field directly in Dataverse.
        /// </summary>
        internal static string TruncateSubmittedDealItemsText(string text, int itemCount)
        {
            var suffix = $"\n\n... (truncated - {itemCount} items in full on the Deal Approval Items snapshot)";

            if (suffix.Length >= SubmittedDealItemsMaxLength)
            {
                return suffix.Substring(0, SubmittedDealItemsMaxLength);
            }

            var available = Math.Min(SubmittedDealItemsMaxLength - suffix.Length, text.Length);

            return text.Substring(0, available) + suffix;
        }

        private static string GroupKey(Guid? bwgId, Guid? bwtId, Guid bwyId) =>
            $"{bwgId?.ToString("D") ?? "none"}|{bwtId?.ToString("D") ?? "none"}|{bwyId:D}";

        private static string FormatMoney(decimal? value, string currencyCode) =>
            value.HasValue ? $"{currencyCode} {value.Value:N0}" : "-";

        private sealed class SubmittedItemsGroup
        {
            public string BusinessWrittenGroupName { get; set; }
            public string BusinessWrittenTerritoryName { get; set; }
            public string BusinessWrittenYearName { get; set; }
            public decimal? Budget { get; set; }
            public decimal? Fc1 { get; set; }
            public decimal? Fc2 { get; set; }
            public decimal? Fc3 { get; set; }
            public decimal DealValue { get; set; }
            public List<ResolvedFinancialItem> Items { get; set; }
        }

        private sealed class BaseCurrencyInfo
        {
            public Guid Id { get; set; }
            public string IsoCode { get; set; }
        }
    }

    /// <summary>
    /// Output shape for SubmitDealApproval: the newly created fmi_dealapproval and how many
    /// fmi_dealapprovalitem children were created for it.
    /// </summary>
    internal sealed class SubmitDealApprovalResult
    {
        public Guid DealApprovalId { get; set; }
        public int ItemCount { get; set; }
    }
}
