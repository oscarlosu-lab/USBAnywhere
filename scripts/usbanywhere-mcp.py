#!/usr/bin/env python3
"""
USBAnywhere Model Context Protocol (MCP) Server (JSON-RPC 2.0 over stdio).
Pure Python stdlib implementation exposing safe USB/IP host inspection and control.
"""
import importlib.machinery
import importlib.util
import json
import os
import re
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")

BUSID_RE = re.compile(r"^\d+-[\d.]+$")
CTL_PATH = os.path.join(os.path.dirname(os.path.abspath(__file__)), "usbanywhere-ctl")

TOOLS = [
    {
        "name": "usb_list_devices",
        "description": "List local USB peripherals and their USB/IP export status.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "usb_share_device",
        "description": "Bind and export a local USB device by BusId (e.g. '1-2').",
        "inputSchema": {
            "type": "object",
            "properties": {
                "busid": {"type": "string", "pattern": r"^\d+-[\d.]+$"},
                "reset": {"type": "boolean", "default": False},
            },
            "required": ["busid"],
            "additionalProperties": False,
        },
    },
    {
        "name": "usb_unshare_device",
        "description": "Unbind a shared USB device by BusId.",
        "inputSchema": {
            "type": "object",
            "properties": {"busid": {"type": "string", "pattern": r"^\d+-[\d.]+$"}},
            "required": ["busid"],
            "additionalProperties": False,
        },
    },
    {
        "name": "usb_doctor",
        "description": "Run non-destructive USB/IP server diagnostics and return JSON health report.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "usb_get_policy",
        "description": "Read active USB/IP server access & security policy (tokens redacted).",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
    {
        "name": "usb_create_pairing_code",
        "description": "Generate an ephemeral 6-digit pairing code and UA2 token for client onboarding.",
        "inputSchema": {"type": "object", "properties": {}, "additionalProperties": False},
    },
]


def _load_ctl():
    loader = importlib.machinery.SourceFileLoader("usbanywhere_ctl_mcp", CTL_PATH)
    spec = importlib.util.spec_from_loader("usbanywhere_ctl_mcp", loader)
    mod = importlib.util.module_from_spec(spec)
    loader.exec_module(mod)
    return mod


def call_tool(name: str, arguments: dict | None = None, ctl_mod=None) -> dict:
    args = arguments or {}
    mod = ctl_mod if ctl_mod is not None else _load_ctl()

    if name == "usb_list_devices":
        return {"devices": mod.list_devices()}
    if name == "usb_share_device":
        busid = str(args.get("busid", "")).strip()
        if not BUSID_RE.match(busid):
            raise ValueError(f"Invalid BusId: {busid!r}")
        ok = mod.share_device(busid, reset_device=bool(args.get("reset", False)))
        return {"ok": bool(ok), "busid": busid}
    if name == "usb_unshare_device":
        busid = str(args.get("busid", "")).strip()
        if not BUSID_RE.match(busid):
            raise ValueError(f"Invalid BusId: {busid!r}")
        ok = mod.unshare_device(busid)
        return {"ok": bool(ok), "busid": busid}
    if name == "usb_doctor":
        return mod.collect_doctor_report()
    if name == "usb_get_policy":
        pol = dict(mod.load_policy())
        pol["control_token"] = "***" if pol.get("control_token") else ""
        return {"policy": pol}
    if name == "usb_create_pairing_code":
        code, ttl = mod._PAIRING_MGR.create_pairing_code()
        _, _, fp = mod.ensure_tls_cert()
        ua2 = mod.build_ua2_token(mod.get_lan_ip(), mod.PORT, os.uname().nodename if hasattr(os, "uname") else "host", mod.TLS_PORT, code, fp)
        return {"code": code, "expires_in": ttl, "ua2_token": ua2}
    raise KeyError(f"Unknown tool: {name}")


def handle_rpc_request(req: dict, ctl_mod=None) -> dict | None:
    req_id = req.get("id")
    method = req.get("method", "")
    params = req.get("params") or {}

    if method == "notifications/initialized":
        return None
    if method == "initialize":
        return {
            "jsonrpc": "2.0",
            "id": req_id,
            "result": {
                "protocolVersion": "2024-11-05",
                "serverInfo": {"name": "usbanywhere-mcp", "version": "3.6.0"},
                "capabilities": {"tools": {}},
            },
        }
    if method == "tools/list":
        return {"jsonrpc": "2.0", "id": req_id, "result": {"tools": TOOLS}}
    if method == "tools/call":
        tool_name = params.get("name", "")
        tool_args = params.get("arguments") or {}
        try:
            payload = call_tool(tool_name, tool_args, ctl_mod=ctl_mod)
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {"content": [{"type": "text", "text": json.dumps(payload)}], "isError": False},
            }
        except Exception as e:
            return {
                "jsonrpc": "2.0",
                "id": req_id,
                "result": {"content": [{"type": "text", "text": f"Error: {e}"}], "isError": True},
            }
    return {
        "jsonrpc": "2.0",
        "id": req_id,
        "error": {"code": -32601, "message": f"Method not found: {method}"},
    }


def main():
    for raw_line in sys.stdin:
        line = raw_line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
            resp = handle_rpc_request(req)
        except Exception as e:
            resp = {"jsonrpc": "2.0", "id": None, "error": {"code": -32700, "message": f"Parse error: {e}"}}
        if resp is not None:
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()


if __name__ == "__main__":
    main()
