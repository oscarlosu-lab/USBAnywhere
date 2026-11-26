#!/usr/bin/env python3
"""
USBAnywhere Control Suite - Automated Unit & Integration Tests
Validates the unified usbanywhere-ctl management tool across macOS and Linux.
"""
import os
import sys
import subprocess
import json
import unittest

import importlib.machinery
import importlib.util
from unittest import mock

SCRIPT_PATH = os.path.join(os.path.dirname(__file__), "..", "usbanywhere-ctl")


def _load_ctl_module():
    loader = importlib.machinery.SourceFileLoader("usbanywhere_ctl_mod", SCRIPT_PATH)
    spec = importlib.util.spec_from_loader("usbanywhere_ctl_mod", loader)
    mod = importlib.util.module_from_spec(spec)
    loader.exec_module(mod)
    return mod

class TestControlSuite(unittest.TestCase):

    def test_script_exists_and_is_executable(self):
        self.assertTrue(os.path.exists(SCRIPT_PATH), f"usbanywhere-ctl not found at {SCRIPT_PATH}")

    def test_status_json_output(self):
        res = subprocess.run([sys.executable, SCRIPT_PATH, "status", "--json"],
                             capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(res.returncode, 0, f"Error running status: {res.stderr}")
        data = json.loads(res.stdout)
        self.assertIn("os", data)
        self.assertIn("service_manager", data)
        self.assertIn("port_active", data)
        self.assertIn("lan_ip", data)
        self.assertIn("devices", data)
        self.assertIsInstance(data["devices"], list)

    def test_doctor_execution(self):
        res = subprocess.run([sys.executable, SCRIPT_PATH, "doctor"],
                             capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(res.returncode, 0, f"Doctor exited with error: {res.stderr}")
        self.assertIn("USBAnywhere System Doctor", res.stdout)
        self.assertIn("Operating System:", res.stdout)

    def test_list_devices(self):
        res = subprocess.run([sys.executable, SCRIPT_PATH, "list"],
                             capture_output=True, text=True, encoding="utf-8")
        self.assertEqual(res.returncode, 0, f"List devices failed: {res.stderr}")
        self.assertIn("Discovered USB Peripherals", res.stdout)

    def test_share_device_linux_fails_when_device_absent(self):
        mod = _load_ctl_module()
        with mock.patch.object(mod.os.path, "exists", return_value=False), \
             mock.patch.object(mod, "_wait_udev_settle"):
            ok = mod._share_device_linux("1-1.3", "/usr/sbin/usbip")
        self.assertFalse(ok)
        self.assertIn("not found on USB bus", mod._last_bind_error.get("1-1.3", ""))

    def test_busid_lock_per_device_serialization(self):
        mod = _load_ctl_module()
        l1 = mod._get_busid_lock("1-1.3")
        l1_again = mod._get_busid_lock("1-1.3")
        l2 = mod._get_busid_lock("1-1.4")
        self.assertIs(l1, l1_again)
        self.assertIsNot(l1, l2)

    def test_sudo_rerun_uses_explicit_subcommand(self):
        mod = _load_ctl_module()
        cmd = mod.build_sudo_command("connect", "192.168.1.2", "1-1")
        self.assertEqual(cmd[0], "sudo")
        self.assertEqual(cmd[1], sys.executable)
        self.assertEqual(os.path.realpath(cmd[2]), os.path.realpath(SCRIPT_PATH))
        self.assertEqual(cmd[3:], ["connect", "192.168.1.2", "1-1"])

    def test_rerun_with_sudo_has_no_timeout_and_handles_ctrl_c(self):
        mod = _load_ctl_module()
        with mock.patch.object(mod.shutil, "which", return_value="/usr/bin/sudo"), \
             mock.patch.object(mod.subprocess, "run", return_value=mock.Mock(returncode=0)) as run:
            self.assertTrue(mod.rerun_with_sudo("start"))
        self.assertNotIn("timeout", run.call_args.kwargs)
        with mock.patch.object(mod.shutil, "which", return_value="/usr/bin/sudo"), \
             mock.patch.object(mod.subprocess, "run", side_effect=KeyboardInterrupt):
            self.assertFalse(mod.rerun_with_sudo("start"))
        with mock.patch.object(mod.shutil, "which", return_value=None):
            self.assertFalse(mod.rerun_with_sudo("start"))

    def test_privileged_actions_rerun_with_their_own_arguments(self):
        mod = _load_ctl_module()
        cases = [
            (lambda: mod.connect_remote_device(" 192.168.1.2 ", "1-1"), ("connect", "192.168.1.2", "1-1")),
            (lambda: mod.disconnect_remote_device("3"), ("disconnect", "3")),
            (lambda: mod.unshare_device("1-2"), ("unshare", "1-2")),
            (lambda: mod.start_service(), ("start",)),
            (lambda: mod.stop_service(), ("stop",)),
        ]
        for call, expected in cases:
            with mock.patch.object(mod, "IS_LINUX", True), \
                 mock.patch.object(mod, "is_root", return_value=False), \
                 mock.patch.object(mod, "rerun_with_sudo", return_value=True) as rerun:
                self.assertTrue(call())
            rerun.assert_called_once_with(*expected)

    def test_connect_reports_failure_written_to_stderr(self):
        mod = _load_ctl_module()

        def fake_result(cmd, timeout=30):
            if "attach" in cmd:
                return 1, "", "usbip: error: Attach Request for 1-1 failed - Device busy (exported)"
            return 0, "", ""

        with mock.patch.object(mod, "IS_LINUX", True), \
             mock.patch.object(mod, "is_root", return_value=True), \
             mock.patch.object(mod, "run_cmd", return_value=""), \
             mock.patch.object(mod, "run_cmd_result", side_effect=fake_result), \
             mock.patch.object(mod, "send_control_command", return_value=None), \
             mock.patch.object(mod.shutil, "which", return_value="/usr/sbin/usbip"):
            self.assertFalse(mod.connect_remote_device("192.168.1.2", "1-1"))

    def test_choose_item_by_number_text_and_cancel(self):
        mod = _load_ctl_module()
        items = ["192.168.1.2", "192.168.1.3"]
        with mock.patch("builtins.input", side_effect=["2"]):
            self.assertEqual(mod.choose_item("server", items, mod.is_valid_host, "an IP"), "192.168.1.3")
        with mock.patch("builtins.input", side_effect=["9", "1"]):
            self.assertEqual(mod.choose_item("server", items, mod.is_valid_host, "an IP"), "192.168.1.2")
        with mock.patch("builtins.input", side_effect=["10.0.0.5"]):
            self.assertEqual(mod.choose_item("server", items, mod.is_valid_host, "an IP"), "10.0.0.5")
        for answer in (["q"], [""], KeyboardInterrupt, EOFError):
            with mock.patch("builtins.input", side_effect=answer):
                with self.assertRaises(mod.PromptCancelled):
                    mod.choose_item("server", items, mod.is_valid_host, "an IP")

    def test_dashboard_survives_ctrl_c_and_q_in_connect_flow(self):
        mod = _load_ctl_module()
        answers = iter(["9", "q", "9", KeyboardInterrupt, KeyboardInterrupt])

        def fake_input(prompt=""):
            value = next(answers)
            if value is KeyboardInterrupt:
                raise KeyboardInterrupt
            return value

        with mock.patch("builtins.input", side_effect=fake_input), \
             mock.patch.object(mod.os, "system"), \
             mock.patch.object(mod, "get_service_status", return_value={
                 "os": "Linux", "service_manager": "systemd", "running": False, "port_active": False}), \
             mock.patch.object(mod, "get_lan_ip", return_value="192.168.1.10"), \
             mock.patch.object(mod, "list_devices", return_value=[]), \
             mock.patch.object(mod, "get_attached_ports", return_value=([], None)), \
             mock.patch.object(mod, "scan_network_servers", return_value=[("192.168.1.2", "host")]), \
             mock.patch.object(mod, "list_remote_devices") as list_remote, \
             mock.patch.object(mod, "connect_remote_device") as connect:
            mod.render_dashboard("both")
        list_remote.assert_not_called()
        connect.assert_not_called()

    def test_client_mode_hides_server_actions(self):
        mod = _load_ctl_module()
        answers = iter(["3", "1", "q"])
        with mock.patch("builtins.input", side_effect=lambda prompt="": next(answers)), \
             mock.patch.object(mod.os, "system"), \
             mock.patch.object(mod, "get_service_status", return_value={
                 "os": "Linux", "service_manager": "systemd", "running": False, "port_active": False}), \
             mock.patch.object(mod, "get_lan_ip", return_value="192.168.1.10"), \
             mock.patch.object(mod, "list_devices", return_value=["  1-1    : Mouse (046d:c077)"]), \
             mock.patch.object(mod, "get_attached_ports", return_value=([], None)), \
             mock.patch.object(mod, "share_device") as share, \
             mock.patch.object(mod, "start_service") as start:
            mod.render_dashboard("client")
        share.assert_not_called()
        start.assert_not_called()

    def test_role_is_saved_in_invoking_users_home_under_sudo(self):
        import tempfile
        mod = _load_ctl_module()
        with tempfile.TemporaryDirectory() as home:
            fake_pw = mock.Mock(pw_dir=home)
            with mock.patch.dict(os.environ, {"SUDO_USER": "someone"}), \
                 mock.patch.object(mod, "is_root", return_value=True), \
                 mock.patch("pwd.getpwnam", return_value=fake_pw), \
                 mock.patch.object(mod.os, "chown"):
                self.assertEqual(mod._role_config_path(), os.path.join(home, ".config", "usbanywhere", "ctl.json"))
                self.assertTrue(mod.save_role("client"))
                self.assertEqual(mod.load_role(), "client")

    def test_relaunch_skips_sudo_v_when_nopasswd_works(self):
        mod = _load_ctl_module()
        calls = []

        def fake_run(cmd, **kwargs):
            calls.append(cmd)
            return mock.Mock(returncode=0)

        with mock.patch.object(mod, "is_root", return_value=False), \
             mock.patch.object(mod, "IS_LINUX", True), \
             mock.patch.object(mod.sys.stdin, "isatty", return_value=True), \
             mock.patch.object(mod.shutil, "which", return_value="/usr/bin/sudo"), \
             mock.patch("builtins.input", return_value="y"), \
             mock.patch.object(mod.subprocess, "run", side_effect=fake_run), \
             mock.patch.object(mod.os, "execvp") as execvp:
            mod.offer_sudo_relaunch("client")
        self.assertEqual(calls, [["sudo", "-n", "true"]])
        execvp.assert_called_once()
        self.assertEqual(execvp.call_args.args[1][-1], "dashboard")

    def test_extract_busid_and_host_validation(self):
        mod = _load_ctl_module()
        self.assertEqual(mod.extract_busid("  1-1.2    : Logitech Mouse (046d:c077)"), "1-1.2")
        self.assertIsNone(mod.extract_busid("Bus 001 Device 002: ID 046d:c077"))
        self.assertFalse(mod.is_valid_host("-h"))
        self.assertTrue(mod.is_valid_host("raspberrypi.local"))

class FakeVhci:
    """Fake vhci_hcd sysfs + usbip binary for client/sleep-guard tests."""

    def __init__(self, root, nports=4):
        self.root = root
        self.dir = os.path.join(root, "vhci_hcd.0")
        os.makedirs(self.dir)
        self.ports = {p: None for p in range(nports)}   # port -> (ip, busid) or None
        self.errors = set()        # ports reported in ERROR state
        self.port_fails = False    # 'usbip port' exits non-zero
        self.hide_from_port = set()
        self.stuck = set()         # ports that ignore detach
        self.calls = []
        self.write()

    def write(self):
        lines = ["hub port sta spd dev      sockfd local_busid"]
        for p, dev in self.ports.items():
            if dev:
                st = 7 if p in self.errors else 6
                lines.append(f"hs  {p:04d} {st:03d} 002 00010004 000003 3-{p + 1}")
            else:
                lines.append(f"hs  {p:04d} 004 000 00000000 000000 0-0")
        with open(os.path.join(self.dir, "status"), "w") as f:
            f.write("\n".join(lines) + "\n")

    def usbip_port_output(self):
        out = ["Imported USB devices", "===================="]
        for p, dev in self.ports.items():
            if dev and p not in self.hide_from_port:
                out += [f"Port {p:02d}: <Port in Use> at Full Speed(12Mbps)",
                        "       unknown vendor : unknown product (3537:1040)",
                        f"       3-{p + 1} -> usbip://{dev[0]}:3240/{dev[1]}",
                        "           -> remote bus/dev 001/004"]
        return "\n".join(out)

    def run(self, cmd, timeout):
        self.calls.append(list(cmd))
        name = os.path.basename(cmd[0])
        if name == "usbip" and cmd[1] == "port":
            if self.port_fails:
                return 1, "", "usbip: error: open vhci_driver"
            return 0, self.usbip_port_output(), ""
        if name == "usbip" and cmd[1] == "detach":
            p = int(cmd[3])
            if p not in self.stuck:
                self.ports[p] = None
                self.errors.discard(p)
                self.write()
            return 0, "", ""
        if name == "usbip" and cmd[1] == "attach":
            ip, busid = cmd[3], cmd[5]
            free = [p for p, d in self.ports.items() if d is None]
            self.ports[free[0]] = (ip, busid)
            self.write()
            return 0, "", ""
        return 0, "", ""

    def attach_calls(self):
        return [c for c in self.calls if c[:2] == ["/usr/bin/usbip", "attach"]]


class TestClientSleepGuard(unittest.TestCase):

    def setUp(self):
        import tempfile
        self.mod = _load_ctl_module()
        self.tmp = tempfile.TemporaryDirectory()
        self.vhci = FakeVhci(self.tmp.name)
        run_dir = os.path.join(self.tmp.name, "run")
        mod = self.mod
        self.patches = [
            mock.patch.object(mod, "VHCI_PLATFORM_GLOB", os.path.join(self.tmp.name, "vhci_hcd.*")),
            mock.patch.object(mod, "CLIENT_RUN_DIR", run_dir),
            mock.patch.object(mod, "CLIENT_LOCK_FILE", os.path.join(run_dir, "client.lock")),
            mock.patch.object(mod, "CLIENT_STATE_FILE", os.path.join(run_dir, "client-restore.json")),
            mock.patch.object(mod, "run_bounded", side_effect=self.vhci.run),
            mock.patch.object(mod.shutil, "which", side_effect=lambda b: f"/usr/bin/{b}"),
            mock.patch.object(mod, "send_control_command", return_value={"status": "OK"}),
            mock.patch.object(mod, "is_root", return_value=True),
            mock.patch.object(mod, "IS_LINUX", True),
            mock.patch.object(mod, "tcp_reachable", return_value=True),
        ]
        for p in self.patches:
            p.start()
        self.logs = []

    def tearDown(self):
        for p in reversed(self.patches):
            p.stop()
        self.tmp.cleanup()

    def log(self, level, msg):
        self.logs.append((level, msg))

    def test_vhci_status_parses_both_sysfs_formats(self):
        mod = self.mod
        self.vhci.ports[1] = ("10.0.0.5", "1-1")
        self.vhci.write()
        st = mod.read_vhci_status()
        self.assertEqual(st[1], {"status": 6, "local_busid": "3-2"})
        self.assertEqual(st[0]["status"], 4)
        with open(os.path.join(self.vhci.dir, "status"), "w") as f:
            f.write("prt sta spd dev socket           local_busid\n"
                    "000 004 000 00000000 0000000000000000 0-0\n"
                    "001 006 002 00010004 ffff8881 3-2\n")
        self.assertEqual(mod.read_vhci_status()[1]["status"], 6)

    def test_no_vhci_controller_means_no_ports(self):
        import shutil as sh
        sh.rmtree(self.vhci.dir)
        self.assertEqual(self.mod.read_client_ports(), ([], None))

    def test_usbip_port_failure_is_not_no_devices(self):
        mod = self.mod
        self.vhci.ports[2] = ("10.0.0.5", "1-1")
        self.vhci.write()
        self.vhci.port_fails = True
        entries, err = mod.read_client_ports()
        self.assertIn("failed", err)
        self.assertEqual([e["port"] for e in entries], ["2"])
        # Nothing occupied but 'usbip port' failing is still an error, not "no devices"
        self.vhci.ports[2] = None
        self.vhci.write()
        entries, err = mod.read_client_ports()
        self.assertEqual(entries, [])
        self.assertTrue(err)
        with mock.patch("builtins.print") as pr:
            self.assertFalse(mod.disconnect_remote_device("all"))
        self.assertNotIn("No attached USB/IP client ports found", str(pr.call_args_list))

    def test_ports_missing_from_usbip_port_are_reported(self):
        mod = self.mod
        self.vhci.ports[0] = ("10.0.0.5", "1-1")
        self.vhci.ports[3] = ("10.0.0.5", "1-2")
        self.vhci.hide_from_port = {3}
        self.vhci.write()
        entries, err = mod.read_client_ports()
        self.assertIn("3", err)
        self.assertEqual(sorted(e["port"] for e in entries), ["0", "3"])

    def test_signal_parsing_checks_logind_sender(self):
        mod = self.mod
        g = mod.ClientSleepGuard(log=self.log)
        g.logind_owner = ":1.5"
        line = json.dumps({"type": "signal", "sender": ":1.5", "path": "/org/freedesktop/login1",
                           "interface": "org.freedesktop.login1.Manager", "member": "PrepareForSleep",
                           "payload": {"type": "b", "data": [True]}})
        self.assertIs(g.parse_signal(line), True)
        with mock.patch.object(g, "refresh_logind_owner", return_value=":1.5"):
            self.assertIsNone(g.parse_signal(line.replace(":1.5", ":1.99")))
        self.assertIsNone(g.parse_signal("not json"))
        self.assertIs(g.parse_signal(line.replace("true", "false")), False)

    def test_suspend_detaches_verifies_and_records_state(self):
        mod = self.mod
        self.vhci.ports[0] = ("192.168.1.50", "1-1.4")
        self.vhci.write()
        g = mod.ClientSleepGuard(log=self.log)
        with mock.patch.object(g, "release_inhibitor") as rel:
            g.on_prepare_sleep()
        rel.assert_called_once()
        self.assertIsNone(self.vhci.ports[0])
        self.assertEqual(mod.load_client_state(), [{"ip": "192.168.1.50", "busid": "1-1.4"}])
        self.assertTrue(any("Ready for sleep" in m for _, m in self.logs), self.logs)
        mod.send_control_command.assert_any_call("192.168.1.50", "UNBIND 1-1.4", timeout=mock.ANY)

    def test_suspend_reports_port_not_released(self):
        mod = self.mod
        self.vhci.ports[1] = ("192.168.1.50", "1-1.4")
        self.vhci.stuck = {1}
        self.vhci.write()
        with mock.patch.object(mod, "GUARD_SLEEP_BUDGET", 0.8):
            g = mod.ClientSleepGuard(log=self.log)
            with mock.patch.object(g, "release_inhibitor") as rel:
                g.on_prepare_sleep()
        rel.assert_called_once()
        self.assertTrue(any(l == "error" and "NOT released" in m for l, m in self.logs), self.logs)
        self.assertFalse(any("Ready for sleep" in m for _, m in self.logs))

    def test_aborted_suspend_restores_and_skips_duplicates(self):
        mod = self.mod
        self.vhci.ports[0] = ("192.168.1.50", "1-1.4")
        self.vhci.write()
        g = mod.ClientSleepGuard(log=self.log)
        with mock.patch.object(g, "release_inhibitor"), mock.patch.object(g, "take_inhibitor") as take:
            g.on_prepare_sleep()
            g.on_resume()        # sleep aborted: PrepareForSleep(false) right away
            g.restore_thread.join(10)
        take.assert_called_once()
        self.assertEqual(mod.load_client_state(), [])
        self.assertEqual(self.vhci.ports[0], ("192.168.1.50", "1-1.4"))
        self.assertTrue(any("Restored 192.168.1.50/1-1.4" in m for _, m in self.logs), self.logs)
        # A second restore with the device already attached does not attach it twice
        mod.save_client_state([{"ip": "192.168.1.50", "busid": "1-1.4"}])
        g.restore_loop(mod.threading.Event())
        self.assertEqual(len(self.vhci.attach_calls()), 1)
        self.assertEqual(mod.load_client_state(), [])

    def test_restore_waits_for_server_and_forces_rebind(self):
        mod = self.mod
        mod.save_client_state([{"ip": "192.168.1.50", "busid": "1-1.4"}])
        reach = iter([False, False, True, True, True])
        results = iter([(False, "attach failed: busy"), (True, "verified on vhci port 0")])
        g = mod.ClientSleepGuard(log=self.log)
        with mock.patch.object(mod, "tcp_reachable", side_effect=lambda *a, **k: next(reach)), \
             mock.patch.object(mod, "attach_remote_device", side_effect=lambda *a, **k: next(results)), \
             mock.patch.object(mod, "GUARD_FORCE_REBIND_AFTER", 0), \
             mock.patch.object(g, "force_rebind") as rebind:
            g.restore_loop(mod.threading.Event(), max_delay=0.01)
        rebind.assert_called_once_with("192.168.1.50", "1-1.4")
        self.assertEqual(mod.load_client_state(), [])

    def test_restore_gives_up_and_logs(self):
        mod = self.mod
        mod.save_client_state([{"ip": "192.168.1.50", "busid": "1-1.4"}])
        g = mod.ClientSleepGuard(log=self.log)
        with mock.patch.object(mod, "tcp_reachable", return_value=False):
            g.restore_loop(mod.threading.Event(), window=0.05, max_delay=0.01)
        self.assertTrue(any(l == "error" and "never reachable" in m for l, m in self.logs), self.logs)
        self.assertEqual(mod.load_client_state(), [])

    def test_manual_disconnect_cancels_pending_restore(self):
        mod = self.mod
        mod.save_client_state([{"ip": "192.168.1.50", "busid": "1-1.4"}, {"ip": "192.168.1.50", "busid": "1-2"}])
        with mock.patch("builtins.print"):
            self.assertTrue(mod.disconnect_remote_device("1-1.4"))
        self.assertEqual(mod.load_client_state(), [{"ip": "192.168.1.50", "busid": "1-2"}])

    def test_connect_verifies_and_rejects_duplicate(self):
        mod = self.mod
        with mock.patch("builtins.print"):
            self.assertTrue(mod.connect_remote_device("192.168.1.50", "1-1.4"))
            self.assertTrue(mod.connect_remote_device("192.168.1.50", "1-1.4"))
        self.assertEqual(len(self.vhci.attach_calls()), 1)

    def test_connect_fails_when_port_never_appears(self):
        mod = self.mod
        real = self.vhci.run

        def no_port(cmd, timeout):
            if cmd[1:2] == ["attach"]:
                self.vhci.calls.append(list(cmd))
                return 0, "", ""
            return real(cmd, timeout)

        with mock.patch.object(mod, "run_bounded", side_effect=no_port):
            ok, msg = mod.attach_remote_device("192.168.1.50", "1-1.4", verify_timeout=0.3)
        self.assertFalse(ok)
        self.assertIn("no vhci port", msg)

    def test_client_lock_excludes_threads_and_times_out(self):
        mod = self.mod
        with mod.client_lock(timeout=1) as first:
            self.assertTrue(first)
            got = []
            t = mod.threading.Thread(target=lambda: got.append(mod.client_lock(timeout=0.2).__enter__()))
            t.start()
            t.join()
            self.assertEqual(got, [False])
        with mod.client_lock(timeout=0.2) as again:
            self.assertTrue(again)

    def test_run_bounded_does_not_block(self):
        import time as _t
        for p in self.patches:
            p.stop()
        self.patches = []
        t0 = _t.monotonic()
        rc, _, err = self.mod.run_bounded(["sleep", "5"], timeout=0.3)
        self.assertIsNone(rc)
        self.assertIn("timed out", err)
        self.assertLess(_t.monotonic() - t0, 2)

    def test_guard_units(self):
        units = self.mod._guard_units()
        hook = units["usbanywhere-client-sleep.service"]
        self.assertIn("Before=sleep.target", hook)
        self.assertIn("WantedBy=sleep.target", hook)
        self.assertIn("client-sleep-hook pre", hook)
        self.assertIn("client-guard run", units["usbanywhere-client-guard.service"])

    def test_guard_lock_prevents_second_instance(self):
        import fcntl
        mod = self.mod
        os.makedirs(mod.CLIENT_RUN_DIR, exist_ok=True)
        with open(os.path.join(mod.CLIENT_RUN_DIR, "guard.lock"), "w") as lock:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
            g = mod.ClientSleepGuard(log=self.log)
            self.assertFalse(g.run())
        self.assertTrue(any(l == "error" and "Another client guard" in m for l, m in self.logs), self.logs)

    def test_dashboard_guard_waits_for_new_inhibitor_pid(self):
        mod = self.mod
        fake_proc = mock.Mock()
        fake_proc.poll.return_value = None
        fake_proc.stdin = mock.Mock()
        pids_seq = iter([{100}, {100}, {100, 200}])
        with mock.patch.object(mod.subprocess, "Popen", return_value=fake_proc), \
             mock.patch.object(mod.ClientSleepGuard, "inhibitor_pids", side_effect=lambda: next(pids_seq)), \
             mock.patch.object(mod.time, "sleep"):
            dg = mod.DashboardGuard()
            self.assertTrue(dg.start())
            self.assertTrue(dg.stop())
        fake_proc.stdin.close.assert_called_once()

    def test_dashboard_guard_menu_switches_modes_and_stops_on_exit(self):
        mod = self.mod
        answers = iter(["g", "1", "", "g", "2", "", "g", "0", "", "q"])
        temp = mock.Mock()
        temp.active.return_value = False
        temp.start.return_value = True
        temp.stop.return_value = True
        with mock.patch("builtins.input", side_effect=lambda prompt="": next(answers)), \
             mock.patch.object(mod.os, "system"), \
             mock.patch.object(mod, "DashboardGuard", return_value=temp), \
             mock.patch.object(mod, "get_service_status", return_value={
                 "os": "Linux", "service_manager": "systemd", "running": False, "port_active": False}), \
             mock.patch.object(mod, "get_lan_ip", return_value="192.168.1.10"), \
             mock.patch.object(mod, "get_attached_ports", return_value=([], None)), \
             mock.patch.object(mod, "run_cmd", return_value="inactive"), \
             mock.patch.object(mod, "SYSTEMD_UNIT_DIR", self.tmp.name), \
             mock.patch.object(mod, "manage_client_guard", return_value=True) as mg:
            mod.render_dashboard("client")
        temp.start.assert_called_once()
        mg.assert_called_once_with("install")
        self.assertGreaterEqual(temp.stop.call_count, 2)

if __name__ == "__main__":
    unittest.main()
