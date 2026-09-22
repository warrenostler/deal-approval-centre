# Daily Digest email configuration

Flow: `Deal Approval - Daily Digest` (`6c1394f5-5b06-4329-b520-d5de70ac859e`).
Solution: `DealApproval`, environment: PP FM TEST.

The checked-in `clientdata.json` is the refactored flow definition. The flow is active. Updating the definition did not manually run the flow or send an email.

## Shared environment variables

All three variables are Text variables and can be read by other solution flows using the same pattern.

| Schema name | Default | PP FM TEST current value |
| --- | --- | --- |
| `fmi_EmailSendingEnabled` | `0` | `1` |
| `fmi_IsProductionEnvironment` | `0` | `0` |
| `fmi_TestEmailRecipient` | blank | `warren.ostler@fremantle.com` |

The flow reads these settings directly from Dataverse each run. A single active current value takes precedence over the default, including a blank current value. Missing definitions or ambiguous current values resolve to blank. Only the exact string `1` enables sending or production routing.

If sending is disabled, the flow completes without querying pending approvals or preparing emails. If sending is enabled:

- Production mode `1`: use each approver's original email recipient, subject and body.
- Any other production value: send only to the configured test address, prefix the subject with `[TEST]`, and prepend the intended recipient to the body.
- No test recipient outside production: skip the digest. There is no fallback to real approvers.

The schedule remains daily at 08:00 UK time. Existing queries, digest content, loops, connections and the Approval Centre URL are preserved. The URL still points to PP FM TEST and must be configured appropriately before production use.

The digest table shows Company, Deal, Deal Type, Sales Executive, Deal Value (USD), Days Pending Approval, Items and Below Forecast. Rows are ordered by Deal Value from highest to lowest for each recipient.

## Testing

For an actual delivery test, keep `fmi_IsProductionEnvironment` at `0`, confirm the test recipient, then set `fmi_EmailSendingEnabled` to `1` and enable/run the flow. Each approver's digest will be redirected to the test mailbox, so multiple test messages can arrive. Return sending to `0` when finished. The shared sending switch affects any other flows that adopt it.

Local validation: `python PowerAutomate/DailyDigest/test_email_routing.py`.

The tests interpret the actual exported routing expressions using mocked connector results. They cover production and test destinations, disabled/missing/invalid settings, blank and duplicate current values, subject/body labelling, and the placement of every email action behind the sending condition. This is not an actual Power Automate execution or email-delivery test. The three FetchXML queries were also verified against Dataverse, and the saved flow was read back and compared structurally with the prepared definition.

`prepare_flow.py` is the one-time transformation used on the original export. It deliberately refuses to run again on an already-refactored definition.

The original live workflow record was backed up locally to `%TEMP%\dac-daily-digest-before-email-routing.json` before the email-routing update. A second backup was saved to `%TEMP%\dac-daily-digest-before-table-columns-20260922.json` before the table update.

API reference: https://learn.microsoft.com/en-us/power-automate/manage-flows-with-code
