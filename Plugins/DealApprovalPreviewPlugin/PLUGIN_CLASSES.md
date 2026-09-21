# Deal Approval Plugin Classes

Plain-English guide to the 7 plugin classes in `fmi_DealApprovalPreviewPlugin`. Each one runs on the Dataverse server, not in the browser, so it can be trusted with checks and writes that a user's own security role wouldn't otherwise allow.

## Summary

| Class | Type | Purpose |
|---|---|---|
| [GetPendingDealApprovals](#getpendingdealapprovals) | Custom API | Builds the Approval Centre's "to-do list" of pending approvals |
| [GetDealApprovalPreview](#getdealapprovalpreview) | Custom API | Works out the numbers for a deal before it's submitted |
| [SubmitDealApproval](#submitdealapproval) | Custom API | Locks in a deal approval request and its figures |
| [ProcessDealApprovalDecision](#processdealapprovaldecision) | Custom API | Records an Approve or Reject decision |
| [CancelDealApprovalRequest](#cancledealapprovalrequest) | Custom API | Withdraws a request submitted in error |
| [DealApprovalItemImmutabilityGuard](#dealapprovalitemimmutabilityguard) | Safety guard | Stops anyone editing the submitted figures after the fact |
| [DealApprovalParentLockGuard](#dealapprovalparentlockguard) | Safety guard | Stops a submitted approval record being deleted, reassigned, or deactivated |

---

## GetPendingDealApprovals

**Topline:** Supplies the list of deals waiting for a decision when someone opens the Approval Centre.

**How it works:** When the app asks "what's pending?", this runs on the server and checks who is actually asking. If that person is a normal approver, it only shows deals waiting on them specifically. If they're a member of the "Deal SuperApprover" group, it shows every pending deal across the business, since that group is allowed to decide anything. The list itself is worked out entirely on the server, so there's nothing the app itself has to decide or can get wrong about who sees what.

## GetDealApprovalPreview

**Topline:** Shows a coordinator the financial picture for a deal — sale value against budget and forecast — before they submit it for approval.

**How it works:** Given an Opportunity, it works through the same chain the rest of the system uses: each deal line's content and territory point to a Business Written Group and a matching budget/forecast record for the year. It pulls those numbers together into a preview so the person submitting the deal can see whether it's above or below budget before they commit to sending it for approval. It doesn't change anything — it's read-only.

## SubmitDealApproval

**Topline:** The one place a deal approval request actually gets created.

**How it works:** Rather than trusting whatever figures the app sends over, this re-checks the deal's numbers itself from scratch on the server — the same budget/forecast lookup as the preview — so the figures an approver eventually sees are guaranteed accurate at the moment of submission. It then creates one approval record plus a locked snapshot line for every item in the deal, and marks the Opportunity as "pending approval". Those snapshot lines become the permanent record of exactly what was asked to be approved, which is why they're locked from editing afterwards (see the Immutability Guard below).

## ProcessDealApprovalDecision

**Topline:** Records an approver's Approve or Reject decision.

**How it works:** Before doing anything, it checks that the deal is still actually waiting for a decision (not already decided), and that the person making the call is allowed to — either they're the specific approver assigned to this deal, or they're a member of the "Deal SuperApprover" group who can decide on anyone's behalf. A rejection also requires a comment explaining why. Once those checks pass, it updates the approval record with the decision and updates the Opportunity to match, and both of those updates happen together — if anything goes wrong partway through, neither change is kept, so the two records can never end up out of sync.

## CancelDealApprovalRequest

**Topline:** Lets the person who submitted a request pull it back if it was raised by mistake.

**How it works:** Only works while the request is still pending — once an approver has made a decision, it can no longer be cancelled. It also only lets the original requester cancel their own request, nobody else's. When cancelled, the approval is marked "Cancelled" and the Opportunity is reset back to whatever its approval status should be for its current stage, so the deal returns to a clean state as if the request had never been made.

## DealApprovalItemImmutabilityGuard

**Topline:** A tamper-proof seal on the submitted deal figures.

**How it works:** Once a deal's individual line-item snapshots are created at submission time, nothing and nobody is allowed to edit, delete, reassign, or deactivate them — not even an administrator through the normal UI. This guard blocks every one of those actions outright, no exceptions. It exists because these snapshots are the audit record of exactly what an approver was shown when they made their decision, so they need to stay exactly as they were at that moment, permanently.

## DealApprovalParentLockGuard

**Topline:** The same kind of protection as above, but for the approval record itself rather than its line items.

**How it works:** A submitted approval record can still have its status updated (that's how decisions and cancellations work), but it can never be deleted, handed to a different owner, or switched active/inactive. This guard blocks those specific actions unconditionally, keeping the approval as a permanent, trustworthy record of what happened and when.

---

## The pattern behind all of them

Every one of these plugins re-checks things for itself on the server rather than trusting what the app sends — who's asking, what state a record is in, whether the figures are current. Combined with the two guards blocking edits/deletes on submitted records, the effect is that the approval history in Dataverse should always be an accurate, tamper-resistant record of what was actually asked for and decided, independent of anything happening in the browser.
