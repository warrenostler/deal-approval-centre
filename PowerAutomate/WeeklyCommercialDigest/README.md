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

The organization-owned `fmi_DigestState` table stores runtime checkpoints. The row named `Weekly Commercial Approval Digest` holds `fmi_LastSuccessfulCutoff`. The flow captures `utcNow()` before querying, uses an exclusive lower bound and inclusive upper bound, then advances the checkpoint to that captured cutoff only after the email action succeeds. Disabled sending, invalid configuration, flow failure, and a run with no approved deals leave the checkpoint unchanged.

The PP FM TEST checkpoint is initially seeded to `2026-09-01T00:00:00Z`, so the first delivery test includes all qualifying approvals after that point. Each environment has independent state.

Digest State is available in the Fremantle CRM sitemap under App Settings > General Settings. The sitemap item requires Read access to `fmi_DigestState`, so users without that table privilege do not see it. PP FM TEST currently grants Digest State privileges only to System Administrator, System Customizer, and built-in Dataverse service roles; normal Deal Approval users have no access.

Local validation: `python PowerAutomate/WeeklyCommercialDigest/test_flow.py`.
