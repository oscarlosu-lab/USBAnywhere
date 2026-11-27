#!/usr/bin/env python3
"""Unit tests for USBAnywhere MCP JSON-RPC 2.0 server."""
import json
import os
import sys
import unittest
from unittest import mock

SCRIPTS_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
if SCRIPTS_DIR not in sys.path:
    sys.path.insert(0, SCRIPTS_DIR)

import importlib.machinery
import importlib.util


def _load_mcp():
    path = os.path.join(SCRIPTS_DIR, "usbanywhere-mcp.py")
    loader = importlib.machinery.SourceFileLoader("usbanywhere_mcp_test", path)
    spec = importlib.util.spec_from_loader("usbanywhere_mcp_test", loader)
    mod = importlib.util.module_from_spec(spec)
    loader.exec_module(mod)
    return mod


class TestMcpServer(unittest.TestCase):
    def test_initialize_and_tools_list(self):
        mcp = _load_mcp()
        init_resp = mcp.handle_rpc_request({"jsonrpc": "2.0", "id": 1, "method": "initialize"})
        self.assertEqual(init_resp["result"]["serverInfo"]["name"], "usbanywhere-mcp")

        list_resp = mcp.handle_rpc_request({"jsonrpc": "2.0", "id": 2, "method": "tools/list"})
        names = {t["name"] for t in list_resp["result"]["tools"]}
        self.assertEqual(len(names), 6)
        self.assertIn("usb_doctor", names)
        self.assertIn("usb_create_pairing_code", names)

    def test_tools_call_and_busid_validation(self):
        mcp = _load_mcp()
        fake_ctl = mock.Mock()
        fake_ctl.collect_doctor_report.return_value = {"healthy": True, "os": "Linux"}
        resp = mcp.handle_rpc_request(
            {"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": "usb_doctor", "arguments": {}}},
            ctl_mod=fake_ctl,
        )
        self.assertFalse(resp["result"]["isError"])
        self.assertTrue(json.loads(resp["result"]["content"][0]["text"])["healthy"])

        bad = mcp.handle_rpc_request(
            {"jsonrpc": "2.0", "id": 4, "method": "tools/call", "params": {"name": "usb_share_device", "arguments": {"busid": "../etc/passwd"}}},
            ctl_mod=fake_ctl,
        )
        self.assertTrue(bad["result"]["isError"])
        self.assertIn("Invalid BusId", bad["result"]["content"][0]["text"])


if __name__ == "__main__":
    unittest.main()
