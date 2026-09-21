using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    // Membership grants visibility only. Decision rights are evaluated separately.
    internal sealed class ReadOnlyTeamResolver
    {
        private readonly IOrganizationService _service;
        public ReadOnlyTeamResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsMember(Guid userId) => IsTeamMember(userId, "fmi_DealApprovalReadOnlyTeam");
        public bool IsApproverTeamMember(Guid userId) => IsTeamMember(userId, "fmi_DealApprovalApproverTeam");

        public bool CanSubmit(Guid userId)
        {
            return new SuperApproverResolver(_service).IsSuperApprover(userId) ||
                IsApproverTeamMember(userId) || !IsMember(userId);
        }

        public void EnsureCanSubmit(Guid userId)
        {
            if (!CanSubmit(userId))
                throw new InvalidPluginExecutionException("You have read-only access to Deal Approval Centre and cannot submit or cancel approval requests.");
        }

        private bool IsTeamMember(Guid userId, string schemaName)
        {
            var definitions = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("defaultvalue")
            };
            definitions.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);
            var definition = _service.RetrieveMultiple(definitions).Entities.SingleOrDefault();
            if (definition == null) return false;

            var values = new QueryExpression("environmentvariablevalue")
            {
                ColumnSet = new ColumnSet("value")
            };
            values.Criteria.AddCondition("environmentvariabledefinitionid", ConditionOperator.Equal, definition.Id);
            values.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            var currentValues = _service.RetrieveMultiple(values).Entities;
            if (currentValues.Count > 1)
                throw new InvalidPluginExecutionException(schemaName + " has multiple current values. Configure a single team GUID.");
            var raw = currentValues.Count == 1
                ? currentValues[0].GetAttributeValue<string>("value")
                : definition.GetAttributeValue<string>("defaultvalue");
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!Guid.TryParse(raw.Trim(), out var teamId) || teamId == Guid.Empty)
                throw new InvalidPluginExecutionException(schemaName + " must contain a valid team GUID.");

            var membership = new QueryExpression("teammembership") { ColumnSet = new ColumnSet("teamid"), TopCount = 1 };
            membership.Criteria.AddCondition("teamid", ConditionOperator.Equal, teamId);
            membership.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            return _service.RetrieveMultiple(membership).Entities.Count > 0;
        }
    }
}