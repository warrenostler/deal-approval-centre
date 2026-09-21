// Plugin-boundary test for ProcessDealApprovalDecision. Unlike DealApprovalPreviewPlugin.Tests
// (which exercises only the pure Core logic - DealApprovalDecisionAuthorization,
// DecisionCommentRule, DecisionUpdateBuilder, etc. - never the plugin's own
// ExecuteDataversePlugin method), this project constructs a real IServiceProvider and drives
// ProcessDealApprovalDecision.Execute(...) end-to-end, so a parameter-key mismatch between the
// live-registered Custom API and the plugin's InputParameters/OutputParameters reads is caught
// here rather than only discovered live in FM TEST.
//
// This targets net462 (matching the main plugin project, not the net10.0 Tests project) because
// it needs a real ProjectReference to DealApprovalPreviewPlugin.csproj plus
// Microsoft.CrmSdk.CoreAssemblies to construct genuine SDK types (ParameterCollection, Entity,
// IPluginExecutionContext, ...), which a net10.0 project referencing a net462 plugin assembly
// cannot reliably do.
//
// The IPluginExecutionContext/IOrganizationServiceFactory/ITracingService/ILogger member lists
// below were confirmed by reflecting on the actual restored
// microsoft.crmsdk.coreassemblies/9.0.2.60 Microsoft.Xrm.Sdk.dll, not assumed from memory.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using DealApprovalPreviewPlugin;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using Microsoft.Xrm.Sdk.Query;

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

void CheckThrows<TException>(string name, Action test, string expectedMessageContains) where TException : Exception
{
    total++;
    try
    {
        test();
        failures++;
        Console.WriteLine($"FAIL  {name}");
        Console.WriteLine($"      Expected {typeof(TException).Name} but no exception was thrown");
    }
    catch (TException ex) when (ex.Message.Contains(expectedMessageContains, StringComparison.Ordinal))
    {
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL  {name}");
        Console.WriteLine($"      Expected {typeof(TException).Name} containing \"{expectedMessageContains}\" but got {ex.GetType().Name}: {ex.Message}");
    }
}

void AssertEqual<T>(T expected, T actual, string context)
{
    if (!Equals(expected, actual))
    {
        throw new Exception($"{context}: expected <{expected}> but got <{actual}>");
    }
}

// --- Fixed scenario data ------------------------------------------------------------------

var approvalId = Guid.NewGuid();
var opportunityId = Guid.NewGuid();
var approverId = Guid.NewGuid();

(FakeOrganizationService service, FakeServiceProvider provider) BuildHarness(Guid callerId)
{
    var service = new FakeOrganizationService(approvalId, opportunityId, approverId);
    var context = new FakePluginExecutionContext { InitiatingUserId = callerId, UserId = callerId };
    var provider = new FakeServiceProvider(context, service);
    return (service, provider);
}

// --- Boundary tests -------------------------------------------------------------------------

Check("ProcessDealApprovalDecision reads fmi_-prefixed inputs and writes fmi_-prefixed outputs (Approve)", () =>
{
    var (service, provider) = BuildHarness(approverId);
    provider.Context.InputParameters["fmi_DealApprovalId"] = approvalId;
    provider.Context.InputParameters["fmi_Decision"] = 1; // Approve
    provider.Context.InputParameters["fmi_DecisionComment"] = null;

    new ProcessDealApprovalDecision(null!, null!).Execute(provider);

    if (!provider.Context.OutputParameters.Contains("fmi_DealApprovalId"))
    {
        throw new Exception("OutputParameters did not contain fmi_DealApprovalId.");
    }

    if (!provider.Context.OutputParameters.Contains("fmi_ApprovalStatus"))
    {
        throw new Exception("OutputParameters did not contain fmi_ApprovalStatus.");
    }

    AssertEqual(approvalId, (Guid)provider.Context.OutputParameters["fmi_DealApprovalId"], "fmi_DealApprovalId");
    AssertEqual(2, (int)provider.Context.OutputParameters["fmi_ApprovalStatus"], "fmi_ApprovalStatus (Approved)");
    AssertEqual(false, provider.Context.OutputParameters.Contains("DealApprovalId"), "legacy unprefixed DealApprovalId output must not be written");
    AssertEqual(false, provider.Context.OutputParameters.Contains("ApprovalStatus"), "legacy unprefixed ApprovalStatus output must not be written");
});

Check("ProcessDealApprovalDecision reads fmi_-prefixed inputs correctly for Reject with a comment", () =>
{
    var (service, provider) = BuildHarness(approverId);
    provider.Context.InputParameters["fmi_DealApprovalId"] = approvalId;
    provider.Context.InputParameters["fmi_Decision"] = 2; // Reject
    provider.Context.InputParameters["fmi_DecisionComment"] = "Not enough margin";

    new ProcessDealApprovalDecision(null!, null!).Execute(provider);

    AssertEqual(3, (int)provider.Context.OutputParameters["fmi_ApprovalStatus"], "fmi_ApprovalStatus (Rejected)");
});

CheckThrows<InvalidPluginExecutionException>(
    "ProcessDealApprovalDecision rejects legacy unprefixed DealApprovalId/Decision keys (regression guard)",
    () =>
    {
        var (service, provider) = BuildHarness(approverId);
        provider.Context.InputParameters["DealApprovalId"] = approvalId; // legacy unprefixed - must not be accepted
        provider.Context.InputParameters["Decision"] = 1;

        new ProcessDealApprovalDecision(null!, null!).Execute(provider);
    },
    "fmi_DealApprovalId is required");

CheckThrows<InvalidPluginExecutionException>(
    "ProcessDealApprovalDecision rejects a missing fmi_Decision even when fmi_DealApprovalId is present",
    () =>
    {
        var (service, provider) = BuildHarness(approverId);
        provider.Context.InputParameters["fmi_DealApprovalId"] = approvalId;

        new ProcessDealApprovalDecision(null!, null!).Execute(provider);
    },
    "fmi_Decision is required");

// --- GetPendingDealApprovals boundary tests ------------------------------------------------
//
// FakeQueueOrganizationService fakes two Pending Deal Approvals assigned to two different
// approvers, plus a configurable Deal SuperApprover membership check, so both scoping branches
// (assigned-approver-only vs. every Pending record) run through the real
// PendingDealApprovalsBuilder query/JSON-assembly path rather than being asserted on Core logic
// alone - the same "exercise the real plugin boundary" rationale as the Decision tests above.

var approvalOneId = Guid.NewGuid();
var approvalTwoId = Guid.NewGuid();
var approverAId = Guid.NewGuid();
var approverBId = Guid.NewGuid();
var superApproverUserId = Guid.NewGuid();

Check("Read-only team membership grants all-queue visibility without SuperApprover rights", () =>
{
    var readerId = Guid.NewGuid();
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { ReadOnlyUserId = readerId };
    var provider = new FakeServiceProvider(new FakePluginExecutionContext { InitiatingUserId = readerId, UserId = superApproverUserId }, service);
    new GetPendingDealApprovals(null!, null!).Execute(provider);
    var parsed = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject((string)provider.Context.OutputParameters["fmi_ApprovalsJson"]);
    AssertEqual(2, (int)parsed["count"], "all pending records visible");
    AssertEqual(readerId.ToString("D"), (string)parsed["callerId"], "identity is initiating human, not execution user");
    AssertEqual(true, (bool)parsed["isReadOnlyTeamMember"], "read-only membership");
    AssertEqual(false, (bool)parsed["isSuperApprover"], "no approval rights granted");
});

Check("Configured read-only team does not broaden a nonmember's queue", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { ReadOnlyUserId = Guid.NewGuid() };
    var provider = new FakeServiceProvider(new FakePluginExecutionContext { InitiatingUserId = approverAId, UserId = approverAId }, service);
    new GetPendingDealApprovals(null!, null!).Execute(provider);
    var parsed = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject((string)provider.Context.OutputParameters["fmi_ApprovalsJson"]);
    AssertEqual(1, (int)parsed["count"], "only assigned approval visible");
    AssertEqual(false, (bool)parsed["isReadOnlyTeamMember"], "not a member");
});
Check("Read-only users cannot submit or cancel through the Custom APIs", () =>
{
    var readerId = Guid.NewGuid();
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { ReadOnlyUserId = readerId };
    foreach (IPlugin plugin in new IPlugin[] { new SubmitDealApproval(null!, null!), new CancelDealApprovalRequest(null!, null!) })
    {
        var provider = new FakeServiceProvider(new FakePluginExecutionContext { InitiatingUserId = readerId, UserId = superApproverUserId }, service);
        provider.Context.InputParameters["OpportunityId"] = Guid.NewGuid();
        try { plugin.Execute(provider); throw new Exception("Expected read-only denial"); }
        catch (InvalidPluginExecutionException ex) when (ex.Message.Contains("read-only")) { }
    }
});

Check("Ordinary approver membership wins over read-only membership without granting SuperApprover access", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { ReadOnlyUserId = approverAId, OrdinaryApproverUserId = approverAId };
    AssertEqual(true, new ReadOnlyTeamResolver(service).CanSubmit(approverAId), "higher mode retains submission rights");
    var provider = new FakeServiceProvider(new FakePluginExecutionContext { InitiatingUserId = approverAId, UserId = approverAId }, service);
    new GetPendingDealApprovals(null!, null!).Execute(provider);
    var parsed = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject((string)provider.Context.OutputParameters["fmi_ApprovalsJson"]);
    AssertEqual(true, (bool)parsed["isApproverTeamMember"], "ordinary approver membership");
    AssertEqual(true, (bool)parsed["isReadOnlyTeamMember"], "read-only membership retained");
    AssertEqual(false, (bool)parsed["isSuperApprover"], "not elevated to SuperApprover");
    AssertEqual(true, (bool)parsed["canSubmit"], "can submit");
    AssertEqual(2, (int)parsed["count"], "read-only membership still grants broader viewing");
});

Check("SuperApprover membership wins over read-only membership", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { ReadOnlyUserId = superApproverUserId };
    AssertEqual(true, new ReadOnlyTeamResolver(service).CanSubmit(superApproverUserId), "SuperApprover retains write mode");
});

Check("Ordinary approver team alone does not broaden the assigned queue", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { OrdinaryApproverUserId = approverAId };
    var provider = new FakeServiceProvider(new FakePluginExecutionContext { InitiatingUserId = approverAId, UserId = approverAId }, service);
    new GetPendingDealApprovals(null!, null!).Execute(provider);
    var parsed = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject((string)provider.Context.OutputParameters["fmi_ApprovalsJson"]);
    AssertEqual(1, (int)parsed["count"], "ordinary approver sees assigned only");
});

Check("A viewer cannot decide another user's approval", () =>
{
    var readerId = Guid.NewGuid();
    var (service, provider) = BuildHarness(readerId);
    provider.Context.InputParameters["fmi_DealApprovalId"] = approvalId;
    provider.Context.InputParameters["fmi_Decision"] = 1;
    try { new ProcessDealApprovalDecision(null!, null!).Execute(provider); throw new Exception("Expected authorization denial"); }
    catch (InvalidPluginExecutionException ex) when (ex.Message.Contains("not authorised")) { }
    AssertEqual(0, service.Updates.Count, "no writes");
});
Check("GetPendingDealApprovals: a normal approver sees only their own assigned Pending approval", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId);
    var context = new FakePluginExecutionContext { InitiatingUserId = approverAId, UserId = approverAId };
    var provider = new FakeServiceProvider(context, service);

    new GetPendingDealApprovals(null!, null!).Execute(provider);

    AssertEqual(true, provider.Context.OutputParameters.Contains("fmi_ApprovalsJson"), "OutputParameters must contain fmi_ApprovalsJson");
    AssertEqual(false, provider.Context.OutputParameters.Contains("ApprovalsJson"), "legacy unprefixed ApprovalsJson output must not be written");

    var json = (string)provider.Context.OutputParameters["fmi_ApprovalsJson"];
    var serializer = new JavaScriptSerializer();
    var parsed = (Dictionary<string, object>)serializer.DeserializeObject(json);
    var approvals = (object[])parsed["approvals"];

    AssertEqual(1, (int)parsed["count"], "count");
    AssertEqual(1, approvals.Length, "approvals.Count");

    var only = (Dictionary<string, object>)approvals[0]!;
    AssertEqual(approvalOneId.ToString(), (string)only["dealApprovalId"], "dealApprovalId");
    AssertEqual("Opp One", (string)only["opportunityName"], "opportunityName");
    AssertEqual("Company One", (string)only["companyName"], "companyName");
    AssertEqual(2, (int)only["itemCount"], "itemCount");
    AssertEqual(1, (int)only["belowForecastCount"], "belowForecastCount");
});

Check("GetPendingDealApprovals: a Deal SuperApprover sees every Pending approval, not just their own", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId);
    var context = new FakePluginExecutionContext { InitiatingUserId = superApproverUserId, UserId = superApproverUserId };
    var provider = new FakeServiceProvider(context, service);

    new GetPendingDealApprovals(null!, null!).Execute(provider);

    var json = (string)provider.Context.OutputParameters["fmi_ApprovalsJson"];
    var serializer = new JavaScriptSerializer();
    var parsed = (Dictionary<string, object>)serializer.DeserializeObject(json);
    var approvals = (object[])parsed["approvals"];

    AssertEqual(2, (int)parsed["count"], "count");
    AssertEqual(2, approvals.Length, "approvals.Count");
});

Check("GetPendingDealApprovals: a user who is neither the assigned approver nor a Super Approver sees nothing", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId);
    var context = new FakePluginExecutionContext { InitiatingUserId = Guid.NewGuid(), UserId = Guid.NewGuid() };
    var provider = new FakeServiceProvider(context, service);

    new GetPendingDealApprovals(null!, null!).Execute(provider);

    var json = (string)provider.Context.OutputParameters["fmi_ApprovalsJson"];
    var serializer = new JavaScriptSerializer();
    var parsed = (Dictionary<string, object>)serializer.DeserializeObject(json);

    AssertEqual(0, (int)parsed["count"], "count");
});

Check("SuperApproverResolver's Team FetchXml has no statecode condition (regression guard) and uses configured team ID, owner type and caller membership", () =>
{
    // The live Dataverse Team entity in FM TEST has no statecode attribute at all - a
    // statecode condition here previously failed the whole query with "'Team' entity
    // doesn't contain attribute with Name = 'statecode'", discovered live against Deal
    // Approval f25ec6e7-4f9b-f111-b8dc-6045bdd2001c. This asserts the actual FetchXml
    // SuperApproverResolver issues (via GetPendingDealApprovals -> PendingDealApprovalsBuilder)
    // never reintroduces it, while still constraining on configured team ID, Owner team type, and the
    // teammembership join.
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId);
    var context = new FakePluginExecutionContext { InitiatingUserId = superApproverUserId, UserId = superApproverUserId };
    var provider = new FakeServiceProvider(context, service);

    new GetPendingDealApprovals(null!, null!).Execute(provider);

    var fetchXml = service.LastTeamFetchXml;

    if (fetchXml == null)
    {
        throw new Exception("SuperApproverResolver did not issue a FetchExpression team query.");
    }

    AssertEqual(false, fetchXml.Contains("statecode", StringComparison.OrdinalIgnoreCase), "must not filter on statecode");
    AssertEqual(true, fetchXml.Contains("name='team'", StringComparison.OrdinalIgnoreCase), "must query the team entity");
    AssertEqual(false, fetchXml.Contains("Deal SuperApprover", StringComparison.Ordinal), "must not filter by hard-coded team name");
    AssertEqual(true, fetchXml.Contains(service.SuperTeamId.ToString("D")), "must use configured team ID");
    AssertEqual(true, fetchXml.Contains("attribute='teamtype'", StringComparison.OrdinalIgnoreCase), "must filter to Owner teams");
    AssertEqual(true, fetchXml.Contains("teammembership", StringComparison.OrdinalIgnoreCase), "must join teammembership");
    AssertEqual(true, fetchXml.Contains("attribute='systemuserid'", StringComparison.OrdinalIgnoreCase), "must filter membership by the caller");
});

Check("SuperApprover current value overrides default and changing the team removes old-team access", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { HasSuperCurrentValue = true };
    service.SuperCurrentValue = service.SuperTeamId.ToString("D");
    AssertEqual(true, new SuperApproverResolver(service).IsSuperApprover(superApproverUserId), "configured team member");
    var replacementTeam = Guid.NewGuid();
    service.SuperCurrentValue = replacementTeam.ToString("D");
    AssertEqual(false, new SuperApproverResolver(service).IsSuperApprover(superApproverUserId), "old team no longer grants access");
    AssertEqual(true, service.LastTeamFetchXml!.Contains(replacementTeam.ToString("D")), "new configured team used immediately");
});

Check("Missing SuperApprover configuration does not fall back to the old team name", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { SuperConfigurationExists = false };
    AssertEqual(false, new SuperApproverResolver(service).IsSuperApprover(superApproverUserId), "no configuration means no elevated access");
    AssertEqual<string?>(null, service.LastTeamFetchXml, "no legacy lookup");
});

Check("Blank SuperApprover current value disables access even when a default exists", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { HasSuperCurrentValue = true, SuperCurrentValue = " " };
    AssertEqual(false, new SuperApproverResolver(service).IsSuperApprover(superApproverUserId), "blank override disables team");
});

CheckThrows<InvalidPluginExecutionException>("Invalid SuperApprover GUID is a configuration error", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { HasSuperCurrentValue = true, SuperCurrentValue = "invalid-guid" };
    new SuperApproverResolver(service).IsSuperApprover(superApproverUserId);
}, "valid team GUID");

CheckThrows<InvalidPluginExecutionException>("Duplicate SuperApprover current values are rejected", () =>
{
    var service = new FakeQueueOrganizationService(approvalOneId, approvalTwoId, approverAId, approverBId, superApproverUserId) { HasSuperCurrentValue = true, DuplicateSuperCurrentValue = true };
    service.SuperCurrentValue = service.SuperTeamId.ToString("D");
    new SuperApproverResolver(service).IsSuperApprover(superApproverUserId);
}, "multiple current values");
// --- FinancialSnapshotResolver: large-Opportunity condition-count regression guard --------
//
// Found live: an Opportunity with 168 Opportunity Items across mostly-distinct Content/Target
// Territory/Business Written Year combinations made ResolveBudgets build a Goal FetchXml with
// 3 conditions PER distinct combination, OR'd together - well past Dataverse's per-query
// condition limit, failing with "Number of conditions in query exceeded maximum limit"
// (CorrelationId 032c3be0-f8c2-401d-93bb-1dffdb237cd1). The fix bounds the Goal query to 3 IN
// conditions (distinct BWG/BWT/BWY ids) regardless of item count, matching the same IN-based
// pattern the Content->BWG and Territory->BWT lookups already used above it in this file. This
// test drives FinancialSnapshotResolver directly (an internal class - see AssemblyInfo.cs'
// InternalsVisibleTo) with 168 items that each have a fully distinct BWG+BWT+BWY combination,
// the exact shape that broke, and asserts both that the Goal query stays bounded to 3 conditions
// and that resolution is still correct despite the query now returning a superset of Goal rows.

Check("FinancialSnapshotResolver: 168 items with fully distinct BWG+BWT+BWY combinations resolve without exceeding a bounded Goal query", () =>
{
    const int itemCount = 168;
    var service = new FakeFinancialSnapshotOrganizationService(itemCount);
    var resolver = new FinancialSnapshotResolver(service);

    var resolved = resolver.Resolve(Guid.NewGuid());

    AssertEqual(itemCount, resolved.Count, "resolved item count");

    if (service.LastGoalFetchXml == null)
    {
        throw new Exception("FinancialSnapshotResolver did not issue a Goal FetchExpression query.");
    }

    var conditionCount = System.Text.RegularExpressions.Regex.Matches(service.LastGoalFetchXml, "<condition ").Count;
    AssertEqual(3, conditionCount, "Goal query condition count (must stay bounded regardless of item count)");
    var andBlockCount = System.Text.RegularExpressions.Regex.Matches(service.LastGoalFetchXml, "type='and'").Count;
    AssertEqual(1, andBlockCount, "must be a single wrapping AND block, not one per combination");

    var samples = new[] { 0, 50, 167 };

    foreach (var i in samples)
    {
        var item = resolved[i];
        AssertEqual(true, item.Financials.FinancialComparisonAvailable, $"item {i} FinancialComparisonAvailable");
        AssertEqual(900m, item.Financials.LatestForecast, $"item {i} LatestForecast (from Fc3)");
        AssertEqual(1000m + i - 900m, item.Financials.VarianceToForecast, $"item {i} VarianceToForecast");
    }
});

// --- SubmitBuilder: fmi_submitteddealitems truncation regression guard --------------------
//
// Found live: a 168-item Opportunity's audit summary text was 37,972 characters against the
// fmi_submitteddealitems column's 20,000 character limit. This used to be a hard failure - the
// whole Submit (parent + all deep-inserted children) rolled back, so nothing was created, with
// no obvious on-screen error. Fixed by truncating this legacy audit-only field instead of
// blocking Submit - the real item data always lives in the fmi_dealapprovalitem children
// regardless of this field. SubmittedDealItemsMaxLength itself stays private on SubmitBuilder;
// 20000 is asserted here as the independently-known Dataverse column length, not read from the
// class under test.

const int submittedDealItemsMaxLength = 20000;

Check("SubmitBuilder.TruncateSubmittedDealItemsText: reproduces the live 168-item overflow and stays within the column limit", () =>
{
    var oversized = new string('x', 37972);

    var truncated = SubmitBuilder.TruncateSubmittedDealItemsText(oversized, 168);

    AssertEqual(true, truncated.Length <= submittedDealItemsMaxLength, "truncated text must fit the column");
    AssertEqual(true, truncated.StartsWith(new string('x', 100), StringComparison.Ordinal), "must preserve the original text's start, not just the marker");
    AssertEqual(true, truncated.Contains("168 items"), "must state the real item count in the truncation marker");
    AssertEqual(true, truncated.Contains("truncated"), "must clearly mark the text as truncated");
});

Check("SubmitBuilder.TruncateSubmittedDealItemsText: never exceeds the column limit even for a pathologically short input", () =>
{
    var truncated = SubmitBuilder.TruncateSubmittedDealItemsText("short", 1);

    AssertEqual(true, truncated.Length <= submittedDealItemsMaxLength, "must never exceed the column limit");
});

Console.WriteLine();
Console.WriteLine($"{total - failures}/{total} passed");

if (failures > 0)
{
    Environment.Exit(1);
}

// --- Fakes ------------------------------------------------------------------------------

sealed class FakeServiceProvider : IServiceProvider
{
    public FakePluginExecutionContext Context { get; }
    private readonly FakeOrganizationServiceFactory _factory;
    private readonly FakeTracingService _tracingService = new();
    private readonly FakeLogger _logger = new();

    public FakeServiceProvider(FakePluginExecutionContext context, IOrganizationService service)
    {
        Context = context;
        _factory = new FakeOrganizationServiceFactory(service);
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(IPluginExecutionContext) || serviceType == typeof(IExecutionContext))
        {
            return Context;
        }

        if (serviceType == typeof(IOrganizationServiceFactory))
        {
            return _factory;
        }

        if (serviceType == typeof(ITracingService))
        {
            return _tracingService;
        }

        if (serviceType == typeof(ILogger))
        {
            return _logger;
        }

        if (serviceType == typeof(IServiceEndpointNotificationService))
        {
            return null;
        }

        throw new NotSupportedException($"FakeServiceProvider was asked for unsupported service type {serviceType}.");
    }
}

sealed class FakePluginExecutionContext : IPluginExecutionContext
{
    public ParameterCollection InputParameters { get; } = new();
    public ParameterCollection OutputParameters { get; } = new();
    public ParameterCollection SharedVariables { get; } = new();
    public Guid UserId { get; set; }
    public Guid InitiatingUserId { get; set; }
    public Guid BusinessUnitId { get; set; }
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = "FakeOrg";
    public Guid PrimaryEntityId { get; set; }
    public string PrimaryEntityName { get; set; } = string.Empty;
    public EntityImageCollection PreEntityImages { get; } = new();
    public EntityImageCollection PostEntityImages { get; } = new();
    public EntityReference? OwningExtension { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public bool IsExecutingOffline { get; set; }
    public bool IsOfflinePlayback { get; set; }
    public bool IsInTransaction { get; set; }
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public DateTime OperationCreatedOn { get; set; } = DateTime.UtcNow;
    public int Mode { get; set; }
    public int IsolationMode { get; set; } = 1;
    public int Depth { get; set; } = 1;
    public string MessageName { get; set; } = "fmi_ProcessDealApprovalDecision";
    public Guid? RequestId { get; set; }
    public string SecondaryEntityName { get; set; } = string.Empty;
    public int Stage { get; set; } = 20;
    public IPluginExecutionContext? ParentContext { get; set; }
}

sealed class FakeOrganizationServiceFactory : IOrganizationServiceFactory
{
    private readonly IOrganizationService _service;

    public FakeOrganizationServiceFactory(IOrganizationService service)
    {
        _service = service;
    }

    public IOrganizationService CreateOrganizationService(Guid? userId) => _service;
}

sealed class FakeTracingService : ITracingService
{
    public void Trace(string format, params object[] args)
    {
        // No-op: this test harness does not assert on trace output.
    }
}

sealed class FakeLogger : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) => null;
    public IDisposable? BeginScope(string format, params object[] args) => null;
    public bool IsEnabled(LogLevel logLevel) => false;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
    public void Log(LogLevel logLevel, EventId eventId, Exception exception, string message, params object[] args) { }
    public void Log(LogLevel logLevel, EventId eventId, string message, params object[] args) { }
    public void Log(LogLevel logLevel, Exception exception, string message, params object[] args) { }
    public void Log(LogLevel logLevel, string message, params object[] args) { }
    public void LogCritical(EventId eventId, Exception exception, string message, params object[] args) { }
    public void LogCritical(EventId eventId, string message, params object[] args) { }
    public void LogCritical(Exception exception, string message, params object[] args) { }
    public void LogCritical(string message, params object[] args) { }
    public void LogDebug(EventId eventId, Exception exception, string message, params object[] args) { }
    public void LogDebug(EventId eventId, string message, params object[] args) { }
    public void LogDebug(Exception exception, string message, params object[] args) { }
    public void LogDebug(string message, params object[] args) { }
    public void LogError(EventId eventId, Exception exception, string message, params object[] args) { }
    public void LogError(EventId eventId, string message, params object[] args) { }
    public void LogError(Exception exception, string message, params object[] args) { }
    public void LogError(string message, params object[] args) { }
    public void LogInformation(EventId eventId, Exception exception, string message, params object[] args) { }
    public void LogInformation(EventId eventId, string message, params object[] args) { }
    public void LogInformation(Exception exception, string message, params object[] args) { }
    public void LogInformation(string message, params object[] args) { }
    public void LogTrace(EventId eventId, Exception exception, string message, params object[] args) { }
    public void LogTrace(EventId eventId, string message, params object[] args) { }
    public void LogTrace(Exception exception, string message, params object[] args) { }
    public void LogTrace(string message, params object[] args) { }
    public void LogWarning(EventId eventId, Exception exception, string message, params object[] args) { }
    public void LogWarning(EventId eventId, string message, params object[] args) { }
    public void LogWarning(Exception exception, string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) { }
    public void LogMetric(string name, long value) { }
    public void LogMetric(string name, IDictionary<string, string> dimensions, long value) { }
    public void AddCustomProperty(string name, string value) { }
    public void Execute(string name, Action action, IEnumerable<KeyValuePair<string, string>> properties) => action();
    public Task ExecuteAsync(string name, Func<Task> action, IEnumerable<KeyValuePair<string, string>> properties) => action();
}

/// <summary>
/// Fakes exactly the Dataverse calls DecisionBuilder/SuperApproverResolver make for a single
/// Pending Deal Approval whose fmi_approver is the fixed approverId, so the full plugin pipeline
/// (authorization, state guard, comment rule, both Updates) runs for real rather than being
/// stubbed out - the only thing not exercised against a live org is the network call itself.
/// </summary>
sealed class FakeOrganizationService : IOrganizationService
{
    private readonly Guid _approvalId;
    private readonly Guid _opportunityId;
    private readonly Guid _approverId;

    public List<Entity> Updates { get; } = new();

    public FakeOrganizationService(Guid approvalId, Guid opportunityId, Guid approverId)
    {
        _approvalId = approvalId;
        _opportunityId = opportunityId;
        _approverId = approverId;
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet)
    {
        if (entityName != "fmi_dealapproval" || id != _approvalId)
        {
            throw new InvalidOperationException($"Unexpected Retrieve({entityName}, {id}).");
        }

        var entity = new Entity(entityName, id);
        entity["fmi_approvalstatus"] = new OptionSetValue(1); // Pending
        entity["fmi_approver"] = new EntityReference("systemuser", _approverId);
        entity["fmi_opportunity"] = new EntityReference("opportunity", _opportunityId);
        return entity;
    }

    public void Update(Entity entity) => Updates.Add(entity);

    public EntityCollection RetrieveMultiple(QueryBase query) => new(); // no Super Approver team membership rows

    public Guid Create(Entity entity) => throw new NotSupportedException("Create is not exercised by ProcessDealApprovalDecision.");
    public void Delete(string entityName, Guid id) => throw new NotSupportedException("Delete is not exercised by ProcessDealApprovalDecision.");
    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException("Execute is not exercised by ProcessDealApprovalDecision.");
    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
}

/// <summary>
/// Fakes two Pending Deal Approvals (assigned to two different approvers) plus a configurable
/// Deal SuperApprover team-membership check, so GetPendingDealApprovals/PendingDealApprovalsBuilder
/// run their real QueryExpression-with-linked-entities and FetchXml-membership-check paths rather
/// than being stubbed out. SuperApproverResolver's membership check is answered by pattern-matching
/// the FetchXml text for the caller's own GUID, matching the actual query shape it issues.
/// </summary>
sealed class FakeQueueOrganizationService : IOrganizationService
{
    private readonly Guid _approvalOneId;
    private readonly Guid _approvalTwoId;
    private readonly Guid _approverAId;
    private readonly Guid _approverBId;
    private readonly Guid _superApproverUserId;

    public FakeQueueOrganizationService(
        Guid approvalOneId, Guid approvalTwoId, Guid approverAId, Guid approverBId, Guid superApproverUserId)
    {
        _approvalOneId = approvalOneId;
        _approvalTwoId = approvalTwoId;
        _approverAId = approverAId;
        _approverBId = approverBId;
        _superApproverUserId = superApproverUserId;
    }

    public string? LastTeamFetchXml { get; private set; }
    public Guid SuperTeamId { get; } = Guid.NewGuid();
    private readonly Guid _superDefinitionId = Guid.NewGuid();
    public bool SuperConfigurationExists { get; set; } = true;
    public bool HasSuperCurrentValue { get; set; }
    public string? SuperCurrentValue { get; set; }
    public bool DuplicateSuperCurrentValue { get; set; }
    public Guid? ReadOnlyUserId { get; set; }
    public Guid? OrdinaryApproverUserId { get; set; }
    private readonly Guid _approverTeamId = Guid.NewGuid();
    private readonly Guid _readOnlyTeamId = Guid.NewGuid();

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        if (query is FetchExpression fetch)
        {
            LastTeamFetchXml = fetch.Query;
            var isMember = fetch.Query.Contains(_superApproverUserId.ToString("D"), StringComparison.OrdinalIgnoreCase) && fetch.Query.Contains(SuperTeamId.ToString("D"), StringComparison.OrdinalIgnoreCase);
            return isMember
                ? new EntityCollection(new List<Entity> { new Entity("team") })
                : new EntityCollection();
        }

        var qe = (QueryExpression)query;
        if (qe.EntityName == "environmentvariabledefinition")
        {
            var schema = (string)qe.Criteria.Conditions.Single(c => c.AttributeName == "schemaname").Values[0];
            if (schema == "fmi_DealApprovalSuperApproverTeam")
            {
                if (!SuperConfigurationExists) return new EntityCollection();
                var superDefinition = new Entity("environmentvariabledefinition", _superDefinitionId);
                superDefinition["defaultvalue"] = SuperTeamId.ToString("D");
                return new EntityCollection(new List<Entity> { superDefinition });
            }
            var isApproverConfig = schema == "fmi_DealApprovalApproverTeam";
            if (isApproverConfig ? !OrdinaryApproverUserId.HasValue : !ReadOnlyUserId.HasValue) return new EntityCollection();
            var definition = new Entity("environmentvariabledefinition", Guid.NewGuid());
            definition["defaultvalue"] = (isApproverConfig ? _approverTeamId : _readOnlyTeamId).ToString("D");
            return new EntityCollection(new List<Entity> { definition });
        }
        if (qe.EntityName == "environmentvariablevalue")
        {
            var definitionId = (Guid)qe.Criteria.Conditions.Single(c => c.AttributeName == "environmentvariabledefinitionid").Values[0];
            if (definitionId != _superDefinitionId || !HasSuperCurrentValue) return new EntityCollection();
            var value = new Entity("environmentvariablevalue", Guid.NewGuid());
            value["value"] = SuperCurrentValue;
            return new EntityCollection(DuplicateSuperCurrentValue ? new List<Entity> { value, value } : new List<Entity> { value });
        }
        if (qe.EntityName == "teammembership")
        {
            var team = (Guid)qe.Criteria.Conditions.Single(c => c.AttributeName == "teamid").Values[0];
            var user = (Guid)qe.Criteria.Conditions.Single(c => c.AttributeName == "systemuserid").Values[0];
            return (team == _readOnlyTeamId && user == ReadOnlyUserId) || (team == _approverTeamId && user == OrdinaryApproverUserId)
                ? new EntityCollection(new List<Entity> { new Entity("teammembership") })
                : new EntityCollection();
        }

        if (qe.EntityName == "fmi_dealapproval")
        {
            var approverCondition = qe.Criteria.Conditions.FirstOrDefault(c => c.AttributeName == "fmi_approver");
            var results = new List<Entity>();

            if (approverCondition == null)
            {
                // Super Approver path: every Pending record, no approver filter applied.
                results.Add(BuildApproval(_approvalOneId, _approverAId, "Opp One", "Company One", 1000m));
                results.Add(BuildApproval(_approvalTwoId, _approverBId, "Opp Two", "Company Two", 2000m));
            }
            else
            {
                var filterApprover = (Guid)approverCondition.Values[0];

                if (filterApprover == _approverAId)
                {
                    results.Add(BuildApproval(_approvalOneId, _approverAId, "Opp One", "Company One", 1000m));
                }
                else if (filterApprover == _approverBId)
                {
                    results.Add(BuildApproval(_approvalTwoId, _approverBId, "Opp Two", "Company Two", 2000m));
                }
            }

            return new EntityCollection(results);
        }

        if (qe.EntityName == "fmi_dealapprovalitem")
        {
            var dealApprovalId = (Guid)qe.Criteria.Conditions.First(c => c.AttributeName == "fmi_dealapproval").Values[0];

            if (dealApprovalId == _approvalOneId)
            {
                return new EntityCollection(new List<Entity> { BuildItem(true), BuildItem(false) });
            }

            if (dealApprovalId == _approvalTwoId)
            {
                return new EntityCollection(new List<Entity> { BuildItem(false), BuildItem(false), BuildItem(false) });
            }

            return new EntityCollection();
        }

        throw new NotSupportedException($"FakeQueueOrganizationService does not support querying '{qe.EntityName}'.");
    }

    private static Entity BuildApproval(Guid id, Guid approverId, string opportunityName, string companyName, decimal dealValue)
    {
        var entity = new Entity("fmi_dealapproval", id);
        entity["fmi_opportunity"] = new EntityReference("opportunity", Guid.NewGuid());
        entity["fmi_submittedcompany"] = new EntityReference("account", Guid.NewGuid());
        entity["fmi_approver"] = new EntityReference("systemuser", approverId);
        entity["fmi_requestedby"] = new EntityReference("systemuser", Guid.NewGuid());
        entity["createdon"] = DateTime.UtcNow;
        entity["fmi_submitteddealvalue"] = new Money(dealValue);
        entity["fmi_approvalstatus"] = new OptionSetValue(1);
        entity["opp.name"] = new AliasedValue("opportunity", "name", opportunityName);
        entity["acct.name"] = new AliasedValue("account", "name", companyName);
        entity["apprv.fullname"] = new AliasedValue("systemuser", "fullname", "Approver Name");
        entity["reqby.fullname"] = new AliasedValue("systemuser", "fullname", "Requester Name");
        return entity;
    }

    private static Entity BuildItem(bool belowForecast)
    {
        var entity = new Entity("fmi_dealapprovalitem", Guid.NewGuid());
        entity["fmi_belowforecast"] = belowForecast;
        return entity;
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException("Retrieve is not exercised by GetPendingDealApprovals.");
    public Guid Create(Entity entity) => throw new NotSupportedException("Create is not exercised by GetPendingDealApprovals.");
    public void Update(Entity entity) => throw new NotSupportedException("Update is not exercised by GetPendingDealApprovals.");
    public void Delete(string entityName, Guid id) => throw new NotSupportedException("Delete is not exercised by GetPendingDealApprovals.");
    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException("Execute is not exercised by GetPendingDealApprovals.");
    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
}

/// <summary>
/// Fakes itemCount Opportunity Items, each with a fully distinct Content/Target Territory pair
/// (and therefore a fully distinct BWG/BWT combination, sharing one Business Written Year), plus
/// a matching Goal row per combination - the worst case for ResolveBudgets' per-combination
/// condition count. Branches on which entity a FetchExpression targets by string-matching the
/// FetchXml, mirroring FakeQueueOrganizationService's approach for FetchExpression-based fakes.
/// </summary>
sealed class FakeFinancialSnapshotOrganizationService : IOrganizationService
{
    private readonly int _itemCount;
    private readonly List<Guid> _contentIds = new();
    private readonly List<Guid> _territoryIds = new();
    private readonly List<Guid> _bwgIds = new();
    private readonly List<Guid> _bwtIds = new();
    private readonly List<Guid> _opportunityItemIds = new();
    private readonly Guid _bwyId = Guid.NewGuid();

    public string? LastGoalFetchXml { get; private set; }

    public FakeFinancialSnapshotOrganizationService(int itemCount)
    {
        _itemCount = itemCount;

        for (var i = 0; i < itemCount; i++)
        {
            _contentIds.Add(Guid.NewGuid());
            _territoryIds.Add(Guid.NewGuid());
            _bwgIds.Add(Guid.NewGuid());
            _bwtIds.Add(Guid.NewGuid());
            _opportunityItemIds.Add(Guid.NewGuid());
        }
    }

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        var fetch = (FetchExpression)query;
        var xml = fetch.Query;

        if (xml.Contains("<entity name='fmi_opportunityitem'>"))
        {
            var items = new List<Entity>();

            for (var i = 0; i < _itemCount; i++)
            {
                var e = new Entity("fmi_opportunityitem", _opportunityItemIds[i]);
                e["fmi_name"] = $"Item {i}";
                e["fmi_content"] = new EntityReference("fmi_content", _contentIds[i]);
                e["fmi_targetterritory"] = new EntityReference("fmi_targetterritory", _territoryIds[i]);
                e["fmi_businesswrittenyear"] = new EntityReference("fmi_businesswrittenyear", _bwyId) { Name = "FY26" };
                e["fmi_actualrevenue_base"] = new Money(1000m + i);
                items.Add(e);
            }

            return new EntityCollection(items);
        }

        if (xml.Contains("<entity name='fmi_content'>"))
        {
            var rows = new List<Entity>();

            for (var i = 0; i < _itemCount; i++)
            {
                var e = new Entity("fmi_content", _contentIds[i]);
                e["fmi_name"] = $"Content {i}";
                e["bwg.fmi_businesswrittengroupid"] = new AliasedValue("fmi_businesswrittengroup", "fmi_businesswrittengroupid", _bwgIds[i]);
                e["bwg.fmi_name"] = new AliasedValue("fmi_businesswrittengroup", "fmi_name", $"BWG {i}");
                rows.Add(e);
            }

            return new EntityCollection(rows);
        }

        if (xml.Contains("<entity name='fmi_targetterritory'>"))
        {
            var rows = new List<Entity>();

            for (var i = 0; i < _itemCount; i++)
            {
                var e = new Entity("fmi_targetterritory", _territoryIds[i]);
                e["fmi_name"] = $"Territory {i}";
                e["fmi_businesswrittenterritory"] = new EntityReference("fmi_businesswrittenterritory", _bwtIds[i]) { Name = $"BWT {i}" };
                rows.Add(e);
            }

            return new EntityCollection(rows);
        }

        if (xml.Contains("<entity name='goal'>"))
        {
            LastGoalFetchXml = xml;
            var rows = new List<Entity>();

            for (var i = 0; i < _itemCount; i++)
            {
                var e = new Entity("goal", Guid.NewGuid());
                e["fmi_businesswrittengroup"] = new EntityReference("fmi_businesswrittengroup", _bwgIds[i]);
                e["fmi_bwterritory"] = new EntityReference("fmi_businesswrittenterritory", _bwtIds[i]);
                e["fmi_businesswrittenyear"] = new EntityReference("fmi_businesswrittenyear", _bwyId);
                e["fmi_currentyearbudget"] = new Money(500m);
                e["fmi_fc3"] = new Money(900m);
                rows.Add(e);
            }

            return new EntityCollection(rows);
        }

        throw new NotSupportedException($"FakeFinancialSnapshotOrganizationService does not support this fetch: {xml}");
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();
    public Guid Create(Entity entity) => throw new NotSupportedException();
    public void Update(Entity entity) => throw new NotSupportedException();
    public void Delete(string entityName, Guid id) => throw new NotSupportedException();
    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotSupportedException();
    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();
}
