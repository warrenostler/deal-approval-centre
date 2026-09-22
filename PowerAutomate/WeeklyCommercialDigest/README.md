# Weekly Commercial Approval Digest

Flow: `Deal Approval - Weekly Commercial Approval Digest` (`7d8cae32-61b6-f111-aaae-7c1e522eebf6`).
Solution: `DealApproval`, environment: PP FM TEST.

The flow is active and runs at 12:00 UK time every Friday with trigger concurrency limited to one run. It sends one table of Deal Approval records whose Approval Status is Approved and whose `Decision On` value is later than the previous successful cutoff and no later than the current run cutoff.

The table contains Client, Deal, Deal Type, Sales Executive, Approved By, Deal Value (USD), and Commercially Approved On. Approved By uses the actual `Decision By` user; a missing historical value displays as `Not supplied`. Approval time is displayed without a trailing timezone label. Rows are ordered by Deal Value from highest to lowest.

## Configuration

The flow reuses these shared Text environment variables:

- `fmi_EmailSendingEnabled`: only the exact value `1` permits email sending.
- `fmi_IsProductionEnvironment`: only the exact value `1` enables production routing.
- `fmi_TestEmailRecipient`: receives redirected messages outside production.

It adds `fmi_WeeklyCommercialDigestRecipients`, a Text environment variable containing comma-separated production email addresses. Before production activation, populate this value with the CEO and any additional recipients.

PP FM TEST currently has email sending enabled, production mode disabled, `warren.ostler@fremantle.com` as the test recipient, and no production weekly recipients configured.

In a non-production environment, the flow sends only to `fmi_TestEmailRecipient`, prefixes the subject with `[TEST]`, and includes the configured production recipient list in the body. In production, a blank weekly recipient list prevents sending.

## Checkpoint

The existing `fmi_SystemProcess` table stores the checkpoint using Process Type `Weekly Commercial Approval Digest` (`797300004`). This follows the table's existing timestamped checkpoint pattern. The latest matching row's `Start Date` is the previous successful cutoff.

The flow captures `utcNow()` before querying, uses an exclusive lower bound and inclusive upper bound, then creates a new successful System Process row with that captured cutoff only after the email action succeeds. The row also records the number of deals included and a short execution log. Business Written Year is intentionally empty because the digest spans commercial approvals rather than one business year. Disabled sending, invalid configuration, flow failure, and a run with no approved deals leave the checkpoint unchanged.

PP FM TEST preserves the previous Digest State cutoff of `2026-09-22T09:02:13Z` in its initial System Process row. Each environment needs one initial row when the flow is first deployed. System Processes are already available in the Fremantle CRM app.

Local validation: `python PowerAutomate/WeeklyCommercialDigest/test_flow.py`.
