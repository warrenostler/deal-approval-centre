using System;
using Microsoft.Xrm.Sdk;

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

        public bool IsMember(Guid userId) => new ConfiguredTeamResolver(_service).IsMember(userId, "fmi_DealApprovalReadOnlyTeam");
        public bool IsApproverTeamMember(Guid userId) => new ConfiguredTeamResolver(_service).IsMember(userId, "fmi_DealApprovalApproverTeam");

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

    }
}
