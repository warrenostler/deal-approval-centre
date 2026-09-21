# Deal Approval Centre

## Purpose

Deal Approval Centre is a React/TypeScript/Vite Power Platform Code App embedded in the Fremantle model-driven CRM experience. It provides:

- A pending approval queue for approvers.
- Approval detail pages with submitted financial snapshots.
- Historic budget context for approval items.
- Contract ID search and approval audit history.
- Coordinator deal approval requests launched from an Opportunity.
- Approve/reject actions for assigned approvers.
- A cancellation path for approval requests created in error.

The application is designed to keep writes and business-rule enforcement server-side while providing a richer, faster user interface in React.

## Environments And App

Current configured environment:

- Environment: FM TEST
- Environment ID: `4b002f9f-ad17-e4a8-8582-0970ce77cdae`
- Organization URL: `https://orgf7602101.crm11.dynamics.com`
- Code App ID: `63bb01cf-9750-4567-889a-60e6e503a6ba`
- App display name: Deal Approval Centre
- App type: Power Platform Code App

The React app is deployed with:

```text
npm run build
npm run power:push
```

The separate `fmi_requestdealapprovalhost` web resource must be updated and published independently. `power:push` does not update that web resource.

## User Journeys

### Approver journey

1. Approver opens Deal Approval Centre.
2. The app loads pending approvals through `fmi_GetPendingDealApprovals`.
3. Approver selects an approval.
4. The detail page loads the Deal Approval and related Deal Approval Item records directly from Dataverse.
5. The approver can inspect submitted financial values, warnings, and historic budgets.
6. The approver approves or rejects through `fmi_ProcessDealApprovalDecision`.

### Coordinator request journey

1. User clicks the Opportunity ribbon command.
2. Ribbon JavaScript saves the Opportunity if it is dirty.
3. Ribbon JavaScript calls `fmi_GetDealApprovalPreview` to resolve financial warnings before opening the app.
4. If warnings exist, the user can Continue or Cancel.
5. The ribbon opens `fmi_requestdealapprovalhost` and passes the Opportunity ID in the web resource `data` parameter.
6. The host web resource embeds the Code App and passes the Opportunity ID through the `DAC_READY` / `DAC_CONTEXT` postMessage handshake.
7. The Code App opens the request review route and displays the preview returned by `fmi_GetDealApprovalPreview`.
8. Submit calls `fmi_SubmitDealApproval`.
9. The Submit plugin creates the Deal Approval parent and one Deal Approval Item snapshot per Opportunity Item.

### Cancellation journey

1. User clicks the Opportunity ribbon `Cancel Approval Request` command.
2. Ribbon JavaScript validates that a current pending Deal Approval exists, asks for confirmation, and calls `fmi_CancelDealApprovalRequest`.
3. The cancellation plugin validates the linked approval is Pending and the caller is the original requestor.
4. The Deal Approval is marked Cancelled.
5. The Opportunity current Deal Approval lookup is cleared.
6. Opportunity current approval status is set by stage:
   - Offer: Not Requested
   - Unauthorised: Pending
   - Authorised: Pending

## Custom APIs

### `fmi_GetPendingDealApprovals`

Used by the pending approval queue.

- Input: none
- Output: JSON payload containing approval summaries
- Plugin: `DealApprovalPreviewPlugin.GetPendingDealApprovals`

### `fmi_GetDealApprovalPreview`

Used by the Opportunity ribbon preflight and the request page.

- Binding: Global/unbound
- Input: `OpportunityId`, Guid, required
- Output: `PreviewJson`, String
- Plugin: `DealApprovalPreviewPlugin.GetDealApprovalPreview`

The preview resolves current Opportunity Item financial context and returns JSON. It remains intentionally server-side because it uses privileged Dataverse reads and must produce the same result as submission.

### `fmi_SubmitDealApproval`

Used by the React request page.

- Input: `OpportunityId`, Guid, required
- Input: `CoordinatorComment`, String, optional
- Output: `DealApprovalId`, Guid
- Output: `ItemCount`, Integer
- Plugin: `DealApprovalPreviewPlugin.SubmitDealApproval`

Submission remains server-side. The plugin re-resolves financial data, enforces the below-forecast comment rule, routes the approver, creates the parent/child records, and updates the Opportunity.

### `fmi_ProcessDealApprovalDecision`

Used by the React approver detail page.

- Input: `fmi_DealApprovalId`, Guid, required
- Input: `fmi_Decision`, Integer, required (`1` Approve, `2` Reject)
- Input: `fmi_DecisionComment`, String, required by the rejection rule
- Output: `fmi_DealApprovalId`, Guid
- Output: `fmi_ApprovalStatus`, Integer
- Plugin: `DealApprovalPreviewPlugin.ProcessDealApprovalDecision`

### `fmi_CancelDealApprovalRequest`

Used by the Opportunity ribbon cancellation command.

- Binding: Global/unbound
- Input: `OpportunityId`, Guid, required
- Output: none
- Plugin: `DealApprovalPreviewPlugin.CancelDealApprovalRequest`

The plugin is in package version `1.0.5`.

## Financial Rules

The shared server-side resolver uses each Opportunity Item's:

- Content
- Target Territory
- Business Written Year
- Actual Revenue Base

Resolution then follows:

1. Content maps to one Business Written Group through the many-to-many relationship `fmi_fmi_businesswrittengroup_fmi_content`.
2. Target Territory maps to one Business Written Territory.
3. A single `goal` record is matched using:
   - Business Written Group
   - Business Written Territory
   - Business Written Year
4. Budget fields are read from the Goal record:
   - `fmi_currentyearbudget`
   - `fmi_fc1`
   - `fmi_fc2`
   - `fmi_fc3`

Latest forecast selection:

```text
FC3 if non-null
otherwise FC2 if non-null
otherwise FC1 if non-null
otherwise 0
```

Zero is a populated value. Only null means blank.

Calculations:

```text
Variance to Forecast = Sale - Latest Forecast
Variance to Budget   = Sale - Current Year Budget
Below Forecast       = Sale < Latest Forecast, when an FC field is populated
```

Missing configuration is represented as a warning rather than silently treated as below forecast. The detail UI displays `N/A` and explains either:

- No budget record found for the BWG, territory, and BWY combination.
- Variance excluded for this Business Written Group.

## Historic Budget Context

The approval detail page can show `View historic budgets` for an item when qualifying Goal records exist.

Qualification:

- Same Business Written Group.
- Same Business Written Territory.
- Earlier Business Written Year than the current item.
- At least one of Current Year Budget, FC1, FC2, or FC3 is non-null and non-zero.

The history is loaded through the registered `goals` Dataverse data source using the Power Apps data client. It opens in a modal and displays BWY, Budget, FC1, FC2, and FC3.

## Approval Type And Version Rules

The Submit plugin derives `fmi_approvaltype` from the Opportunity `fmi_stage` option set:

```text
Offer         797300003 -> 1 Initial Approval
Unauthorised  797300000 -> 2 Reapproval - Pre-Authorisation
Authorised    797300001 -> 3 Reapproval - Post-Authorisation
```

The plugin also populates `fmi_approvalversion`:

```text
No previous approval       -> 1
Previous version N         -> N + 1
```

The version is written both during parent construction and in an explicit post-create update. This field already exists on `fmi_dealapproval` as an Integer.

## UI And Navigation

The app uses a hash router with these primary routes:

```text
/                       Pending approvals
/approvals/:approvalId  Approval detail
/opportunity-history    Contract ID approval history
/request-deal-approval  Opportunity request review
```

The shared top header appears on the pending approvals and approval history views. It contains:

- Deal Approval Centre title.
- Pending count.
- Pending approvals tab.
- Approval history tab.

Approval history stores the searched contract ID in the URL and restores the search results when returning from an approval detail page.

The request host and app use postMessage for embedded navigation:

```text
DAC_READY          App announces readiness
DAC_CONTEXT        Host sends Opportunity ID
DAC_ACK            App acknowledges context
DAC_CLOSE_REQUEST  App asks host to close the request dialog
```

The host web resource explicitly reopens the originating Opportunity form after Submit, Cancel, or Back. The current deployed host source is `public/DealApprovalCentreHost.html`.

## Plugin Package

Plugin source location:

```text
C:\Users\OstlerW\source\repos\DealApprovalPreviewPlugin
```

Current cancellation package:

```text
C:\Users\OstlerW\source\repos\DealApprovalPreviewPlugin\bin\Release\fmi_DealApprovalPreviewPlugin.1.0.5.nupkg
```

The plugin package contains:

- GetDealApprovalPreview
- GetPendingDealApprovals
- SubmitDealApproval
- ProcessDealApprovalDecision
- CancelDealApprovalRequest
- Supporting guards, resolvers, builders, and calculation helpers

The package has been built and the focused plugin test suite has passed with `75/75` tests.

## Dataverse Registration Dependencies

The implementation partner should verify these are included in the relevant unmanaged solution:

- Custom APIs and request/response components.
- Plugin assembly/package and all required plugin types.
- Opportunity ribbon command definitions.
- Ribbon JavaScript web resources.
- `fmi_requestdealapprovalhost` web resource.
- Code App registration and data sources.
- `goal`/Budgets table data source.

The cancellation API request parameter must use the exact Unique Name:

```text
OpportunityId
```

The Custom API must be bound to:

```text
DealApprovalPreviewPlugin.CancelDealApprovalRequest
```

When command bar behavior appears stale, export and reimport the unmanaged solution containing the table command bar, web resources, and API components, then publish all customizations and refresh the model-driven app.

## Testing Checklist

- Pending queue loads and displays approval summaries.
- Approval detail loads stored item snapshots.
- Missing budget displays N/A and an explanation.
- Historic budget button appears only for qualifying prior Goal rows.
- Historic budget modal opens and closes correctly.
- Contract search loads the full approval history.
- Current Opportunity approval status is prominent on the history page.
- History result -> approval detail -> Back returns to the searched results.
- Opportunity ribbon preview warning appears when financial comparison is unavailable.
- Opportunity request review opens with the correct Opportunity.
- Submit creates one parent and one child per Opportunity Item.
- Approval type is correct for Offer, Unauthorised, and Authorised stages.
- Approval version is populated as 1, 2, 3, etc.
- Approver approve/reject works.
- Cancellation works for Offer, Unauthorised, and Authorised.
- Cancellation resets Opportunity status according to stage.
- Removed commands do not reappear after solution reimport.
