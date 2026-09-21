using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace DealApprovalPreviewPlugin
{
    /// <summary>
    /// Resolves whether a user is currently a member of the "Deal SuperApprover" Owner Team - the
    /// team allowed to decide any Pending Deal Approval regardless of fmi_approver. Queries by
    /// team name rather than a hard-coded GUID, constrained to Owner teams (teamtype=0) to reduce
    /// the chance of matching an unrelated team that happens to share the name - the practical
    /// limit of what a name-based lookup can guarantee without a Business Unit to anchor to.
    ///
    /// Does NOT filter on statecode: the live Dataverse team entity in FM TEST does not expose a
    /// statecode attribute at all (confirmed live - a statecode condition here fails the query
    /// outright with "'Team' entity doesn't contain attribute with Name = 'statecode'", it does
    /// not silently return zero rows). There is no state-based concept of an "inactive" Team to
    /// filter out here; name + teamtype is the full extent of what this lookup can constrain.
    ///
    /// "Active/current membership" is interpreted as: a teammembership row currently exists for
    /// this user against this team. Dataverse does not carry a separate active/inactive flag on
    /// team membership itself - removal from the team deletes the row, so row-existence already
    /// fully captures "is a current member" without any extra state to check.
    ///
    /// Team resolution and membership are checked in a single query (team joined to teammembership,
    /// filtered by both sides) so that "no matching row" uniformly means "not a Super Approver",
    /// whether that is because the team cannot be found at all, is the wrong type, or the user
    /// simply is not a member - i.e. this fails closed by construction, not by a separate check.
    ///
    /// DecisionBuilder passes this class an elevated service (not the calling user's own), so an
    /// otherwise-legitimate caller who has no Read privilege on team/teammembership at all (a
    /// perfectly normal security-role configuration for an ordinary approver) does not get an
    /// unrelated permission failure just from this membership check.
    /// </summary>
    internal sealed class SuperApproverResolver
    {
        private const string TeamLogicalName = "team";
        private const string TeamMembershipRelationshipName = "teammembership";
        private const string SuperApproverTeamName = "Deal SuperApprover";
        private const int OwnerTeamType = 0;

        private readonly IOrganizationService _service;

        public SuperApproverResolver(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public bool IsSuperApprover(Guid userId)
        {
            var fetchXml = $@"
                <fetch top='1'>
                  <entity name='{TeamLogicalName}'>
                    <attribute name='teamid' />
                    <filter>
                      <condition attribute='name' operator='eq' value='{SuperApproverTeamName}' />
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
