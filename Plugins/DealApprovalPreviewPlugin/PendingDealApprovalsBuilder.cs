using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using DealApprovalPreviewPlugin.Core;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Builds the read-only Pending Deal Approval queue for the Approval Centre landing page.
    /// Scope is decided once, server-side, by SuperApproverResolver - the same class
    /// DecisionBuilder already uses to authorize decisions - never by client-side team
    /// membership logic: a Deal SuperApprover sees every Pending Deal Approval, everyone else
    /// sees only the ones where fmi_approver is them. This mirrors PreviewBuilder's JSON-response
    /// style (Dictionary&lt;string, object&gt; + JavaScriptSerializer) for consistency with the
    /// existing fmi_GetDealApprovalPreview API.
    ///
    /// Deliberately uses submitted snapshot data only - fmi_submitteddealvalue and
    /// fmi_dealapprovalitem.fmi_belowforecast as they were at Submit time - never the current
    /// live Opportunity/Opportunity Item financials, matching fmi_GetDealApprovalPreview's own
    /// "resolve independently, never trust stale client state" posture but applied to already-
    /// persisted snapshot rows rather than a live recalculation.
    /// </summary>
    internal sealed class PendingDealApprovalsBuilder
    {
        private const string DealApprovalLogicalName = "fmi_dealapproval";
        private const string DealApprovalItemLogicalName = "fmi_dealapprovalitem";
        private const string OpportunityLogicalName = "opportunity";
        private const string AccountLogicalName = "account";
        private const string SystemUserLogicalName = "systemuser";

        private readonly IOrganizationService _service;
        private readonly SuperApproverResolver _superApproverResolver;

        public PendingDealApprovalsBuilder(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _superApproverResolver = new SuperApproverResolver(service);
        }

        public string Build(Guid callerId)
        {
            var isSuperApprover = _superApproverResolver.IsSuperApprover(callerId);
            var approvals = RetrievePendingApprovals(callerId, isSuperApprover);

            var items = approvals
                .Select(BuildApprovalSummary)
                .ToList();

            var response = new Dictionary<string, object>
            {
                ["approvals"] = items,
                ["count"] = items.Count
            };

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            return serializer.Serialize(response);
        }

        // --- Retrieval ------------------------------------------------------------------

        private List<Entity> RetrievePendingApprovals(Guid callerId, bool isSuperApprover)
        {
            var query = new QueryExpression(DealApprovalLogicalName)
            {
                ColumnSet = new ColumnSet(
                    "fmi_opportunity",
                    "fmi_submittedcompany",
                    "fmi_approver",
                    "fmi_requestedby",
                    "createdon",
                    "fmi_submitteddealvalue",
                    "fmi_approvalstatus")
            };

            query.Criteria.AddCondition("fmi_approvalstatus", ConditionOperator.Equal, DealApprovalStatus.Pending);

            if (!isSuperApprover)
            {
                query.Criteria.AddCondition("fmi_approver", ConditionOperator.Equal, callerId);
            }

            query.AddOrder("createdon", OrderType.Ascending);

            var opportunityLink = query.AddLink(OpportunityLogicalName, "fmi_opportunity", "opportunityid", JoinOperator.LeftOuter);
            opportunityLink.EntityAlias = "opp";
            opportunityLink.Columns = new ColumnSet("name");

            var companyLink = query.AddLink(AccountLogicalName, "fmi_submittedcompany", "accountid", JoinOperator.LeftOuter);
            companyLink.EntityAlias = "acct";
            companyLink.Columns = new ColumnSet("name");

            var approverLink = query.AddLink(SystemUserLogicalName, "fmi_approver", "systemuserid", JoinOperator.LeftOuter);
            approverLink.EntityAlias = "apprv";
            approverLink.Columns = new ColumnSet("fullname");

            var requestedByLink = query.AddLink(SystemUserLogicalName, "fmi_requestedby", "systemuserid", JoinOperator.LeftOuter);
            requestedByLink.EntityAlias = "reqby";
            requestedByLink.Columns = new ColumnSet("fullname");

            return _service.RetrieveMultiple(query).Entities.ToList();
        }

        /// <summary>
        /// One RetrieveMultiple per Pending Deal Approval, filtered to just fmi_belowforecast.
        /// N+1 rather than a FetchXML aggregate: at demo/production scale for a single approver's
        /// or Super Approver's Pending queue this is simpler and more reliable than aggregate
        /// FetchXML, and matches "smallest clean implementation" over premature optimization.
        /// </summary>
        private (int ItemCount, int BelowForecastCount) RetrieveItemCounts(Guid dealApprovalId)
        {
            var query = new QueryExpression(DealApprovalItemLogicalName)
            {
                ColumnSet = new ColumnSet("fmi_belowforecast")
            };

            query.Criteria.AddCondition("fmi_dealapproval", ConditionOperator.Equal, dealApprovalId);

            var items = _service.RetrieveMultiple(query).Entities;
            var belowForecastCount = items.Count(i => i.GetAttributeValue<bool>("fmi_belowforecast"));

            return (items.Count, belowForecastCount);
        }

        // --- Response assembly -----------------------------------------------------------

        private Dictionary<string, object> BuildApprovalSummary(Entity approval)
        {
            var opportunityRef = approval.GetAttributeValue<EntityReference>("fmi_opportunity");
            var companyRef = approval.GetAttributeValue<EntityReference>("fmi_submittedcompany");
            var approverRef = approval.GetAttributeValue<EntityReference>("fmi_approver");
            var requestedByRef = approval.GetAttributeValue<EntityReference>("fmi_requestedby");
            var requestedOn = approval.GetAttributeValue<DateTime>("createdon");
            var submittedDealValue = approval.GetAttributeValue<Money>("fmi_submitteddealvalue")?.Value ?? 0m;
            var approvalStatus = approval.GetAttributeValue<OptionSetValue>("fmi_approvalstatus")?.Value ?? 0;

            var (itemCount, belowForecastCount) = RetrieveItemCounts(approval.Id);

            return new Dictionary<string, object>
            {
                ["dealApprovalId"] = approval.Id.ToString(),
                ["opportunityId"] = opportunityRef?.Id.ToString(),
                ["opportunityName"] = GetAliasedString(approval, "opp", "name") ?? string.Empty,
                ["companyId"] = companyRef?.Id.ToString(),
                ["companyName"] = GetAliasedString(approval, "acct", "name") ?? string.Empty,
                ["approverId"] = approverRef?.Id.ToString(),
                ["approverName"] = GetAliasedString(approval, "apprv", "fullname") ?? string.Empty,
                ["requestedById"] = requestedByRef?.Id.ToString(),
                ["requestedByName"] = GetAliasedString(approval, "reqby", "fullname") ?? string.Empty,
                ["requestedOn"] = requestedOn.ToString("o", CultureInfo.InvariantCulture),
                ["submittedDealValue"] = submittedDealValue,
                ["itemCount"] = itemCount,
                ["belowForecastCount"] = belowForecastCount,
                ["approvalStatus"] = approvalStatus
            };
        }

        private static string GetAliasedString(Entity entity, string alias, string attributeName)
        {
            var key = $"{alias}.{attributeName}";

            if (!entity.Contains(key))
            {
                return null;
            }

            return (entity[key] as AliasedValue)?.Value as string;
        }
    }
}
