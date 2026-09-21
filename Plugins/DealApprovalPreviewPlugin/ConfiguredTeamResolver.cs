using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    // Resolves team configuration consistently for all app access modes.
    internal sealed class ConfiguredTeamResolver
    {
        private readonly IOrganizationService _service;
        public ConfiguredTeamResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsMember(Guid userId, string schemaName)
        {
            var teamId = ResolveTeamId(schemaName);
            if (!teamId.HasValue) return false;
            var membership = new QueryExpression("teammembership") { ColumnSet = new ColumnSet("teamid"), TopCount = 1 };
            membership.Criteria.AddCondition("teamid", ConditionOperator.Equal, teamId.Value);
            membership.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            return _service.RetrieveMultiple(membership).Entities.Count > 0;
        }
        public Guid? ResolveTeamId(string schemaName)
        {
            var definitions = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("defaultvalue")
            };
            definitions.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);
            var definition = _service.RetrieveMultiple(definitions).Entities.SingleOrDefault();
            if (definition == null) return null;

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
            if (string.IsNullOrWhiteSpace(raw)) return null;
            if (!Guid.TryParse(raw.Trim(), out var teamId) || teamId == Guid.Empty)
                throw new InvalidPluginExecutionException(schemaName + " must contain a valid team GUID.");

            return teamId;
        }
    }
}
