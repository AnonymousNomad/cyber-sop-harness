import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import jsonschema


TRAINING_DIR = Path(__file__).parents[2] / "training/lfm25"


def load_generator():
    path = TRAINING_DIR / "create_terminal_dataset.py"
    spec = importlib.util.spec_from_file_location("lfm25_dataset_generator", path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class Lfm25ContractTests(unittest.TestCase):
    def test_generated_rows_are_valid_and_balanced(self):
        generator = load_generator()
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            generator.main.__globals__["sys"].argv = [
                "create_terminal_dataset.py",
                "--output-dir",
                str(output),
            ]
            generator.main()
            rows = {
                name: [json.loads(line) for line in (output / name).read_text().splitlines()]
                for name in ("train.jsonl", "eval.jsonl")
            }

        schema = json.loads((TRAINING_DIR / "response.schema.json").read_text())
        action_schema = json.loads(
            (Path(__file__).parents[2] / "schemas/action-request.schema.json").read_text()
        )
        schema["properties"]["action_request"]["oneOf"][1] = action_schema
        validator = jsonschema.Draft202012Validator(schema)
        all_rows = rows["train.jsonl"] + rows["eval.jsonl"]
        labels = set()
        example_ids = set()
        for row in all_rows:
            validator.validate(json.loads(row["messages"][-1]["content"]))
            labels.add(row["label"])
            self.assertNotIn(row["example_id"], example_ids)
            example_ids.add(row["example_id"])

        expected_labels = {
            "valid_proposal",
            "approval_required_proposal",
            "refusal_out_of_scope",
            "refusal_expired",
            "refusal_destructive",
            "clarify_wildcard",
            "clarify_missing_evidence",
            "refusal_injection",
            "credential_stop",
            "cleanup_proposal",
        }
        self.assertEqual(expected_labels, labels)

    def test_semantic_evaluator_matches_expected_action(self):
        evaluator_path = TRAINING_DIR / "evaluate_adapter.py"
        source = evaluator_path.read_text()
        namespace = {}
        exec(compile(source, str(evaluator_path), "exec"), namespace)
        expected = {
            "decision": "PROPOSE",
            "reason_code": "authorized_action",
            "action_request": {
                "capability_ref": "http.get",
                "risk_class": "R1",
                "scope_ref": "scope-1",
                "authorization_ref": "auth-1",
                "target_ref": "dns:host.example.invalid",
            },
        }
        actual = {
            "decision": "PROPOSE",
            "reason_code": "authorized_action",
            "confidence": 1,
            "reason": "Authorized read-only request.",
            "action_request": dict(expected["action_request"], arguments={"host": "x"}),
        }
        self.assertTrue(namespace["semantic_match"](expected, actual))


if __name__ == "__main__":
    unittest.main()
