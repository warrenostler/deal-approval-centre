using System;
using Microsoft.Xrm.Sdk;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// PreValidation guard on fmi_dealapprovalitem Update, Delete, Assign, SetState and
    /// SetStateDynamicEntity: always blocks, unconditionally, for every one of these messages,
    /// with no marker exception at all.
    ///
    /// These rows are the immutable evidential item-level snapshot of what an approver was asked
    /// to decide on. Unlike Create (which the Submit deep insert legitimately needs, and which
    /// DealApprovalItemCreateGuard separately protects), no current controlled process ever
    /// updates, deletes, reassigns, or deactivates them - so there is no marker exception to check
    /// here at all. fmi_dealapprovalitem is UserOwned (confirmed from the FM TEST solution
    /// export's Entity.xml OwnershipTypeMask), so Assign/SetState are real, callable messages
    /// here, not hypothetical ones.
    ///
    /// One exception: a caller holding the System Administrator security role is let through all
    /// of these messages. Checked via SystemAdministratorResolver against the real human caller
    /// (context.InitiatingUserId), using the elevated service so the check itself never depends on
    /// what privilege that caller's own role happens to grant on this table. Note this trades away
    /// the "permanent, unedited audit trail" guarantee described above whenever an admin actually
    /// uses this bypass.
    /// </summary>
    public class DealApprovalItemImmutabilityGuard : PluginBase
    {
        public DealApprovalItemImmutabilityGuard(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(DealApprovalItemImmutabilityGuard))
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
                case "Update":
                    throw new InvalidPluginExecutionException("Deal Approval Item snapshots cannot be modified after submission.");

                case "Delete":
                    throw new InvalidPluginExecutionException("Deal Approval Item snapshots cannot be deleted.");

                case "Assign":
                    throw new InvalidPluginExecutionException("Deal Approval Item snapshots cannot be reassigned to another owner.");

                case "SetState":
                case "SetStateDynamicEntity":
                    throw new InvalidPluginExecutionException("Deal Approval Item snapshots cannot be deactivated or reactivated.");

                default:
                    throw new InvalidPluginExecutionException("This operation on the Deal Approval Item is not permitted.");
            }
        }
    }
}
