using System;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// PreValidation guard on fmi_dealapproval Delete, Assign, SetState and
    /// SetStateDynamicEntity: always blocks, unconditionally, for every one of these messages,
    /// with no marker exception at all.
    ///
    /// A submitted Deal Approval is the evidence trail for a decision. Neither Submit nor
    /// Decision ever deletes, reassigns ownership of, or deactivates a Deal Approval, so there is
    /// no legitimate path to allow. fmi_dealapproval is UserOwned (confirmed from the FM TEST
    /// solution export's Entity.xml OwnershipTypeMask), so Assign is a real, callable message
    /// here, not a hypothetical - and as a UserOwned custom entity it also carries the standard
    /// Active/Inactive state model, so SetState/SetStateDynamicEntity are real routes too. Both
    /// are distinct SDK messages from Update, which is why they need their own step registrations
    /// and cannot be caught by DealApprovalParentUpdateGuard.
    ///
    /// This intentionally does not touch fmi_approvalstatus, our own business approval-status
    /// choice field - that is a normal attribute governed by DealApprovalParentUpdateGuard, wholly
    /// separate from Dataverse's own statecode/statuscode record-level state.
    ///
    /// One exception: a caller holding the System Administrator security role is let through all
    /// of these messages. Checked via SystemAdministratorResolver against the real human caller
    /// (context.InitiatingUserId), using the elevated service so the check itself never depends on
    /// what privilege that caller's own role happens to grant on this table.
    /// </summary>
    public class DealApprovalParentLockGuard : PluginBase
    {
        public DealApprovalParentLockGuard(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(DealApprovalParentLockGuard))
        {
        }

        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            var context = localPluginContext.PluginExecutionContext;
            var elevatedService = localPluginContext.OrgSvcFactory.CreateOrganizationService(null);
            if (new SystemAdministratorResolver(elevatedService).IsSystemAdministrator(context.InitiatingUserId))
            {
                return;
            }

            switch (localPluginContext.PluginExecutionContext.MessageName)
            {
                case "Delete":
                    throw new InvalidPluginExecutionException("A submitted Deal Approval cannot be deleted.");

                case "Assign":
                    throw new InvalidPluginExecutionException("A submitted Deal Approval cannot be reassigned to another owner.");

                case "SetState":
                case "SetStateDynamicEntity":
                    throw new InvalidPluginExecutionException("A submitted Deal Approval cannot be deactivated or reactivated.");

                default:
                    throw new InvalidPluginExecutionException("This operation on the Deal Approval is not permitted.");
            }
        }
    }
}
