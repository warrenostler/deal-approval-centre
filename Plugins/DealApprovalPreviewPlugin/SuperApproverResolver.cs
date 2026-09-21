using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// SuperApprover membership is determined by the owner-team GUID configured in
    /// fmi_DealApprovalSuperApproverTeam. No team-name or GUID fallback is used.
    /// Checks the real initiating user through an elevated service. Missing/blank
    /// configuration or absent membership grants no SuperApprover access.
    /// </summary>
    internal sealed class SuperApproverResolver
    {
        private const string TeamLogicalName = "team";
        private const string TeamMembershipRelationshipName = "teammembership";
        internal const string EnvironmentVariableSchemaName = "fmi_DealApprovalSuperApproverTeam";
        private const int OwnerTeamType = 0;

        private readonly IOrganizationService _service;

        public SuperApproverResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsSuperApprover(Guid userId)
        {
            var teamId = new ConfiguredTeamResolver(_service).ResolveTeamId(EnvironmentVariableSchemaName);
            if (!teamId.HasValue) return false;

            var fetchXml = $@"
                <fetch top='1'>
                  <entity name='{TeamLogicalName}'>
                    <attribute name='teamid' />
                    <filter>
                      <condition attribute='teamid' operator='eq' value='{teamId.Value:D}' />
                      <condition attribute='teamtype' operator='eq' value='{OwnerTeamType}' />
                    </filter>
                    <link-entity name='{TeamMembershipRelationshipName}' from='teamid' to='teamid' intersect='true' link-type='inner'>
                      <filter>
                        <condition attribute='systemuserid' operator='eq' value='{userId:D}' />
                      </filter>
                    </link-entity>
                  </entity>
                </fetch>";

            return _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities.Count > 0;
        }
    }
}
