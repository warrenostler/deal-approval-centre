import json
import unittest
from pathlib import Path


FLOW = json.loads(Path(__file__).with_name("clientdata.json").read_text(encoding="utf-8"))
DEFINITION = FLOW["properties"]["definition"]
ACTIONS = DEFINITION["actions"]
GATE = ACTIONS["If_Email_Sending_Allowed"]
STATE_CHECK = GATE["actions"]["If_Digest_State_Valid"]
DEALS_CHECK = STATE_CHECK["actions"]["If_Approved_Deals_Found"]
SEND_BRANCH = DEALS_CHECK["actions"]


def walk(actions, path=""):
    for name, action in actions.items():
        action_path = f"{path}/{name}" if path else name
        yield action_path, action
        yield from walk(action.get("actions", {}), action_path)
        yield from walk(action.get("else", {}).get("actions", {}), f"{action_path}/else")


class WeeklyDigestFlowTests(unittest.TestCase):
    def test_schedule_is_friday_lunchtime_uk_with_single_run_concurrency(self):
        trigger = DEFINITION["triggers"]["Friday_Lunchtime_Recurrence"]
        recurrence = trigger["recurrence"]
        self.assertEqual(recurrence["frequency"], "Week")
        self.assertEqual(recurrence["timeZone"], "GMT Standard Time")
        self.assertEqual(recurrence["schedule"], {"weekDays": ["Friday"], "hours": ["12"], "minutes": [0]})
        self.assertEqual(trigger["runtimeConfiguration"]["concurrency"]["runs"], 1)

    def test_shared_controls_and_weekly_recipient_setting_are_read(self):
        serialized = json.dumps(ACTIONS)
        for schema_name in (
            "fmi_EmailSendingEnabled",
            "fmi_IsProductionEnvironment",
            "fmi_TestEmailRecipient",
            "fmi_WeeklyCommercialDigestRecipients",
        ):
            self.assertIn(schema_name, serialized)
        self.assertIn("equals(outputs('EmailSendingEnabled_Value'), '1')", GATE["expression"])

    def test_query_uses_decision_timestamp_and_bounded_checkpoint_window(self):
        query = STATE_CHECK["actions"]["List_Approved_Deals"]["inputs"]["parameters"]
        self.assertIn("fmi_approvalstatus eq 2", query["$filter"])
        self.assertIn("fmi_decisionon gt ", query["$filter"])
        self.assertIn("fmi_decisionon le ", query["$filter"])
        self.assertIn("Last_Successful_Cutoff_Utc", query["$filter"])
        self.assertIn("Run_Cutoff_Utc", query["$filter"])
        self.assertEqual(query["$orderby"], "fmi_submitteddealvalue desc")

    def test_table_has_requested_deal_fields(self):
        body = SEND_BRANCH["Compose_Email_Body"]["inputs"]
        expected_headers = (
            "<th>Client</th>",
            "<th>Deal</th>",
            "<th>Deal Type</th>",
            "<th>Sales Executive</th>",
            "<th>Deal Value (USD)</th>",
            "<th>Commercially Approved On</th>",
        )
        positions = [body.index(header) for header in expected_headers]
        self.assertEqual(positions, sorted(positions))
        row = SEND_BRANCH["For_each_Approved_Deal"]["actions"]["Append_Approved_Deal_Row"]["inputs"]["value"]
        self.assertIn("fmi_salestype@OData.Community.Display.V1.FormattedValue", row)
        self.assertIn("['fmi_salesexecutive']?['fullname']", row)
        self.assertIn("fmi_decisionon", row)

    def test_test_routing_and_comma_separated_production_recipients(self):
        parameters = SEND_BRANCH["Send_Weekly_Commercial_Digest"]["inputs"]["parameters"]
        self.assertIn("replace(trim(outputs('WeeklyCommercialDigestRecipients_Value')), ',', ';')", parameters["emailMessage/To"])
        self.assertIn("trim(outputs('TestEmailRecipient_Value'))", parameters["emailMessage/To"])
        self.assertIn("'[TEST] '", parameters["emailMessage/Subject"])
        self.assertIn("Production recipients:", parameters["emailMessage/Body"])

    def test_checkpoint_advances_only_after_successful_email(self):
        updates = [
            (path, action)
            for path, action in walk(ACTIONS)
            if isinstance(action.get("inputs"), dict)
            and action["inputs"].get("host", {}).get("operationId") == "UpdateRecord"
        ]
        self.assertEqual([path for path, _ in updates], [
            "If_Email_Sending_Allowed/If_Digest_State_Valid/If_Approved_Deals_Found/Update_Last_Successful_Cutoff"
        ])
        update = updates[0][1]
        self.assertEqual(update["runAfter"], {"Send_Weekly_Commercial_Digest": ["Succeeded"]})
        self.assertEqual(update["inputs"]["parameters"]["item/fmi_lastsuccessfulcutoff"], "@outputs('Run_Cutoff_Utc')")

    def test_only_one_email_action_exists_inside_all_safety_gates(self):
        sends = [
            (path, action)
            for path, action in walk(ACTIONS)
            if isinstance(action.get("inputs"), dict)
            and action["inputs"].get("host", {}).get("operationId") == "SendEmailV2"
        ]
        self.assertEqual([path for path, _ in sends], [
            "If_Email_Sending_Allowed/If_Digest_State_Valid/If_Approved_Deals_Found/Send_Weekly_Commercial_Digest"
        ])


if __name__ == "__main__":
    unittest.main(verbosity=2)
