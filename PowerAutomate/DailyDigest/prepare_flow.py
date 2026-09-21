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
path.write_text(json.dumps(flow, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print("Prepared Daily Digest: shared settings gate and non-production email redirection.")
