using DealApprovalPreviewPlugin.Core;

var failures = 0;
var total = 0;

void Check(string name, Action test)
{
    total++;
    try
    {
        test();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}");
        Console.WriteLine($"      {ex.Message}");
    }
}

void AssertEqual<T>(T expected, T actual, string context)
{
    if (!Equals(expected, actual))
    {
        throw new Exception($"{context}: expected <{expected}> but got <{actual}>");
    }
}

// --- SelectLatestForecast: fallback order and null-vs-zero -------------------------------

Check("FC3 populated -> FC3 selected", () =>
{
    var r = FinancialCalculator.SelectLatestForecast(1000m, 2000m, 3000m);
    AssertEqual(FinancialCalculator.Fc3Label, r.Type, "Type");
    AssertEqual(3000m, r.Value, "Value");
});

Check("FC3 null, FC2 populated -> FC2 selected", () =>
{
    var r = FinancialCalculator.SelectLatestForecast(1000m, 2000m, null);
    AssertEqual(FinancialCalculator.Fc2Label, r.Type, "Type");
    AssertEqual(2000m, r.Value, "Value");
});

Check("FC3/FC2 null, FC1 populated -> FC1 selected", () =>
{
    var r = FinancialCalculator.SelectLatestForecast(1000m, null, null);
    AssertEqual(FinancialCalculator.Fc1Label, r.Type, "Type");
    AssertEqual(1000m, r.Value, "Value");
});

Check("All null -> value 0 and blank type", () =>
{
    var r = FinancialCalculator.SelectLatestForecast(null, null, null);
    AssertEqual(string.Empty, r.Type, "Type");
    AssertEqual(0m, r.Value, "Value");
});

Check("FC3 explicitly 0 -> FC3 selected (zero is populated, not blank)", () =>
{
    var r = FinancialCalculator.SelectLatestForecast(500m, 700m, 0m);
    AssertEqual(FinancialCalculator.Fc3Label, r.Type, "Type");
    AssertEqual(0m, r.Value, "Value");
});

Check("FC2 explicitly 0 with FC3 null -> FC2 selected", () =>
{
    var r = FinancialCalculator.SelectLatestForecast(500m, 0m, null);
    AssertEqual(FinancialCalculator.Fc2Label, r.Type, "Type");
    AssertEqual(0m, r.Value, "Value");
});

// --- Calculate: variance and below-forecast ----------------------------------------------

Check("Variance calculations are Sale minus Forecast/Budget", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 5000m, Fc1 = 6000m, Fc2 = 7600m, Fc3 = 8999m };
    var result = FinancialCalculator.Calculate(163506.38m, budget);

    AssertEqual(8999m, result.LatestForecast, "LatestForecast");
    AssertEqual(FinancialCalculator.Fc3Label, result.LatestForecastType, "LatestForecastType");
    AssertEqual(154507.38m, result.VarianceToForecast, "VarianceToForecast");
    AssertEqual(158506.38m, result.VarianceToBudget, "VarianceToBudget");
});

Check("BelowForecast is true when Sale is under the resolved forecast", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 1000m, Fc1 = null, Fc2 = null, Fc3 = 500m };
    var result = FinancialCalculator.Calculate(100m, budget);

    AssertEqual(true, result.BelowForecast, "BelowForecast");
});

Check("BelowForecast is false when Sale meets or exceeds the resolved forecast", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 1000m, Fc1 = null, Fc2 = null, Fc3 = 500m };
    var result = FinancialCalculator.Calculate(500m, budget);

    AssertEqual(false, result.BelowForecast, "BelowForecast");
});

Check("BelowForecast is false when no FC field was ever populated", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 1000m, Fc1 = null, Fc2 = null, Fc3 = null };
    var result = FinancialCalculator.Calculate(0m, budget);

    AssertEqual(0m, result.LatestForecast, "LatestForecast");
    AssertEqual(string.Empty, result.LatestForecastType, "LatestForecastType");
    AssertEqual(false, result.BelowForecast, "BelowForecast");
});

// --- Independently-verified FM TEST acceptance rows (Opportunity 4941096c-cbf4-4b8f-adef-f314ca01fe00) ---
// These reproduce the calculation half of the acceptance matrix gathered by live read-only
// FetchXml queries during Phase 3 planning. They do not exercise Dataverse retrieval or the
// Content->BWG / Territory->BWT resolution - only the pure numeric rules under test here.

Check("Acceptance row: French Canada", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 5000.00m, Fc1 = 6000.00m, Fc2 = 7600.00m, Fc3 = 8999.00m };
    var result = FinancialCalculator.Calculate(163506.38m, budget);

    AssertEqual(FinancialCalculator.Fc3Label, result.LatestForecastType, "LatestForecastType");
    AssertEqual(8999.00m, result.LatestForecast, "LatestForecast");
    AssertEqual(154507.38m, result.VarianceToForecast, "VarianceToForecast");
    AssertEqual(158506.38m, result.VarianceToBudget, "VarianceToBudget");
    AssertEqual(false, result.BelowForecast, "BelowForecast");
});

Check("Acceptance row: Austria", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 55544.00m, Fc1 = 1555.00m, Fc2 = 23333.00m, Fc3 = 5555.00m };
    var result = FinancialCalculator.Calculate(160000.00m, budget);

    AssertEqual(FinancialCalculator.Fc3Label, result.LatestForecastType, "LatestForecastType");
    AssertEqual(5555.00m, result.LatestForecast, "LatestForecast");
    AssertEqual(154445.00m, result.VarianceToForecast, "VarianceToForecast");
    AssertEqual(104456.00m, result.VarianceToBudget, "VarianceToBudget");
    AssertEqual(false, result.BelowForecast, "BelowForecast");
});

Check("Acceptance row: German Speaking, Sale 1,520,000", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 55544.00m, Fc1 = 1555.00m, Fc2 = 23333.00m, Fc3 = 5555.00m };
    var result = FinancialCalculator.Calculate(1520000.00m, budget);

    AssertEqual(5555.00m, result.LatestForecast, "LatestForecast");
    AssertEqual(1514445.00m, result.VarianceToForecast, "VarianceToForecast");
    AssertEqual(1464456.00m, result.VarianceToBudget, "VarianceToBudget");
    AssertEqual(false, result.BelowForecast, "BelowForecast");
});

Check("Acceptance row: German Speaking, Sale 80,000", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 55544.00m, Fc1 = 1555.00m, Fc2 = 23333.00m, Fc3 = 5555.00m };
    var result = FinancialCalculator.Calculate(80000.00m, budget);

    AssertEqual(5555.00m, result.LatestForecast, "LatestForecast");
    AssertEqual(74445.00m, result.VarianceToForecast, "VarianceToForecast");
    AssertEqual(24456.00m, result.VarianceToBudget, "VarianceToBudget");
    AssertEqual(false, result.BelowForecast, "BelowForecast");
});

// --- CoordinatorCommentRule: server-side below-forecast comment enforcement ----------------

ItemFinancialResult MakeResult(bool belowForecast) => new ItemFinancialResult { BelowForecast = belowForecast };

Check("No items below forecast, blank comment -> accepted", () =>
{
    var items = new[] { MakeResult(false), MakeResult(false) };
    AssertEqual(true, CoordinatorCommentRule.IsSatisfied(items, string.Empty), "IsSatisfied");
});

Check("No items below forecast, null comment -> accepted", () =>
{
    var items = new[] { MakeResult(false) };
    AssertEqual(true, CoordinatorCommentRule.IsSatisfied(items, null), "IsSatisfied");
});

Check("One item below forecast, blank comment -> rejected", () =>
{
    var items = new[] { MakeResult(false), MakeResult(true) };
    AssertEqual(false, CoordinatorCommentRule.IsSatisfied(items, string.Empty), "IsSatisfied");
});

Check("One item below forecast, whitespace-only comment -> rejected", () =>
{
    var items = new[] { MakeResult(true) };
    AssertEqual(false, CoordinatorCommentRule.IsSatisfied(items, "   "), "IsSatisfied");
});

Check("One item below forecast, populated comment -> accepted", () =>
{
    var items = new[] { MakeResult(false), MakeResult(true) };
    AssertEqual(true, CoordinatorCommentRule.IsSatisfied(items, "Approved despite shortfall - see note"), "IsSatisfied");
});

// --- ApprovalTypeSelector: Opportunity stage derivation ------------------------------------

Check("Offer stage -> Initial Approval", () =>
{
    AssertEqual(ApprovalTypeSelector.InitialApproval, ApprovalTypeSelector.Determine("Offer"), "ApprovalType");
});

Check("Unauthorised stage -> Reapproval Pre-Authorisation", () =>
{
    AssertEqual(ApprovalTypeSelector.ReapprovalPreAuthorisation, ApprovalTypeSelector.Determine("Unauthorised"), "ApprovalType");
});

Check("Authorised stage -> Reapproval Post-Authorisation", () =>
{
    AssertEqual(ApprovalTypeSelector.ReapprovalPostAuthorisation, ApprovalTypeSelector.Determine("Authorised"), "ApprovalType");
});

// --- LatestForecastTypeCode: FC1/FC2/FC3/blank -> fmi_dealapprovalitem option set values ---

Check("FC1 label maps to option value 1", () =>
{
    AssertEqual(1, LatestForecastTypeCode.Resolve(FinancialCalculator.Fc1Label), "Code");
});

Check("FC2 label maps to option value 2", () =>
{
    AssertEqual(2, LatestForecastTypeCode.Resolve(FinancialCalculator.Fc2Label), "Code");
});

Check("FC3 label maps to option value 3", () =>
{
    AssertEqual(3, LatestForecastTypeCode.Resolve(FinancialCalculator.Fc3Label), "Code");
});

Check("Blank label (no FC populated) maps to no option value", () =>
{
    AssertEqual((int?)null, LatestForecastTypeCode.Resolve(string.Empty), "Code");
});

Check("Zero FC3 still resolves to FC3's option value (zero is populated, not blank)", () =>
{
    var forecast = FinancialCalculator.SelectLatestForecast(500m, 700m, 0m);
    AssertEqual(3, LatestForecastTypeCode.Resolve(forecast.Type), "Code");
});

// --- DealApprovalItemDraftBuilder: one draft per Opportunity Item, no aggregation ----------

ResolvedOpportunityItem MakeResolvedItem(int saleValue, Guid sharedBwgId, Guid sharedBwtId, Guid sharedBwyId) =>
    new ResolvedOpportunityItem
    {
        OpportunityItemId = Guid.NewGuid(),
        ContentId = Guid.NewGuid(),
        ContentName = $"Content {saleValue}",
        TargetTerritoryId = Guid.NewGuid(),
        TargetTerritoryName = $"Territory {saleValue}",
        BusinessWrittenGroupId = sharedBwgId,
        BusinessWrittenGroupName = "Shared BWG",
        BusinessWrittenTerritoryId = sharedBwtId,
        BusinessWrittenTerritoryName = "Shared BWT",
        BusinessWrittenYearId = sharedBwyId,
        BusinessWrittenYearName = "2026",
        Financials = new ItemFinancialResult
        {
            Sale = saleValue,
            Budget = 1000m,
            LatestForecast = 900m,
            LatestForecastType = FinancialCalculator.Fc3Label,
            VarianceToForecast = saleValue - 900m,
            VarianceToBudget = saleValue - 1000m,
            BelowForecast = saleValue < 900
        }
    };

Check("100 resolved items sharing one Goal combination produce 100 drafts, not aggregated", () =>
{
    var sharedBwg = Guid.NewGuid();
    var sharedBwt = Guid.NewGuid();
    var sharedBwy = Guid.NewGuid();

    var resolvedItems = Enumerable.Range(1, 100)
        .Select(i => MakeResolvedItem(i, sharedBwg, sharedBwt, sharedBwy))
        .ToList();

    var drafts = DealApprovalItemDraftBuilder.Build(resolvedItems);

    AssertEqual(100, drafts.Count, "Draft count");

    var distinctDraftItemIds = drafts.Select(d => d.OpportunityItemId).Distinct().Count();
    AssertEqual(100, distinctDraftItemIds, "Distinct OpportunityItemIds");

    var resolvedItemIds = resolvedItems.Select(r => r.OpportunityItemId).OrderBy(id => id).ToList();
    var actualDraftIds = drafts.Select(d => d.OpportunityItemId).OrderBy(id => id).ToList();

    for (var i = 0; i < resolvedItemIds.Count; i++)
    {
        AssertEqual(resolvedItemIds[i], actualDraftIds[i], $"OpportunityItemId[{i}]");
    }
});

Check("Draft name joins Content, Territory and Business Written Year", () =>
{
    var item = MakeResolvedItem(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    var draft = DealApprovalItemDraftBuilder.Build(new[] { item }).Single();

    AssertEqual("Content 1 - Territory 1 - 2026", draft.Name, "Name");
});

Check("Draft name skips missing parts rather than leaving blank segments", () =>
{
    var item = MakeResolvedItem(1, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    item.TargetTerritoryName = null;

    var draft = DealApprovalItemDraftBuilder.Build(new[] { item }).Single();

    AssertEqual("Content 1 - 2026", draft.Name, "Name");
});

Check("Draft preserves each item's own Financials rather than merging Goal-sharing items", () =>
{
    var sharedBwg = Guid.NewGuid();
    var sharedBwt = Guid.NewGuid();
    var sharedBwy = Guid.NewGuid();

    var itemA = MakeResolvedItem(500, sharedBwg, sharedBwt, sharedBwy);
    var itemB = MakeResolvedItem(1200, sharedBwg, sharedBwt, sharedBwy);

    var drafts = DealApprovalItemDraftBuilder.Build(new[] { itemA, itemB });

    var draftA = drafts.Single(d => d.OpportunityItemId == itemA.OpportunityItemId);
    var draftB = drafts.Single(d => d.OpportunityItemId == itemB.OpportunityItemId);

    AssertEqual(500m, draftA.Financials.Sale, "Item A Sale");
    AssertEqual(1200m, draftB.Financials.Sale, "Item B Sale");
    AssertEqual(true, draftA.Financials.BelowForecast, "Item A BelowForecast");
    AssertEqual(false, draftB.Financials.BelowForecast, "Item B BelowForecast");
});

// --- LicenceSnapshotFormatter: legacy fmi_submitteddealitems licence-date line ------------

Check("FormatDate with a value renders dd MMM yyyy", () =>
{
    AssertEqual("05 Mar 2026", LicenceSnapshotFormatter.FormatDate(new DateTime(2026, 3, 5)), "FormatDate");
});

Check("FormatDate with null renders the placeholder", () =>
{
    AssertEqual("-", LicenceSnapshotFormatter.FormatDate(null), "FormatDate");
});

Check("FormatLicenceLine with neither date present -> omitted (null)", () =>
{
    string? expectedNull = null;
    AssertEqual(expectedNull, LicenceSnapshotFormatter.FormatLicenceLine(null, null), "FormatLicenceLine");
});

Check("FormatLicenceLine with only a start date present", () =>
{
    var line = LicenceSnapshotFormatter.FormatLicenceLine(new DateTime(2026, 3, 5), null);
    AssertEqual("Licence: 05 Mar 2026 to -", line, "FormatLicenceLine");
});

Check("FormatLicenceLine with only an end date present", () =>
{
    var line = LicenceSnapshotFormatter.FormatLicenceLine(null, new DateTime(2027, 4, 12));
    AssertEqual("Licence: - to 12 Apr 2027", line, "FormatLicenceLine");
});

Check("FormatLicenceLine with both dates present", () =>
{
    var line = LicenceSnapshotFormatter.FormatLicenceLine(new DateTime(2026, 3, 5), new DateTime(2027, 4, 12));
    AssertEqual("Licence: 05 Mar 2026 to 12 Apr 2027", line, "FormatLicenceLine");
});

// --- ItemFinancialResult.Unavailable: missing financial configuration warns, never blocks ---

Check("Unavailable preserves Sale and sets nulls, not zeros, for comparison fields", () =>
{
    var result = ItemFinancialResult.Unavailable(1234.56m, "No Budget/Forecast record was found for Drama / Germany / BWY 2028.");

    AssertEqual(1234.56m, result.Sale, "Sale");
    AssertEqual((decimal?)null, result.Budget, "Budget");
    AssertEqual((decimal?)null, result.LatestForecast, "LatestForecast");
    AssertEqual((decimal?)null, result.VarianceToForecast, "VarianceToForecast");
    AssertEqual((decimal?)null, result.VarianceToBudget, "VarianceToBudget");
});

Check("Unavailable is never BelowForecast and marks the comparison unavailable", () =>
{
    var result = ItemFinancialResult.Unavailable(0m, "Content is not assigned to a Business Written Group.");

    AssertEqual(false, result.BelowForecast, "BelowForecast");
    AssertEqual(false, result.FinancialComparisonAvailable, "FinancialComparisonAvailable");
    AssertEqual("Content is not assigned to a Business Written Group.", result.FinancialWarning, "FinancialWarning");
});

Check("A normal Calculate() result has FinancialComparisonAvailable true and no warning", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 1000m, Fc1 = null, Fc2 = null, Fc3 = 500m };
    var result = FinancialCalculator.Calculate(600m, budget);

    AssertEqual(true, result.FinancialComparisonAvailable, "FinancialComparisonAvailable");
    string? expectedNullWarning = null;
    AssertEqual(expectedNullWarning, result.FinancialWarning, "FinancialWarning");
});

Check("Goal exists but all FC blank -> still FinancialComparisonAvailable (unchanged existing rule)", () =>
{
    var budget = new BudgetRow { CurrentYearBudget = 1000m, Fc1 = null, Fc2 = null, Fc3 = null };
    var result = FinancialCalculator.Calculate(0m, budget);

    AssertEqual((decimal?)0m, result.LatestForecast, "LatestForecast");
    AssertEqual(string.Empty, result.LatestForecastType, "LatestForecastType");
    AssertEqual(false, result.BelowForecast, "BelowForecast");
    AssertEqual(true, result.FinancialComparisonAvailable, "FinancialComparisonAvailable");
});

Check("A warning-only row (BelowForecast false) never makes the coordinator comment mandatory", () =>
{
    var items = new[]
    {
        ItemFinancialResult.Unavailable(500m, "No Budget/Forecast record was found for Drama / Germany / BWY 2028."),
        ItemFinancialResult.Unavailable(750m, "Content is not assigned to a Business Written Group.")
    };

    AssertEqual(false, CoordinatorCommentRule.IsRequired(items), "IsRequired");
    AssertEqual(true, CoordinatorCommentRule.IsSatisfied(items, string.Empty), "IsSatisfied");
});

Check("A genuine below-forecast row still requires comment even alongside warning-only rows", () =>
{
    var belowForecastBudget = new BudgetRow { CurrentYearBudget = 1000m, Fc1 = null, Fc2 = null, Fc3 = 500m };

    var items = new[]
    {
        ItemFinancialResult.Unavailable(500m, "Target Territory is not mapped to a Business Written Territory."),
        FinancialCalculator.Calculate(100m, belowForecastBudget)
    };

    AssertEqual(true, CoordinatorCommentRule.IsRequired(items), "IsRequired");
    AssertEqual(false, CoordinatorCommentRule.IsSatisfied(items, string.Empty), "IsSatisfied");
});

// --- ApproverRouter: Target Territory Default Approver routing ------------------------

var sarah = Guid.Parse("11111111-1111-1111-1111-111111111111");
var bob = Guid.Parse("22222222-2222-2222-2222-222222222222");
var darren = Guid.Parse("33333333-3333-3333-3333-333333333333");

Check("Sarah + Sarah + Sarah -> Sarah", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { sarah, sarah, sarah }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(sarah, result.ApproverId, "ApproverId");
});

Check("Sarah + Sarah + fallback -> fallback", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { sarah, sarah, darren }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("Sarah + Sarah + blank -> fallback", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { sarah, sarah, null }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("Sarah + Bob -> fallback", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { sarah, bob }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("blank + blank -> fallback", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { null, null }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("fallback + fallback -> fallback", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { darren, darren }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("single Sarah -> Sarah", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { sarah }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(sarah, result.ApproverId, "ApproverId");
});

Check("single blank -> fallback", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { (Guid?)null }, id => true);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("Invalid/missing fallback environment variable -> configuration error", () =>
{
    var result = ApproverRouter.Resolve(null, false, new Guid?[] { sarah, sarah }, id => true);
    AssertEqual(true, result.IsConfigurationError, "IsConfigurationError");
});

Check("Inactive unanimous specific approver -> fallback, not the inactive user", () =>
{
    var result = ApproverRouter.Resolve(darren, true, new Guid?[] { sarah, sarah, sarah }, id => id != sarah);
    AssertEqual(false, result.IsConfigurationError, "IsConfigurationError");
    AssertEqual(darren, result.ApproverId, "ApproverId");
});

Check("Inactive fallback approver -> configuration error even with a unanimous specific approver", () =>
{
    var result = ApproverRouter.Resolve(darren, false, new Guid?[] { sarah, sarah, sarah }, id => true);
    AssertEqual(true, result.IsConfigurationError, "IsConfigurationError");
});

// --- ProcessDealApprovalDecision: authorization, comment rule, state guard, outcome ------

var approverDarren = Guid.Parse("44444444-4444-4444-4444-444444444444");
var otherUser = Guid.Parse("55555555-5555-5555-5555-555555555555");
var ceoSuperApprover = Guid.Parse("66666666-6666-6666-6666-666666666666");

// 1. Assigned approver approves Pending record -> success
Check("Assigned approver approving is authorized", () =>
{
    var authorized = DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, approverDarren, false);
    AssertEqual(true, authorized, "IsAuthorized");
    AssertEqual(true, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Approve, null), "CommentSatisfied");
});

// 2. Assigned approver rejects with comment -> success
Check("Assigned approver rejecting with a comment is authorized and satisfied", () =>
{
    var authorized = DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, approverDarren, false);
    AssertEqual(true, authorized, "IsAuthorized");
    AssertEqual(true, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Reject, "Not enough margin"), "CommentSatisfied");
});

// 3. Assigned approver rejects without comment -> blocked
Check("Rejecting without a comment is never satisfied, regardless of blank/null/whitespace form", () =>
{
    AssertEqual(false, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Reject, string.Empty), "Empty");
    AssertEqual(false, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Reject, null), "Null");
    AssertEqual(false, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Reject, "   "), "Whitespace");
});

// 4. Assigned approver approves without comment -> success
Check("Approving without a comment is always satisfied", () =>
{
    AssertEqual(true, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Approve, string.Empty), "Empty");
    AssertEqual(true, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Approve, null), "Null");
    AssertEqual(true, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Approve, "   "), "Whitespace");
});

// 5 & 6. Non-assigned, non-super user approves/rejects -> blocked (decision-agnostic check)
Check("Non-assigned, non-Super Approver user is never authorized", () =>
{
    var authorized = DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, otherUser, false);
    AssertEqual(false, authorized, "IsAuthorized");
});

Check("Non-assigned user is not authorized even with no approver configured at all", () =>
{
    var authorized = DealApprovalDecisionAuthorization.IsAuthorized(null, otherUser, false);
    AssertEqual(false, authorized, "IsAuthorized");
});

// 7 & 8. Super Approver approves/rejects a record assigned to someone else -> success
Check("Super Approver is authorized on a record assigned to someone else", () =>
{
    var authorized = DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, ceoSuperApprover, true);
    AssertEqual(true, authorized, "IsAuthorized");
    AssertEqual(true, DecisionCommentRule.IsSatisfied(DealApprovalDecision.Reject, "CEO override - budget concerns"), "CommentSatisfied");
});

Check("Super Approver is authorized even when no approver is configured at all", () =>
{
    var authorized = DealApprovalDecisionAuthorization.IsAuthorized(null, ceoSuperApprover, true);
    AssertEqual(true, authorized, "IsAuthorized");
});

// 9. Decision By = Super Approver, Approver remains the original assigned user
Check("DecisionUpdateDraft records the actual caller as DecisionBy, not the assigned approver", () =>
{
    var fixedNow = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
    var draft = DecisionUpdateBuilder.Build(DealApprovalDecision.Approve, ceoSuperApprover, null, fixedNow);

    AssertEqual(ceoSuperApprover, draft.DecisionBy, "DecisionBy");
    // DecisionUpdateDraft has no Approver property at all - fmi_dealapproval.fmi_approver
    // (Darren, the originally assigned approver) is structurally impossible to overwrite from
    // this draft, not merely avoided by convention.
});

// 10, 11, 12. State protection - only a currently-Pending record may be decided
Check("Already Approved record cannot be decided again", () =>
{
    AssertEqual(false, DealApprovalStateGuard.CanDecide(DealApprovalStatus.Approved), "CanDecide");
});

Check("Already Rejected record cannot be decided again", () =>
{
    AssertEqual(false, DealApprovalStateGuard.CanDecide(DealApprovalStatus.Rejected), "CanDecide");
});

Check("Cancelled, Failed, or missing status are all non-Pending and blocked", () =>
{
    AssertEqual(false, DealApprovalStateGuard.CanDecide(DealApprovalStatus.Cancelled), "Cancelled");
    AssertEqual(false, DealApprovalStateGuard.CanDecide(DealApprovalStatus.Failed), "Failed");
    AssertEqual(false, DealApprovalStateGuard.CanDecide(null), "Null");
});

Check("Pending record can be decided", () =>
{
    AssertEqual(true, DealApprovalStateGuard.CanDecide(DealApprovalStatus.Pending), "CanDecide");
});

// 13. Successful Approve sets Decision On, Decision By and Approval Status
Check("Approve draft sets ApprovalStatus, DecisionOn and DecisionBy", () =>
{
    var fixedNow = new DateTime(2026, 8, 18, 9, 30, 0, DateTimeKind.Utc);
    var draft = DecisionUpdateBuilder.Build(DealApprovalDecision.Approve, approverDarren, null, fixedNow);

    AssertEqual(DealApprovalStatus.Approved, draft.ApprovalStatus, "ApprovalStatus");
    AssertEqual(fixedNow, draft.DecisionOn, "DecisionOn");
    AssertEqual(approverDarren, draft.DecisionBy, "DecisionBy");
    string expectedNullComment = null;
    AssertEqual(expectedNullComment, draft.DecisionComment, "DecisionComment");
});

// 14. Successful Reject sets Decision On, Decision By, Decision Comments and Approval Status
Check("Reject draft sets ApprovalStatus, DecisionOn, DecisionBy and trimmed DecisionComment", () =>
{
    var fixedNow = new DateTime(2026, 8, 18, 9, 45, 0, DateTimeKind.Utc);
    var draft = DecisionUpdateBuilder.Build(DealApprovalDecision.Reject, approverDarren, "  Not enough margin  ", fixedNow);

    AssertEqual(DealApprovalStatus.Rejected, draft.ApprovalStatus, "ApprovalStatus");
    AssertEqual(fixedNow, draft.DecisionOn, "DecisionOn");
    AssertEqual(approverDarren, draft.DecisionBy, "DecisionBy");
    AssertEqual("Not enough margin", draft.DecisionComment, "DecisionComment");
});

// 15. Opportunity becomes Approved on Approve
Check("Approve draft sets the Opportunity to Approved (3)", () =>
{
    var draft = DecisionUpdateBuilder.Build(DealApprovalDecision.Approve, approverDarren, null, DateTime.UtcNow);
    AssertEqual(3, draft.OpportunityApprovalStatus, "OpportunityApprovalStatus");
});

// 16. Opportunity becomes Rejected on Reject
Check("Reject draft sets the Opportunity to Rejected (4)", () =>
{
    var draft = DecisionUpdateBuilder.Build(DealApprovalDecision.Reject, approverDarren, "No", DateTime.UtcNow);
    AssertEqual(4, draft.OpportunityApprovalStatus, "OpportunityApprovalStatus");
});

// --- Authorization/routing/decision logic - unchanged by the intelligent rollback ---------
//
// The marker-authenticating guards (DealApprovalParentCreateGuard, DealApprovalItemCreateGuard,
// DealApprovalParentUpdateGuard) and everything that only existed to support them
// (ControlledOperationContext, ControlledOperationSigner, DealApprovalOperationMarker,
// DealApprovalFieldClassification, DealApprovalFieldUpdateDecision) have been deleted from the
// codebase entirely, along with their tests below - not left disabled "just in case". The final
// trust model no longer needs any plugin to authenticate which API a Create/Update originated
// from: ordinary users get no direct Create/Write privilege on fmi_dealapproval/
// fmi_dealapprovalitem at the security-role level, Submit and Decision both perform their
// controlled writes via an elevated service, and the two remaining guards
// (DealApprovalParentLockGuard, DealApprovalItemImmutabilityGuard) block Delete/Assign/
// SetState/SetStateDynamicEntity unconditionally - no marker needed for an always-block.
//
// Everything below this point already existed before the abandoned request-signing/
// ExecuteTransactionRequest redesign and is unchanged by the rollback - restored verbatim to
// confirm nothing was accidentally lost.

Check("Assigned approver is still authorized", () =>
{
    AssertEqual(true, DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, approverDarren, false), "IsAuthorized");
});

Check("Super Approver is still authorized", () =>
{
    AssertEqual(true, DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, ceoSuperApprover, true), "IsAuthorized");
});

Check("Unauthorized user is still blocked", () =>
{
    AssertEqual(false, DealApprovalDecisionAuthorization.IsAuthorized(approverDarren, otherUser, false), "IsAuthorized");
});

// Decision By is the real human caller (context.InitiatingUserId), never the elevated execution
// identity that actually performs the write - DecisionUpdateBuilder is pure data computation
// with no notion of "service" at all, so which service performs the resulting write cannot
// change what identity gets recorded. The same separation now applies to Submit's
// fmi_requestedby via SubmitBuilder's own _initiatingUserId field - not independently
// Core-testable (SubmitBuilder is Dataverse-facing, same testing boundary as every other
// builder class in this project), but structurally identical to this already-tested pattern and
// confirmed by code review: SubmitDealApproval passes an elevated service as the execution
// identity and context.InitiatingUserId separately as the attribution identity, exactly as
// ProcessDealApprovalDecision already did before this rollback.
Check("DecisionBy is the real caller even though the write itself runs on an elevated service", () =>
{
    var draft = DecisionUpdateBuilder.Build(DealApprovalDecision.Reject, ceoSuperApprover, "Budget concerns", DateTime.UtcNow);
    AssertEqual(ceoSuperApprover, draft.DecisionBy, "DecisionBy");
});

// --- DirectDecisionMapper: raw fmi_approvalstatus -> DealApprovalDecision, for a System
// Administrator's direct field edit in the model-driven app (DealApprovalDirectDecisionSync) ----

Check("Approved status maps back to Approve", () =>
{
    AssertEqual(DealApprovalDecision.Approve, DirectDecisionMapper.FromApprovalStatus(DealApprovalStatus.Approved), "Decision");
});

Check("Rejected status maps back to Reject", () =>
{
    AssertEqual(DealApprovalDecision.Reject, DirectDecisionMapper.FromApprovalStatus(DealApprovalStatus.Rejected), "Decision");
});

Check("Pending, Cancelled and Failed are not valid direct-edit decision targets", () =>
{
    AssertEqual(null, DirectDecisionMapper.FromApprovalStatus(DealApprovalStatus.Pending), "Pending");
    AssertEqual(null, DirectDecisionMapper.FromApprovalStatus(DealApprovalStatus.Cancelled), "Cancelled");
    AssertEqual(null, DirectDecisionMapper.FromApprovalStatus(DealApprovalStatus.Failed), "Failed");
});

Console.WriteLine();
Console.WriteLine($"{total - failures}/{total} passed");

return failures == 0 ? 0 : 1;
