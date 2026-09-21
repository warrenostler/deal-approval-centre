using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Dataverse-facing half of Deal Approval approver routing: reads the
    /// fmi_DealApprovalDefaultApprover environment variable, validates systemuser
    /// existence/active status, and resolves each Target Territory's Default Approver
    /// (fmi_defaultapprover, confirmed from the FM TEST solution export's
    /// Entities/fmi_targetterritory/Entity.xml - not guessed). The routing decision itself is
    /// Dataverse-independent and lives in Core's ApproverRouter; this class only supplies the
    /// resolved primitives that decision needs.
    /// </summary>
    internal sealed class ApproverResolver
    {
        private const string SystemUserLogicalName = "systemuser";
        private const string TargetTerritoryLogicalName = "fmi_targetterritory";
        private const string TargetTerritoryDefaultApproverAttributeName = "fmi_defaultapprover";
        private const string EnvironmentVariableDefinitionLogicalName = "environmentvariabledefinition";
        private const string EnvironmentVariableValueLogicalName = "environmentvariablevalue";

        private const string DefaultApproverEnvironmentVariableSchemaName = "fmi_DealApprovalDefaultApprover";
        private const string TapeApproverEmeaEnvironmentVariableSchemaName = "fmi_DealApproverTapeEMEA";
        private const string TapeApproverNonEmeaEnvironmentVariableSchemaName = "fmi_DealApproverTapeNonEMEA";
        private const string TargetTerritoryRegionAttributeName = "fmi_region";
        private const string RegionEmea = "EMEA";

        private readonly IOrganizationService _service;

        public ApproverResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public FallbackApprover ResolveFallbackApprover()
        {
            var rawValue = ReadEnvironmentVariableValue(DefaultApproverEnvironmentVariableSchemaName);

            if (string.IsNullOrWhiteSpace(rawValue) || !Guid.TryParse(rawValue.Trim(), out var fallbackId))
            {
                return new FallbackApprover { Id = null, IsActive = false };
            }

            return new FallbackApprover { Id = fallbackId, IsActive = IsUserActive(fallbackId) };
        }

        public bool IsUserActive(Guid userId)
        {
            try
            {
                var user = _service.Retrieve(SystemUserLogicalName, userId, new ColumnSet("isdisabled"));
                return user.GetAttributeValue<bool?>("isdisabled") != true;
            }
            catch (Exception)
            {
                return false; // does not exist, or not accessible
            }
        }

        /// <summary>
        /// Straight one-to-one environment-variable approver lookup, with no territory or region
        /// logic - used for Sales Types other than Tape that route to a single fixed approver.
        /// </summary>
        public Guid? ResolveApproverBySchemaName(string schemaName)
        {
            var rawValue = ReadEnvironmentVariableValue(schemaName);

            if (string.IsNullOrWhiteSpace(rawValue) || !Guid.TryParse(rawValue.Trim(), out var userId))
            {
                return null;
            }

            return userId;
        }

        public Guid? ResolveTapeApprover(IReadOnlyList<ResolvedFinancialItem> resolvedItems)
        {
            if (resolvedItems == null || resolvedItems.Count == 0)
            {
                return null;
            }

            var territoryIds = resolvedItems
                .Select(item => item.TargetTerritoryRef?.Id)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToList();

            if (territoryIds.Count == 0)
            {
                return null;
            }

            var regions = ReadTerritoryRegions(territoryIds);
            var allEmea = regions.Count == territoryIds.Count &&
                          regions.Values.All(region => string.Equals(region, RegionEmea, StringComparison.OrdinalIgnoreCase));

            var schemaName = allEmea
                ? TapeApproverEmeaEnvironmentVariableSchemaName
                : TapeApproverNonEmeaEnvironmentVariableSchemaName;

            var rawValue = ReadEnvironmentVariableValue(schemaName);

            if (string.IsNullOrWhiteSpace(rawValue) || !Guid.TryParse(rawValue.Trim(), out var userId))
            {
                return null;
            }

            return userId;
        }

        /// <summary>
        /// Territory ID -> Default Approver systemuser ID (null when that Target Territory has no
        /// Default Approver configured).
        /// </summary>
        public Dictionary<Guid, Guid?> ResolveTerritoryApprovers(IEnumerable<Guid> distinctTerritoryIds)
        {
            var territoryIds = distinctTerritoryIds.ToList();
            var result = new Dictionary<Guid, Guid?>();

            if (territoryIds.Count == 0)
            {
                return result;
            }

            var values = string.Join(string.Empty, territoryIds.Select(id => $"<value>{id:D}</value>"));

            var fetchXml = $@"
                <fetch>
                  <entity name='{TargetTerritoryLogicalName}'>
                    <attribute name='fmi_targetterritoryid' />
                    <attribute name='{TargetTerritoryDefaultApproverAttributeName}' />
                    <filter>
                      <condition attribute='fmi_targetterritoryid' operator='in'>{values}</condition>
                    </filter>
                  </entity>
                </fetch>";

            foreach (var territory in _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities)
            {
                var approverRef = territory.GetAttributeValue<EntityReference>(TargetTerritoryDefaultApproverAttributeName);
                result[territory.Id] = approverRef?.Id;
            }

            return result;
        }

        /// <summary>
        /// The environment variable's overriding value if one has been set for this environment,
        /// otherwise its definition-level default value (mirrors how environment variables
        /// resolve generally - the override, when present, always wins).
        /// </summary>
        private Dictionary<Guid, string> ReadTerritoryRegions(IEnumerable<Guid> territoryIds)
        {
            var ids = territoryIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            var values = string.Join(string.Empty, ids.Select(id => $"<value>{id:D}</value>"));

            var fetchXml = $@"
                <fetch>
                  <entity name='{TargetTerritoryLogicalName}'>
                    <attribute name='fmi_targetterritoryid' />
                    <attribute name='{TargetTerritoryRegionAttributeName}' />
                    <filter>
                      <condition attribute='fmi_targetterritoryid' operator='in'>{values}</condition>
                    </filter>
                  </entity>
                </fetch>";

            var result = new Dictionary<Guid, string>();

            foreach (var territory in _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities)
            {
                var region = territory.GetAttributeValue<string>(TargetTerritoryRegionAttributeName);
                result[territory.Id] = region ?? string.Empty;
            }

            return result;
        }

        private string ReadEnvironmentVariableValue(string schemaName)
        {
            var fetchXml = $@"
                <fetch>
                  <entity name='{EnvironmentVariableDefinitionLogicalName}'>
                    <attribute name='defaultvalue' />
                    <filter>
                      <condition attribute='schemaname' operator='eq' value='{schemaName}' />
                    </filter>
                    <link-entity name='{EnvironmentVariableValueLogicalName}' from='environmentvariabledefinitionid' to='environmentvariabledefinitionid' link-type='outer' alias='envval'>
                      <attribute name='value' />
                    </link-entity>
                  </entity>
                </fetch>";

            var definition = _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.FirstOrDefault();

            if (definition == null)
            {
                return null;
            }

            var overrideValue = definition.GetAttributeValue<AliasedValue>("envval.value")?.Value as string;

            return !string.IsNullOrWhiteSpace(overrideValue)
                ? overrideValue
                : definition.GetAttributeValue<string>("defaultvalue");
        }

        public sealed class FallbackApprover
        {
            public Guid? Id { get; set; }
            public bool IsActive { get; set; }
        }
    }
}
