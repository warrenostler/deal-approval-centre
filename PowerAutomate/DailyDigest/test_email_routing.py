"""Exercise the exported flow's actual routing expressions with mocked connector data.

This bounded expression interpreter does not replace a Power Automate run. It tests
email routing without invoking Outlook or contacting any real recipient.
"""
import json
import re
import unittest
from pathlib import Path

FLOW = json.loads(Path(__file__).with_name("clientdata.json").read_text(encoding="utf-8"))
ACTIONS = FLOW["properties"]["definition"]["actions"]
GATE = ACTIONS["If_Email_Sending_Allowed"]
SEND = GATE["actions"]["If_Any_Pending_Approvals"]["actions"]["For_each_Recipient"]["actions"]["Send_an_email_V2"]["inputs"]["parameters"]
TOKEN = re.compile(r"\s*('(?:[^']|'')*'|[A-Za-z_]\w*|\d+|\?\[|[(),\]])")


def evaluate(expression, bodies, outputs):
    if not isinstance(expression, str) or not expression.startswith("@"):
        return expression
    source = expression[1:]
    tokens, position = [], 0
    while position < len(source):
        match = TOKEN.match(source, position)
        if not match:
            raise ValueError(f"Unsupported expression syntax: {source[position:]}")
        tokens.append(match.group(1))
        position = match.end()
    index = 0

    def parse():
        nonlocal index
        token = tokens[index]
        index += 1
        if token.startswith("'"):
            value = token[1:-1].replace("''", "'")
        elif token.isdigit():
            value = int(token)
        else:
            assert tokens[index] == "("
            index += 1
            args = []
            while tokens[index] != ")":
                args.append(parse())
                if tokens[index] != ")":
                    assert tokens[index] == ","
                    index += 1
            index += 1
            funcs = {
                "body": lambda name: bodies[name],
                "outputs": lambda name: outputs[name],
                "json": json.loads,
                "createArray": lambda *items: list(items),
                "union": lambda *arrays: list({json.dumps(item, sort_keys=True): item for array in arrays for item in array}.values()),
                "first": lambda array: array[0],
                "coalesce": lambda *items: next((item for item in items if item is not None), None),
                "length": len,
                "contains": lambda container, item: item in container,
                "equals": lambda left, right: type(left) is type(right) and left == right,
                "if": lambda condition, yes, no: yes if condition else no,
                "and": lambda *conditions: all(conditions),
                "or": lambda *conditions: any(conditions),
                "not": lambda condition: not condition,
                "empty": lambda value: value is None or len(value) == 0,
                "trim": lambda value: value.strip(),
                "concat": lambda *items: "".join(str(item) for item in items),
                "string": str,
                "replace": lambda value, old, new: value.replace(old, new),
            }
            value = funcs[token](*args)
        while index < len(tokens) and tokens[index] == "?[":
            index += 1
            key = parse()
            assert tokens[index] == "]"
            index += 1
            value = value.get(key) if isinstance(value, dict) else None
        return value

    result = parse()
    assert index == len(tokens)
    return result


def rows(value):
    return [] if value is None else [{"current.environmentvariablevalueid": "mock-id", "current.value": value}]


def route(enabled="1", production="0", recipient="tester@example.com", overrides=None):
    bodies = {
        "Read_EmailSendingEnabled": {"value": rows(enabled)},
        "Read_IsProductionEnvironment": {"value": rows(production)},
        "Read_TestEmailRecipient": {"value": rows(recipient)},
        "Get_Recipient": {"internalemailaddress": "approver@example.com"},
        "List_My_Pending_Approvals": {"value": [{}, {}]},
    }
    bodies.update(overrides or {})
    outputs = {"Compose_Email_Body": "<p>Original digest content</p>"}
    for name, action in ACTIONS.items():
        if action["type"] == "Compose":
            outputs[name] = evaluate(action["inputs"], bodies, outputs)
    if not evaluate(GATE["expression"], bodies, outputs):
        return None
    return {key: evaluate(value, bodies, outputs) for key, value in SEND.items()}


class RoutingTests(unittest.TestCase):
    def test_disabled_missing_or_invalid_switch_never_sends(self):
        for value in ("0", "", None, "true", "2", " 1 "):
            for production in ("0", "1"):
                with self.subTest(value=value, production=production):
                    self.assertIsNone(route(enabled=value, production=production))

    def test_production_preserves_intended_recipient_and_content(self):
        result = route(production="1", recipient=None)
        self.assertEqual(result["emailMessage/To"], "approver@example.com")
        self.assertEqual(result["emailMessage/Subject"], "Deal Approvals awaiting your action (2)")
        self.assertEqual(result["emailMessage/Body"], "<p>Original digest content</p>")

    def test_every_nonproduction_value_redirects_and_labels(self):
        for production in ("0", "", None, "invalid", "true", " 1 "):
            with self.subTest(production=production):
                result = route(production=production, recipient=" tester@example.com ")
                self.assertEqual(result["emailMessage/To"], "tester@example.com")
                self.assertTrue(result["emailMessage/Subject"].startswith("[TEST] "))
                self.assertIn("approver@example.com", result["emailMessage/Body"])
                self.assertIn("Original digest content", result["emailMessage/Body"])
                self.assertNotIn("emailMessage/Cc", result)
                self.assertNotIn("emailMessage/Bcc", result)

    def test_nonproduction_without_test_recipient_never_sends(self):
        for recipient in (None, "", "   "):
            self.assertIsNone(route(recipient=recipient))

    def test_default_is_used_only_when_no_current_value_exists(self):
        self.assertIsNotNone(route(overrides={"Read_EmailSendingEnabled": {"value": [{"defaultvalue": "1"}]}}))
        for current in (None, "", "0"):
            self.assertIsNone(route(overrides={"Read_EmailSendingEnabled": {"value": [{
                "defaultvalue": "1", "current.environmentvariablevalueid": "mock-id", "current.value": current,
            }]}}))

    def test_ambiguous_current_values_do_not_enable_sending(self):
        self.assertIsNone(route(overrides={"Read_EmailSendingEnabled": {"value": rows("1") * 2}}))
        self.assertIsNone(route(overrides={"Read_TestEmailRecipient": {"value": rows("tester@example.com") * 2}}))
        result = route(overrides={"Read_IsProductionEnvironment": {"value": rows("1") * 2}})
        self.assertEqual(result["emailMessage/To"], "tester@example.com")

    def test_every_email_is_inside_the_enabled_branch_and_dependencies_exist(self):
        found = []

        def visit(actions, allowed=False):
            for name, action in actions.items():
                self.assertTrue(set(action.get("runAfter", {})).issubset(actions), name)
                inputs = action.get("inputs", {})
                if isinstance(inputs, dict) and inputs.get("host", {}).get("operationId") == "SendEmailV2":
                    found.append(name)
                    self.assertTrue(allowed, name)
                visit(action.get("actions", {}), allowed or name == "If_Email_Sending_Allowed")
                visit(action.get("else", {}).get("actions", {}), allowed)

        visit(ACTIONS)
        self.assertEqual(found, ["Send_an_email_V2"])
        self.assertNotIn("List_Pending_Deal_Approvals", ACTIONS)

    def test_digest_query_orders_value_descending_and_expands_new_columns(self):
        recipient_actions = GATE["actions"]["If_Any_Pending_Approvals"]["actions"]["For_each_Recipient"]["actions"]
        query = recipient_actions["List_My_Pending_Approvals"]["inputs"]["parameters"]
        self.assertEqual(query["$orderby"], "fmi_submitteddealvalue desc")
        self.assertIn("fmi_salestype", query["$expand"])
        self.assertIn("fmi_salesexecutive($select=fullname)", query["$expand"])

    def test_digest_table_contains_requested_headers_and_row_values(self):
        recipient_actions = GATE["actions"]["If_Any_Pending_Approvals"]["actions"]["For_each_Recipient"]["actions"]
        headers = recipient_actions["Compose_Email_Body"]["inputs"]
        expected = (
            "<th>Company</th><th>Deal</th><th>Deal Type</th><th>Sales Executive</th>"
            "<th>Deal Value (USD)</th><th>Days Pending Approval</th><th>Items</th><th>Below Forecast</th>"
        )
        self.assertIn(expected, headers)
        row = recipient_actions["For_each_My_Approval"]["actions"]["Append_to_string_variable"]["inputs"]["value"]
        self.assertIn("fmi_salestype@OData.Community.Display.V1.FormattedValue", row)
        self.assertIn("['fmi_salesexecutive']?['fullname']", row)
        self.assertNotIn(" day(s)", row)


if __name__ == "__main__":
    unittest.main(verbosity=2)
