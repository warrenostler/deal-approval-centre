using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Whether the caller holds the built-in System Administrator security role, checked by role
    /// name (not a fixed roleid, which is not stable across business units or environments).
    /// Used only to let admins through the record-level guards on fmi_dealapproval and
    /// fmi_dealapprovalitem in the model-driven app; it has no bearing on the separate
    /// Approver/SuperApprover/ReadOnly team logic used by the Custom API plugins.
    /// </summary>
    internal sealed class SystemAdministratorResolver
    {
        private const string RoleName = "System Administrator";

        private readonly IOrganizationService _service;

        public SystemAdministratorResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsSystemAdministrator(Guid userId)
        {
            var fetchXml = $@"
                <fetch top='1'>
                  <entity name='role'>
                    <attribute name='roleid' />
                    <filter>
                      <condition attribute='name' operator='eq' value='{RoleName}' />
                    </filter>
                    <link-entity name='systemuserroles' from='roleid' to='roleid' intersect='true' link-type='inner'>
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
