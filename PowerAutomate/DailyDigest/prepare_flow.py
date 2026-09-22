"""Add shared email settings to an exported Daily Digest clientdata document."""
import copy
import json
from pathlib import Path

path = Path(__file__).with_name("clientdata.json")
flow = json.loads(path.read_text(encoding="utf-8-sig"))
actions = flow["properties"]["definition"]["actions"]
if "If_Email_Sending_Allowed" in actions:
    raise SystemExit("Email routing already exists; refusing to apply twice.")

original_send = actions["If_Any_Pending_Approvals"]["actions"]["For_each_Recipient"]["actions"]["Send_an_email_V2"]
list_template = copy.deepcopy(actions["List_Pending_Deal_Approvals"])
previous = "Initialize_Email_Rows_Html"
for setting in ("EmailSendingEnabled", "IsProductionEnvironment", "TestEmailRecipient"):
    read_name = f"Read_{setting}"
    row_name = f"{setting}_Row"
    value_name = f"{setting}_Value"
    read = copy.deepcopy(list_template)
    read.pop("metadata", None)
    read["runAfter"] = {previous: ["Succeeded"]}
    read["inputs"]["parameters"] = {
        "entityName": "environmentvariabledefinitions",
        # At most two rows are needed to detect ambiguous current values.
        "fetchXml": (
            '<fetch top="2"><entity name="environmentvariabledefinition">'
            '<attribute name="defaultvalue"/>'
            '<filter><condition attribute="schemaname" operator="eq" '
            f'value="fmi_{setting}"/></filter>'
            '<link-entity name="environmentvariablevalue" '
            'from="environmentvariabledefinitionid" to="environmentvariabledefinitionid" '
            'link-type="outer" alias="current">'
            '<attribute name="environmentvariablevalueid"/><attribute name="value"/>'
            '<filter><condition attribute="statecode" operator="eq" value="0"/></filter>'
            '</link-entity></entity></fetch>'
        ),
    }
    actions[read_name] = read
    actions[row_name] = {
        "type": "Compose", "runAfter": {read_name: ["Succeeded"]},
        "inputs": f"@first(union(coalesce(body('{read_name}')?['value'], json('[]')), createArray(json('{{}}'))))",
    }
    # A present but blank current value overrides a populated default.
    actions[value_name] = {
        "type": "Compose", "runAfter": {row_name: ["Succeeded"]},
        "inputs": (
            f"@if(equals(length(body('{read_name}')?['value']), 1), "
            f"if(contains(outputs('{row_name}'), 'current.environmentvariablevalueid'), "
            f"coalesce(outputs('{row_name}')?['current.value'], ''), "
            f"coalesce(outputs('{row_name}')?['defaultvalue'], '')), '')"
        ),
    }
    previous = value_name

actions["Is_Production"] = {
    "type": "Compose", "runAfter": {previous: ["Succeeded"]},
    "inputs": "@equals(outputs('IsProductionEnvironment_Value'), '1')",
}
pending = actions.pop("List_Pending_Deal_Approvals")
pending["runAfter"] = {}
digest = actions.pop("If_Any_Pending_Approvals")
actions["If_Email_Sending_Allowed"] = {
    "type": "If", "runAfter": {"Is_Production": ["Succeeded"]},
    "expression": "@and(equals(outputs('EmailSendingEnabled_Value'), '1'), or(outputs('Is_Production'), not(empty(trim(outputs('TestEmailRecipient_Value'))))))",
    "actions": {"List_Pending_Deal_Approvals": pending, "If_Any_Pending_Approvals": digest},
    "else": {"actions": {"Digest_Skipped": {
        "type": "Compose", "runAfter": {},
        "inputs": "Digest skipped: email sending is disabled or the non-production test recipient is missing.",
    }}},
}
parameters = original_send["inputs"]["parameters"]
parameters["emailMessage/To"] = "@if(outputs('Is_Production'), body('Get_Recipient')?['internalemailaddress'], trim(outputs('TestEmailRecipient_Value')))"
parameters["emailMessage/Subject"] = parameters["emailMessage/Subject"].replace(
    "@concat(", "@concat(if(outputs('Is_Production'), '', '[TEST] '), ", 1
)
parameters["emailMessage/Body"] = (
    "@if(outputs('Is_Production'), outputs('Compose_Email_Body'), "
    "concat('<p><strong>TEST EMAIL</strong> — Intended recipient: ', "
    "replace(replace(replace(coalesce(body('Get_Recipient')?['internalemailaddress'], 'Not supplied'), '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), "
    "'</p>', outputs('Compose_Email_Body')))"
)
recipient_actions = digest["actions"]["For_each_Recipient"]["actions"]
approval_query = recipient_actions["List_My_Pending_Approvals"]["inputs"]["parameters"]
approval_query["$expand"] = (
    "fmi_Opportunity($select=opportunityid,name,fmi_salestype;"
    "$expand=fmi_salesexecutive($select=fullname)),"
    "fmi_SubmittedCompany($select=accountid,name)"
)
approval_query["$orderby"] = "fmi_submitteddealvalue desc"
recipient_actions["For_each_My_Approval"]["actions"]["Append_to_string_variable"]["inputs"]["value"] = """@concat(
  '<tr><td>',
  coalesce(items('For_each_My_Approval')?['fmi_SubmittedCompany']?['name'], 'Not supplied'),
  '</td><td>',
  coalesce(items('For_each_My_Approval')?['fmi_Opportunity']?['name'], 'Unnamed opportunity'),
  '</td><td>',
  coalesce(items('For_each_My_Approval')?['fmi_Opportunity']?['fmi_salestype@OData.Community.Display.V1.FormattedValue'], 'Not supplied'),
  '</td><td>',
  coalesce(items('For_each_My_Approval')?['fmi_Opportunity']?['fmi_salesexecutive']?['fullname'], 'Not supplied'),
  '</td><td style="text-align:right">',
  formatNumber(coalesce(items('For_each_My_Approval')?['fmi_submitteddealvalue'], 0), 'N2'),
  '</td><td style="text-align:center">',
  string(div(sub(ticks(utcNow()), ticks(items('For_each_My_Approval')?['createdon'])), 864000000000)),
  '</td><td style="text-align:center">',
  string(length(body('List_Approval_Items')?['value'])),
  '</td><td style="text-align:center">',
  string(length(body('List_BelowForecast_Items')?['value'])),
  '</td></tr>'
)"""
recipient_actions["Compose_Email_Body"]["inputs"] = """@concat('<p>You have ', string(length(body('List_My_Pending_Approvals')?['value'])), ' Deal Approval(s) awaiting your action.</p><table border="1" cellpadding="6" cellspacing="0" style="border-collapse:collapse;font-family:Segoe UI,Arial,sans-serif;font-size:13px"><tr style="background:#f2f2f2"><th>Client</th><th>Deal</th><th>Deal Type</th><th>Sales Executive</th><th>Deal Value (USD)</th><th>Days Pending Approval</th><th>Items</th><th>Below Forecast</th></tr>', variables('EmailRowsHtml'), '</table><p style="margin-top:16px"><a href="', variables('ApprovalCentreUrl'), '">Open the Approval Centre</a> to review and action these approvals.</p>')"""
operation_ids = {
    "Read_EmailSendingEnabled": "244dc15a-6185-47af-be81-87177a09472c",
    "EmailSendingEnabled_Row": "3131cfcb-20b7-428a-a12b-6c3b378efadc",
    "EmailSendingEnabled_Value": "e1306b90-48ae-4e30-8db2-ed3508e1bc8e",
    "Read_IsProductionEnvironment": "112dc678-f144-43a7-9f33-157d097f25e7",
    "IsProductionEnvironment_Row": "1713a2cd-1509-4d20-bb72-d21133a1709d",
    "IsProductionEnvironment_Value": "4f4667e0-a986-4742-8a01-5c662ab43764",
    "Read_TestEmailRecipient": "72335239-4e24-4181-85d5-1cf24b9d8891",
    "TestEmailRecipient_Row": "10737b79-179c-4857-889b-6de1f294b856",
    "TestEmailRecipient_Value": "daf45c6b-e601-4a8a-827e-182b67c0170f",
    "Is_Production": "d49b9e32-a7e1-4ba6-9ad3-8988e9efbf66",
    "If_Email_Sending_Allowed": "d23f14a8-6de7-4b3e-9b60-7877c81ef475",
}
for action_name, operation_id in operation_ids.items():
    actions[action_name]["metadata"] = {"operationMetadataId": operation_id}
path.write_text(json.dumps(flow, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("Prepared Daily Digest: shared settings gate and non-production email redirection.")
