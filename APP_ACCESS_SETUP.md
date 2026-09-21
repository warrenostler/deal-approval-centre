# Deal Approval Centre access (plugin 1.5.0)

## Configuration

Create Text environment variables in the DealApproval solution and set their current values to Dataverse team GUIDs:

| Schema name | Purpose |
| --- | --- |
| `fmi_DealApprovalReadOnlyTeam` | Members can view all pending approvals. Does not grant decision rights. |
| `fmi_DealApprovalApproverTeam` | Optional separate ordinary-approver team. Takes precedence over read-only membership for app mode and request submission/cancellation. Does not grant SuperApprover rights. |

The existing `Deal SuperApprover` owner team remains the source of permission to decide any pending approval. Both ordinary approver membership and SuperApprover membership take precedence over read-only membership. An assigned approver can still decide their assigned record, including when they also belong to the read-only team.

Missing or blank team configuration grants no additional access. The current value overrides the default value; a blank current value disables that team configuration. Invalid GUIDs or multiple active current values produce a configuration error.

## App sharing and Dataverse permissions

Share the app with the relevant teams. Environment variables do not share the app or grant Dataverse table privileges.

Give the read-only team organization-wide Read access to the app's underlying tables, including Deal Approval, Deal Approval Item, Opportunity, Opportunity Item, Account, System User, Content, Target Territory, Business Written Group, Business Written Year and Goal/Budget, plus the necessary access to run the read APIs. The queue custom API resolves visibility server-side; details and history use the caller's Dataverse Read privileges. Do not grant write privileges through the read-only team.

Custom APIs enforce decision authorization and block read-only-only callers from submitting or cancelling. Ordinary approvers can decide only records assigned to them; SuperApprovers can decide any pending record. Cancellation still requires being the original requester and a pending request.

## UI behaviour

- Deal type and text search filter the queue together. Cards identify their deal type.
- Assigned to Me matches the real initiating user's ID to the assigned approver.
- SuperApprovers and read-only members can select All. Read-only-only users start in All; approvers start in Assigned to Me.
- Read-only membership can grant an ordinary approver visibility of additional records, but those records are labelled View only and have no decision controls.
- Home Entertainment and Inflight omit forecast metrics; Ancillary also omits item count.
- Approval detail and request submission determine action visibility from the server response even when opened through a direct URL. Completed approvals do not show decision actions.

## Deployment and validation

Deploy plugin package `fmi_DealApprovalPreviewPlugin.1.5.0.nupkg` manually through Plugin Registration Tool Update Content, then use the matching code app in PP FM TEST. Existing Custom API names and registered parameter definitions are unchanged: the extra access fields are inside the existing JSON response.

The updated app requires the 1.5.0 access fields to enable submission/decision controls. Before the plugin update, those controls stay hidden. The new team experience also requires team configuration, app sharing and Dataverse Read permissions described above.

Validate in PP FM TEST with an ordinary approver, SuperApprover, read-only member and a user with overlapping memberships. Check queue scopes, deal-type/search combinations, direct detail URLs, approval history, allowed decisions and denied read-only submit/cancel calls. No UAT or Live deployment is included.
