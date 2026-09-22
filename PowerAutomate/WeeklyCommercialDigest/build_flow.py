"""Build the solution-aware Weekly Commercial Approval Digest flow definition."""

import json
import uuid
from pathlib import Path


HERE = Path(__file__).parent


_metadata_counter = 0


def metadata():
    global _metadata_counter
    _metadata_counter += 1
    operation_id = uuid.uuid5(
        uuid.NAMESPACE_URL,
        f"fmi:weekly-commercial-digest:operation:{_metadata_counter}",
    )
    return {"operationMetadataId": str(operation_id)}


def dataverse_action(operation, parameters, run_after):
    return {
        "runAfter": run_after,
        "metadata": metadata(),
        "type": "OpenApiConnection",
        "inputs": {
            "host": {
                "connectionName": "shared_commondataserviceforapps",
                "operationId": operation,
                "apiId": "/providers/Microsoft.PowerApps/apis/shared_commondataserviceforapps",
            },
            "parameters": parameters,
            "authentication": "@parameters('$authentication')",
        },
    }


def compose(value, run_after):
    return {
        "type": "Compose",
        "runAfter": run_after,
        "metadata": metadata(),
        "inputs": value,
    }


def setting_actions(schema_name, action_stem, run_after):
    read_name = f"Read_{action_stem}"
    row_name = f"{action_stem}_Row"
    value_name = f"{action_stem}_Value"
    fetch = (
        '<fetch top="2"><entity name="environmentvariabledefinition">'
        '<attribute name="defaultvalue"/><filter>'
        f'<condition attribute="schemaname" operator="eq" value="{schema_name}"/>'
        '</filter><link-entity name="environmentvariablevalue" '
        'from="environmentvariabledefinitionid" to="environmentvariabledefinitionid" '
        'link-type="outer" alias="current"><attribute name="environmentvariablevalueid"/>'
        '<attribute name="value"/><filter><condition attribute="statecode" operator="eq" value="0"/>'
        '</filter></link-entity></entity></fetch>'
    )
    return {
        read_name: dataverse_action("ListRecords", {"entityName": "environmentvariabledefinitions", "fetchXml": fetch}, run_after),
        row_name: compose(
            f"@first(union(coalesce(body('{read_name}')?['value'], json('[]')), createArray(json('{{}}'))))",
            {read_name: ["Succeeded"]},
        ),
        value_name: compose(
            f"@if(equals(length(body('{read_name}')?['value']), 1), "
            f"if(contains(outputs('{row_name}'), 'current.environmentvariablevalueid'), "
            f"coalesce(outputs('{row_name}')?['current.value'], ''), "
            f"coalesce(outputs('{row_name}')?['defaultvalue'], '')), '')",
            {row_name: ["Succeeded"]},
        ),
    }


actions = {
    "Initialize_Email_Rows_Html": {
        "runAfter": {},
        "metadata": metadata(),
        "type": "InitializeVariable",
        "inputs": {"variables": [{"name": "EmailRowsHtml", "type": "string"}]},
    },
    "Run_Cutoff_Utc": compose("@utcNow()", {"Initialize_Email_Rows_Html": ["Succeeded"]}),
}

previous = "Run_Cutoff_Utc"
for schema, stem in (
    ("fmi_EmailSendingEnabled", "EmailSendingEnabled"),
    ("fmi_IsProductionEnvironment", "IsProductionEnvironment"),
    ("fmi_TestEmailRecipient", "TestEmailRecipient"),
    ("fmi_WeeklyCommercialDigestRecipients", "WeeklyCommercialDigestRecipients"),
):
    new_actions = setting_actions(schema, stem, {previous: ["Succeeded"]})
    actions.update(new_actions)
    previous = f"{stem}_Value"

actions["Is_Production"] = compose(
    "@equals(outputs('IsProductionEnvironment_Value'), '1')",
    {previous: ["Succeeded"]},
)

row_html = """@concat(
  '<tr><td>',
  coalesce(items('For_each_Approved_Deal')?['fmi_SubmittedCompany']?['name'], 'Not supplied'),
  '</td><td>',
  coalesce(items('For_each_Approved_Deal')?['fmi_Opportunity']?['name'], 'Unnamed opportunity'),
  '</td><td>',
  coalesce(items('For_each_Approved_Deal')?['fmi_Opportunity']?['fmi_salestype@OData.Community.Display.V1.FormattedValue'], 'Not supplied'),
  '</td><td>',
  coalesce(items('For_each_Approved_Deal')?['fmi_Opportunity']?['fmi_salesexecutive']?['fullname'], 'Not supplied'),
  '</td><td style="text-align:right">',
  formatNumber(coalesce(items('For_each_Approved_Deal')?['fmi_submitteddealvalue'], 0), 'N2'),
  '</td><td>',
  formatDateTime(items('For_each_Approved_Deal')?['fmi_decisionon'], 'dd MMM yyyy HH:mm', 'en-GB'),
  ' UTC</td></tr>'
)"""

email_body = (
    "@concat('<p>', string(length(body('List_Approved_Deals')?['value'])), "
    "' deal(s) have been commercially approved since the previous successful digest.</p>', "
    "'<p><strong>Reporting period:</strong> ', outputs('Last_Successful_Cutoff_Utc'), ' to ', "
    "outputs('Run_Cutoff_Utc'), ' UTC</p>', "
    "'<table border=\"1\" cellpadding=\"6\" cellspacing=\"0\" "
    "style=\"border-collapse:collapse;font-family:Segoe UI,Arial,sans-serif;font-size:13px\">', "
    "'<tr style=\"background:#f2f2f2\"><th>Client</th><th>Deal</th><th>Deal Type</th>', "
    "'<th>Sales Executive</th><th>Deal Value (USD)</th><th>Commercially Approved On</th></tr>', "
    "variables('EmailRowsHtml'), '</table>')"
)

send_parameters = {
    "emailMessage/To": (
        "@if(outputs('Is_Production'), "
        "replace(trim(outputs('WeeklyCommercialDigestRecipients_Value')), ',', ';'), "
        "trim(outputs('TestEmailRecipient_Value')))"
    ),
    "emailMessage/Subject": (
        "@concat(if(outputs('Is_Production'), '', '[TEST] '), "
        "'Weekly Commercial Approval Digest - ', "
        "string(length(body('List_Approved_Deals')?['value'])), ' deal(s)')"
    ),
    "emailMessage/Body": (
        "@if(outputs('Is_Production'), outputs('Compose_Email_Body'), "
        "concat('<p><strong>Production recipients:</strong> ', "
        "replace(replace(replace(coalesce(outputs('WeeklyCommercialDigestRecipients_Value'), 'Not configured'), '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), "
        "'</p>', outputs('Compose_Email_Body')))"
    ),
    "emailMessage/Importance": "Normal",
}

send_action = {
    "runAfter": {"Compose_Email_Body": ["Succeeded"]},
    "metadata": metadata(),
    "type": "OpenApiConnection",
    "inputs": {
        "host": {
            "connectionName": "shared_office365_1",
            "operationId": "SendEmailV2",
            "apiId": "/providers/Microsoft.PowerApps/apis/shared_office365",
        },
        "parameters": send_parameters,
        "authentication": "@parameters('$authentication')",
    },
}

approved_actions = {
    "Reset_Email_Rows_Html": {
        "runAfter": {},
        "metadata": metadata(),
        "type": "SetVariable",
        "inputs": {"name": "EmailRowsHtml", "value": " "},
    },
    "For_each_Approved_Deal": {
        "foreach": "@body('List_Approved_Deals')?['value']",
        "actions": {
            "Append_Approved_Deal_Row": {
                "type": "AppendToStringVariable",
                "inputs": {"name": "EmailRowsHtml", "value": row_html},
            }
        },
        "runAfter": {"Reset_Email_Rows_Html": ["Succeeded"]},
        "metadata": metadata(),
        "type": "Foreach",
        "runtimeConfiguration": {"concurrency": {"repetitions": 1}},
    },
    "Compose_Email_Body": compose(email_body, {"For_each_Approved_Deal": ["Succeeded"]}),
    "Send_Weekly_Commercial_Digest": send_action,
    "Update_Last_Successful_Cutoff": dataverse_action(
        "UpdateRecord",
        {
            "entityName": "fmi_digeststates",
            "recordId": "@outputs('Digest_State_Row')?['fmi_digeststateid']",
            "item/fmi_lastsuccessfulcutoff": "@outputs('Run_Cutoff_Utc')",
        },
        {"Send_Weekly_Commercial_Digest": ["Succeeded"]},
    ),
}

state_valid_actions = {
    "List_Approved_Deals": dataverse_action(
        "ListRecords",
        {
            "entityName": "fmi_dealapprovals",
            "$select": "fmi_dealapprovalid,fmi_submitteddealvalue,fmi_decisionon",
            "$filter": (
                "@concat('fmi_approvalstatus eq 2 and fmi_decisionon gt ', "
                "outputs('Last_Successful_Cutoff_Utc'), ' and fmi_decisionon le ', "
                "outputs('Run_Cutoff_Utc'))"
            ),
            "$expand": (
                "fmi_Opportunity($select=opportunityid,name,fmi_salestype;"
                "$expand=fmi_salesexecutive($select=fullname)),"
                "fmi_SubmittedCompany($select=accountid,name)"
            ),
            "$orderby": "fmi_submitteddealvalue desc",
            "$top": 5000,
        },
        {},
    ),
    "If_Approved_Deals_Found": {
        "actions": approved_actions,
        "runAfter": {"List_Approved_Deals": ["Succeeded"]},
        "expression": {"greater": ["@length(body('List_Approved_Deals')?['value'])", 0]},
        "metadata": metadata(),
        "type": "If",
        "else": {
            "actions": {
                "No_Approved_Deals": compose(
                    "No approved deals were found. No email was sent and the checkpoint was left unchanged.",
                    {},
                )
            }
        },
    },
}

gate_actions = {
    "Get_Digest_State": dataverse_action(
        "ListRecords",
        {
            "entityName": "fmi_digeststates",
            "$select": "fmi_digeststateid,fmi_name,fmi_lastsuccessfulcutoff",
            "$filter": "fmi_name eq 'Weekly Commercial Approval Digest'",
            "$top": 2,
        },
        {},
    ),
    "Digest_State_Row": compose(
        "@first(union(coalesce(body('Get_Digest_State')?['value'], json('[]')), createArray(json('{}'))))",
        {"Get_Digest_State": ["Succeeded"]},
    ),
    "Last_Successful_Cutoff_Utc": compose(
        "@if(equals(length(body('Get_Digest_State')?['value']), 1), "
        "coalesce(outputs('Digest_State_Row')?['fmi_lastsuccessfulcutoff'], ''), '')",
        {"Digest_State_Row": ["Succeeded"]},
    ),
    "If_Digest_State_Valid": {
        "actions": state_valid_actions,
        "runAfter": {"Last_Successful_Cutoff_Utc": ["Succeeded"]},
        "expression": (
            "@and(equals(length(body('Get_Digest_State')?['value']), 1), "
            "not(empty(outputs('Last_Successful_Cutoff_Utc'))))"
        ),
        "metadata": metadata(),
        "type": "If",
        "else": {
            "actions": {
                "Invalid_Digest_State": compose(
                    "Digest skipped: exactly one initialized Digest State row is required.",
                    {},
                )
            }
        },
    },
}

actions["If_Email_Sending_Allowed"] = {
    "type": "If",
    "runAfter": {"Is_Production": ["Succeeded"]},
    "metadata": metadata(),
    "expression": (
        "@and(equals(outputs('EmailSendingEnabled_Value'), '1'), "
        "if(outputs('Is_Production'), "
        "not(empty(trim(outputs('WeeklyCommercialDigestRecipients_Value')))), "
        "not(empty(trim(outputs('TestEmailRecipient_Value'))))))"
    ),
    "actions": gate_actions,
    "else": {
        "actions": {
            "Digest_Skipped": compose(
                "Digest skipped: email sending is disabled or the required recipient configuration is missing.",
                {},
            )
        }
    },
}

flow = {
    "schemaVersion": "1.0.0.0",
    "properties": {
        "connectionReferences": {
            "shared_commondataserviceforapps": {
                "runtimeSource": "embedded",
                "connection": {"connectionReferenceLogicalName": "fmi_dealapprovaldataverse"},
                "api": {"name": "shared_commondataserviceforapps"},
            },
            "shared_office365_1": {
                "runtimeSource": "embedded",
                "connection": {"connectionReferenceLogicalName": "fmi_sharedoffice365_cbef4"},
                "api": {"name": "shared_office365"},
            },
        },
        "definition": {
            "$schema": "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
            "contentVersion": "1.0.0.0",
            "parameters": {
                "$connections": {"defaultValue": {}, "type": "Object"},
                "$authentication": {"defaultValue": {}, "type": "SecureObject"},
            },
            "triggers": {
                "Friday_Lunchtime_Recurrence": {
                    "recurrence": {
                        "frequency": "Week",
                        "interval": 1,
                        "timeZone": "GMT Standard Time",
                        "schedule": {"weekDays": ["Friday"], "hours": ["12"], "minutes": [0]},
                    },
                    "evaluatedRecurrence": {
                        "frequency": "Week",
                        "interval": 1,
                        "timeZone": "GMT Standard Time",
                        "schedule": {"weekDays": ["Friday"], "hours": ["12"], "minutes": [0]},
                    },
                    "metadata": metadata(),
                    "type": "Recurrence",
                    "runtimeConfiguration": {"concurrency": {"runs": 1}},
                }
            },
            "actions": actions,
            "outputs": {},
        },
        "templateName": "",
    },
}

(HERE / "clientdata.json").write_text(json.dumps(flow, indent=2) + "\n", encoding="utf-8")
print("Built Weekly Commercial Approval Digest clientdata.json")
