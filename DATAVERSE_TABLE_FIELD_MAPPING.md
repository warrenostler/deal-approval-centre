# Deal Approval Centre Dataverse Table And Field Mapping

## Scope

This document lists the Dataverse tables, fields, relationships, and Custom API dependencies required by the Deal Approval Centre React Code App and the companion `DealApprovalPreviewPlugin` package.

The implementation uses two layers:

- React Code App: reads Dataverse records for queue, detail, history, and historic budget display; invokes server-side Custom APIs for preview, submission, and decisions.
- Server-side plugins: perform authoritative financial resolution, approval creation, cancellation, approval decisions, routing, and Opportunity updates.

## Environment

- Environment: FM TEST
- Organization URL: `https://orgf7602101.crm11.dynamics.com`
- Environment ID: `4b002f9f-ad17-e4a8-8582-0970ce77cdae`
- Code App ID: `63bb01cf-9750-4567-889a-60e6e503a6ba`

## Table Summary

| Table | Logical name | Entity set | Primary use |
|---|---|---|---|
| Opportunity | `opportunity` | `opportunities` | Source deal, stage, approval state, current approval link |
| Opportunity Item | `fmi_opportunityitem` | `fmi_opportunityitems` | Deal content lines and sale values |
| Content | `fmi_content` | `fmi_contents` | Title/content identity and BWG relationship |
| Target Territory | `fmi_targetterritory` | `fmi_targetterritories` | Sales territory and BWT mapping |
| Business Written Group | `fmi_businesswrittengroup` | `fmi_businesswrittengroups` | BWG identity and variance inclusion rule |
| Business Written Territory | `fmi_businesswrittenterritory` | Not currently a Code App data source | Financial matching dimension for Goal records |
| Business Written Year | `fmi_businesswrittenyear` | `fmi_businesswrittenyears` | Financial year dimension |
| Budget / Goal | `goal` | `goals` | Current budget and FC1/FC2/FC3 values |
| Deal Approval | `fmi_dealapproval` | `fmi_dealapprovals` | Approval parent, audit history, status, type, version |
| Deal Approval Item | `fmi_dealapprovalitem` | `fmi_dealapprovalitems` | Immutable submitted financial snapshot per Opportunity Item |
| Account | `account` | `accounts` | Submitted company name |
| System User | `systemuser` | `systemusers` | Requestor, approver, decision maker, sales executive |
| Process Stage | `processstage` | Platform table | BPF stage fallback used by Submit plugin |
| Organization | `organization` | Platform table | Base currency resolution during Submit |
| Transaction Currency | `transactioncurrency` | Platform table | Submitted currency and approval item currency |

## Detailed Table Requirements

### Opportunity (`opportunity`)

| Field | Type/use | Required by |
|---|---|---|
| `opportunityid` | Primary key | All Opportunity lookups |
| `name` | Deal name | Preview, history, submit naming, queue enrichment |
| `fmi_dpssalescontractid` | Contract ID | Contract ID history search |
| `fmi_actualrevenue_base` | Money/base sale value | Preview and Submit parent submitted deal value |
| `fmi_stage` | Option set | Submit approval type; cancellation stage-dependent reset |
| `fmi_currentapprovalstatus` | Option set | Submit pending guard; history current status; cancellation state reset |
| `fmi_currentdealapproval` | Lookup to Deal Approval | Submit previous approval link; cancellation target; ribbon state |
| `parentaccountid` | Lookup to Account | Submit company snapshot |
| `_fmi_salesexecutive_value` | Lookup to System User | Queue/detail Sales Executive display |
| `stageid` | Guid, deprecated BPF field | Submit plugin fallback stage resolution |
| `traversedpath` | String, deprecated BPF path | Submit plugin fallback when `stageid` is empty |
| `processid` | Guid, BPF process ID | Available for BPF diagnostics; not currently used in final mapping |

Opportunity stage values used for approval type:

```text
797300003 = Offer
797300000 = Unauthorised
797300001 = Authorised
```

Opportunity approval status values used by the implementation:

```text
1 = Not Requested
2 = Pending
3 = Approved
4 = Rejected
5 = Reapproval Required
```

Cancellation reset rule:

```text
Offer         -> fmi_currentapprovalstatus = 1 (Not Requested)
Unauthorised  -> fmi_currentapprovalstatus = 2 (Pending)
Authorised    -> fmi_currentapprovalstatus = 2 (Pending)
```

### Opportunity Item (`fmi_opportunityitem`)

| Field | Type/use | Required by |
|---|---|---|
| `fmi_opportunityitemid` | Primary key | Item identity and snapshot link |
| `fmi_opportunityid` | Lookup to Opportunity | Retrieve all lines for an Opportunity |
| `fmi_name` | Text | Fallback item name and snapshot name |
| `fmi_content` | Lookup to Content | Content/BWG resolution and display |
| `fmi_targetterritory` | Lookup to Target Territory | Territory/BWT resolution and display |
| `fmi_businesswrittenyear` | Lookup to Business Written Year | Goal matching and historic-year comparison |
| `fmi_actualrevenue_base` | Money/base value | Sale value and variance calculations |
| `fmi_licensestartdate` | Date | Detail display and legacy snapshot text |
| `fmi_licenseenddate` | Date | Detail display and legacy snapshot text |
| `fmi_budget` | Lookup to Goal | Existing Opportunity Item budget relationship; financial resolver currently matches Goal by dimensions instead |
| `statecode` / `statuscode` | State/status | Normal active-item filtering where required |

### Content (`fmi_content`)

| Field/relationship | Type/use | Required by |
|---|---|---|
| `fmi_contentid` | Primary key | Content lookup |
| `fmi_name` | Text | Title display |
| `fmi_fmi_businesswrittengroup_fmi_content` | Many-to-many relationship | Resolve Content to Business Written Group |
| Relationship result | Must resolve to zero or one BWG | More than one BWG is a hard configuration error |

### Target Territory (`fmi_targetterritory`)

| Field | Type/use | Required by |
|---|---|---|
| `fmi_targetterritoryid` | Primary key | Territory lookup |
| `fmi_name` | Text | Territory display and error context |
| `fmi_businesswrittenterritory` | Lookup to Business Written Territory | Goal matching dimension |
| `fmi_defaultapprover` | Lookup to System User | Server-side approver routing |
| `fmi_salesexec` | Lookup to System User | Territory sales executive context |

A missing BWT mapping is treated as financial comparison unavailable rather than silently matching a Goal.

### Business Written Group (`fmi_businesswrittengroup`)

| Field | Type/use | Required by |
|---|---|---|
| `fmi_businesswrittengroupid` | Primary key | BWG identity and Goal matching |
| `fmi_name` | Text | Display and error context |
| `fmi_includeinvariances` | Two-options Boolean | Controls whether variance values are displayed |
| `fmi_businesswrittengrouptype` | Option set | Configuration/reference |

When `fmi_includeinvariances` is false, the Code App displays variance fields as `N/A` and explains that variance is excluded for the BWG.

### Business Written Territory (`fmi_businesswrittenterritory`)

| Field | Type/use | Required by |
|---|---|---|
| `fmi_businesswrittenterritoryid` | Primary key | Goal matching |
| `fmi_name` | Text | History/financial context display and error context |

This table is accessed through the Target Territory lookup and server-side financial resolver. It is a financial matching dimension even though it is not currently registered as a separate React Code App data source.

### Business Written Year (`fmi_businesswrittenyear`)

| Field | Type/use | Required by |
|---|---|---|
| `fmi_businesswrittenyearid` | Primary key | Goal matching and historical comparison |
| `fmi_name` | Text, for example `BWY 2026` or `FY2026` | Display and prior-year filtering |
| `fmi_businesswrittenyear` | Option set/year | Year reference |
| `fmi_year` | Option set/year | Year reference |
| `fmi_budgetstatus` | Option set | Budget configuration reference |
| `fmi_includeinformatbudgetcalculations` | Boolean | Existing financial configuration |
| `fmi_includeintapebudgetcalculations` | Boolean | Existing financial configuration |

### Budget / Goal (`goal`)

The Dataverse table display name is Budgets, while its logical name is `goal` and its entity set is `goals`.

| Field | Type/use | Required by |
|---|---|---|
| `goalid` | Primary key | Goal identity |
| `fmi_businesswrittengroup` | Lookup to BWG | Goal matching dimension |
| `fmi_bwterritory` | Lookup to Business Written Territory | Goal matching dimension |
| `fmi_businesswrittenyear` | Lookup to Business Written Year | Goal matching and previous-year history |
| `fmi_currentyearbudget` | Money | Budget comparison and history |
| `fmi_fc1` | Money | Forecast selection and history |
| `fmi_fc2` | Money | Forecast selection and history |
| `fmi_fc3` | Money | Forecast selection and history |
| `fmi_budgetstatus` | Option set | Budget configuration/reference |
| `fmi_budgettype` | Option set | Budget configuration/reference |
| `fmi_nextyearbudget` | Money | Not currently used by the approval UI |
| `fmi_previousyearbudget` | Money | Not currently used by the approval UI |

Goal matching key:

```text
Business Written Group + Business Written Territory + Business Written Year
```

Latest forecast rule:

```text
FC3 when non-null
otherwise FC2 when non-null
otherwise FC1 when non-null
otherwise 0
```

A Goal row with all FC fields blank is still a valid match; the latest forecast is treated as zero. A missing Goal row produces unavailable financial comparison rather than a hard failure.

Historic budget qualification:

```text
Same BWG
Same BWT
Historical BWY earlier than the current item BWY
At least one of Current Year Budget, FC1, FC2, FC3 is non-null and non-zero
```

### Deal Approval (`fmi_dealapproval`)

| Field | Type/use | Required by |
|---|---|---|
| `fmi_dealapprovalid` | Primary key | Approval identity |
| `fmi_name` | Text | Approval name/audit display |
| `fmi_opportunity` | Lookup to Opportunity | Related history and current approval relationship |
| `fmi_approvalstatus` | Option set | Pending/Approved/Rejected/Cancelled/Failed state |
| `fmi_approvaltype` | Option set | Initial/Reapproval classification |
| `fmi_approvalversion` | Integer | Approval sequence number |
| `fmi_approvalsenton` | DateTime | History timeline |
| `fmi_decisionon` | DateTime | History timeline |
| `fmi_decisioncomments` | Memo | Approver decision comments |
| `fmi_requestorcomment` | Memo | Coordinator request comment |
| `fmi_iscurrentapproval` | Boolean | Current approval reference/audit |
| `fmi_reapprovalrequired` | Boolean | Reapproval state/reference |
| `fmi_previousapproval` | Lookup to Deal Approval | Approval chain and version source |
| `fmi_supersededby` | Lookup to Deal Approval | Approval chain reference |
| `fmi_cancelledon` | DateTime | Cancellation audit |
| `fmi_cancellationreason` | Memo/text | Cancellation audit |
| `fmi_requestedby` | Lookup to System User | Original requester and cancellation authorization |
| `fmi_approver` | Lookup to System User | Assigned approver |
| `fmi_decisionby` | Lookup to System User | Actual decision maker |
| `fmi_submittedcompany` | Lookup to Account | Company snapshot |
| `fmi_submittedcurrency` | Lookup to Transaction Currency | Submission currency |
| `fmi_submitteddealvalue` | Money | Submitted parent deal value |
| `fmi_submitteddealitems` | Memo | Legacy human-readable summary; not source of truth |
| `transactioncurrencyid` | Lookup to Transaction Currency | Money field currency |

Deal Approval status values used by the app/plugin:

```text
1 = Pending
2 = Approved
3 = Rejected
4 = Cancelled
5 = Failed
```

Approval type values:

```text
1 = Initial Approval
2 = Reapproval - Pre-Authorisation
3 = Reapproval - Post-Authorisation
```

Approval type is derived from Opportunity `fmi_stage`:

```text
Offer         -> 1
Unauthorised  -> 2
Authorised    -> 3
```

Approval version is derived from the linked previous approval:

```text
No previous approval -> 1
Previous version N   -> N + 1
```

### Deal Approval Item (`fmi_dealapprovalitem`)

One record is created per Opportunity Item. Items sharing a Goal are not grouped.

| Field | Type/use | Required by |
|---|---|---|
| `fmi_dealapprovalitemid` | Primary key | Snapshot identity |
| `fmi_dealapproval` | Lookup to Deal Approval | Parent-child relationship |
| `fmi_opportunityitem` | Lookup to Opportunity Item | Original line reference |
| `fmi_content` | Lookup to Content | Snapshot context |
| `fmi_targetterritory` | Lookup to Target Territory | Snapshot context |
| `fmi_businesswrittengroup` | Lookup to BWG | Snapshot financial dimension |
| `fmi_businesswrittenterritory` | Lookup to BWT | Snapshot financial dimension |
| `fmi_businesswrittenyear` | Lookup to BWY | Snapshot financial dimension |
| `fmi_name` | Text | Snapshot display name |
| `fmi_submittedsalevalue` | Money | Submitted sale snapshot |
| `fmi_submittedbudgetvalue` | Money | Submitted budget snapshot; null indicates no matched Goal in current implementation |
| `fmi_submittedlatestforecast` | Money | Submitted latest forecast snapshot |
| `fmi_latestforecasttype` | Option set | FC1/FC2/FC3 snapshot |
| `fmi_variancetoforecast` | Money | Sale minus latest forecast |
| `fmi_variancetobudget` | Money | Sale minus current budget |
| `fmi_belowforecast` | Boolean | Below latest forecast flag |
| `transactioncurrencyid` | Lookup to Transaction Currency | Money field currency |

The React detail page adds derived UI flags without changing Dataverse schema:

```text
fmi_nobudgetrecordfound
fmi_varianceexcluded
```

### Account (`account`)

| Field | Type/use | Required by |
|---|---|---|
| `accountid` | Primary key | Company lookup |
| `name` | Text | Approval summary/detail display |

### System User (`systemuser`)

| Field | Type/use | Required by |
|---|---|---|
| `systemuserid` | Primary key | User lookup |
| `fullname` | Text | Requestor, approver, decision maker, Sales Executive display |
| `isdisabled` | Boolean | Server-side approver routing |

### Process Stage (`processstage`)

| Field | Type/use | Required by |
|---|---|---|
| `processstageid` | Primary key | Resolve active BPF stage when available |
| `stagename` | Text | Stage-to-approval-type mapping fallback |

The final Submit implementation primarily uses Opportunity `fmi_stage`, because `stageid` and `traversedpath` were observed to be empty in the target environment. The BPF fields remain fallback diagnostics.

### Organization / Transaction Currency

| Table | Fields | Required by |
|---|---|---|
| `organization` | `basecurrencyid` | Submit base currency resolution |
| `transactioncurrency` | `transactioncurrencyid`, `isocurrencycode` | Approval parent/item currency and legacy text formatting |

## Relationships And Dependency Chain

```text
Opportunity
  -> Opportunity Items
      -> Content
          -> many-to-many Business Written Group
      -> Target Territory
          -> Business Written Territory
      -> Business Written Year

Business Written Group + Business Written Territory + Business Written Year
  -> Goal/Budget

Opportunity + resolved financial rows
  -> Deal Approval
      -> Deal Approval Items (one per Opportunity Item)
```

## React Code App Data Sources

Configured in `power.config.json`:

| Data source name | Entity set | Used for |
|---|---|---|
| `dealapprovals` | `fmi_dealapprovals` | Queue/detail/history |
| `dealapprovalitems` | `fmi_dealapprovalitems` | Approval detail snapshots |
| `companies` | `accounts` | Company names |
| `opportunities` | `opportunities` | Queue enrichment and contract history |
| `users` | `systemusers` | User names |
| `content` | `fmi_contents` | Content names |
| `targetterritories` | `fmi_targetterritories` | Territory names and BWT mapping |
| `businesswrittenyears` | `fmi_businesswrittenyears` | BWY names |
| `opportunityitems` | `fmi_opportunityitems` | Licence dates |
| `businesswrittengroups` | `fmi_businesswrittengroups` | Variance inclusion flag |
| `budgets` | `goals` | Historic budget retrieval |

## Custom API And Plugin Mapping

| Custom API | Plugin | Tables/fields used |
|---|---|---|
| `fmi_GetPendingDealApprovals` | `GetPendingDealApprovals` | Pending Deal Approval summaries and related Opportunity/user data |
| `fmi_GetDealApprovalPreview` | `GetDealApprovalPreview` | Opportunity Items, Content/BWG, Target Territory/BWT, BWY, Goal fields |
| `fmi_SubmitDealApproval` | `SubmitDealApproval` | Opportunity, Opportunity Items, financial dimensions, Goal, Deal Approval, Deal Approval Item, Account, User, Organization, Currency |
| `fmi_ProcessDealApprovalDecision` | `ProcessDealApprovalDecision` | Deal Approval status/approver fields; Opportunity approval status |
| `fmi_CancelDealApprovalRequest` | `CancelDealApprovalRequest` | Opportunity stage/current approval fields; Deal Approval status/requester/cancellation fields |

## Required Solution Components

The implementation partner should include and deploy together:

- The Code App and its configured Dataverse data sources.
- Opportunity and Opportunity Item table customizations.
- Deal Approval and Deal Approval Item table customizations.
- Budget/Goal table access and fields.
- Content, BWG, Target Territory, BWT, and BWY configuration.
- All five Custom APIs and their request/response components.
- Plugin package and all registered plugin types.
- Opportunity ribbon commands and JavaScript web resources.
- `fmi_requestdealapprovalhost` web resource.
- Relevant security roles/privileges for the app connection and plugin execution.

## Deployment Notes

- `npm run power:push` deploys the Code App bundle only.
- It does not update ribbon JavaScript or `fmi_requestdealapprovalhost`.
- Plugin package updates are separate from Code App deployment.
- The cancellation package currently has version `1.0.5`.
- The plugin source/package repository commit for the cancellation package is `573a55d`.
- When command bar metadata appears stale, export/reimport the unmanaged solution containing the table command bar and web resources, publish all customizations, and refresh the model-driven app.

## Verification Checklist

- Every table logical name and entity set is correct in the target environment.
- The `goal` entity set is exposed as `goals` and contains the required financial fields.
- Content-to-BWG relationship resolves correctly and does not produce ambiguous mappings.
- Target Territory-to-BWT mapping is populated.
- Opportunity `fmi_stage` is populated and matches the active business stage.
- Deal Approval `fmi_approvalversion` is writable as Integer.
- All Custom APIs point to the intended plugin types.
- Cancellation API uses request parameter Unique Name `OpportunityId` of type Guid.
- App and plugin identities have the required read/write privileges.
- Ribbon commands reference the published JavaScript web resources.
