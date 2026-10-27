#!/usr/bin/env bash
# ==============================================================================
# 🌐 USBAnywhere — 1-Click Linux & Raspberry Pi Plug & Play Server Installer
# https://github.com/oscarlosu-lab/USBAnywhere
#
# Usage:
#   curl -fsSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh | sudo bash
#
# Features:
#   1. Installs usbip, hwdata, and usbutils across Debian/Ubuntu/PiOS/Fedora/Arch
#   2. Loads usbip_core & usbip_host kernel modules persistently across reboots
#   3. Creates a systemd service (usbanywhere.service) running usbipd
#   4. Sets up a udev rule + auto-bind helper with configurable safe security policy
# ==============================================================================

set -euo pipefail

echo "================================================================"
echo " 🌐 USBAnywhere Server Installer (Plug & Play Edition)"
echo "================================================================"

if [ "${EUID:-$(id -u)}" -ne 0 ]; then
    echo "❌ Please run as root (use sudo)."
    exit 1
fi

# 1. Detect Package Manager & Install Dependencies
echo "[1/4] Installing USB/IP packages..."
if command -v apt-get >/dev/null 2>&1; then
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq || true
    # Try standard Debian/Armbian/Raspbian 'usbip' package first; fallback to Ubuntu 'linux-tools'
    if ! apt-get install -y --no-install-recommends usbip hwdata usbutils 2>/dev/null; then
        apt-get install -y --no-install-recommends "linux-tools-$(uname -r)" hwdata usbutils 2>/dev/null || \
        apt-get install -y --no-install-recommends linux-tools-generic linux-tools-common hwdata usbutils 2>/dev/null || true
    fi
elif command -v dnf >/dev/null 2>&1; then
    dnf install -y usbip hwdata usbutils || true
elif command -v pacman >/dev/null 2>&1; then
    pacman -Sy --noconfirm usbip hwdata usbutils || true
elif command -v opkg >/dev/null 2>&1; then
    opkg update && opkg install usbip-server kmod-usb-ohci usbutils || true
else
    echo "⚠️ Unsupported package manager. Ensure 'usbip' and 'usbipd' are installed."
fi

# On Ubuntu with custom SBC kernels (Armbian/OrangePi/RPi), /usr/bin/usbip wrapper may fail
# looking for /usr/lib/linux-tools/$(uname -r)/usbip. Link the actual binary if needed.
if ! usbip version >/dev/null 2>&1; then
    actual_usbip=$(find /usr/lib/linux-tools -name usbip -type f 2>/dev/null | sort -V | tail -n 1 || true)
    if [ -n "$actual_usbip" ]; then
        echo "Linking $actual_usbip to /usr/local/sbin/usbip..."
        mkdir -p /usr/local/sbin
        ln -sf "$actual_usbip" /usr/local/sbin/usbip
        actual_usbipd=$(dirname "$actual_usbip")/usbipd
        [ -f "$actual_usbipd" ] && ln -sf "$actual_usbipd" /usr/local/sbin/usbipd
    fi
fi

# 2. Persist USB/IP kernel modules across reboots
echo "[2/4] Enabling USB/IP kernel modules..."
modprobe usbip_core 2>/dev/null || true
modprobe usbip_host 2>/dev/null || true

mkdir -p /etc/modules-load.d
cat << 'EOF' > /etc/modules-load.d/usbanywhere.conf
usbip_core
usbip_host
EOF

# 3. Create Default Configuration File (Safe Auto-Export Policy)
mkdir -p /etc/default
if [ ! -f /etc/default/usbanywhere ]; then
    cat << 'EOF' > /etc/default/usbanywhere
# ==============================================================================
# USBAnywhere Server — Auto-Export Security Policy
# ==============================================================================
# Set to "true" to automatically export keyboards and mice (HID Class 03).
# Default: false (protects host console from unauthorized remote capture).
ALLOW_HID="false"

# Set to "true" to automatically export USB flash drives and storage (Class 08).
# Default: false (prevents unauthorized remote mounting of host disks).
ALLOW_STORAGE="false"
EOF
fi

# 4. Create Plug & Play Auto-Bind Helper Script
echo "[3/4] Installing Plug & Play auto-bind watcher..."
cat << 'EOF' > /usr/local/bin/usbanywhere-autobind.sh
#!/usr/bin/env bash
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin"

# Load policy config
[ -f /etc/default/usbanywhere ] && . /etc/default/usbanywhere

ALLOW_HID="${ALLOW_HID:-false}"
ALLOW_STORAGE="${ALLOW_STORAGE:-false}"

USBIP_BIN=$(command -v usbip || echo "/usr/sbin/usbip")
LOCK_FILE="/run/usbanywhere-autobind.lock"

bind_all_usb() {
    (
        flock -w 5 9 || exit 0

        local root_src root_disks=""
        root_src=$(findmnt -n -o SOURCE / 2>/dev/null || true)
        if [[ -n "$root_src" && -b "$root_src" ]]; then
            root_disks=$(lsblk -no PKNAME "$root_src" 2>/dev/null | sort -u | tr '\n' ' ')
        fi

        for devpath in /sys/bus/usb/devices/*; do
            [ -e "$devpath" ] || continue
            busid=$(basename "$devpath")
            # Skip interfaces (containing ':') and root usb buses ('usb1', 'usb2', etc.)
            [[ "$busid" == *:* ]] && continue
            [[ "$busid" == usb* ]] && continue
            [[ ! -f "$devpath/idVendor" ]] && continue

            bDeviceClass=$(cat "$devpath/bDeviceClass" 2>/dev/null || echo "")
            idVendor=$(cat "$devpath/idVendor" 2>/dev/null || echo "")
            idProduct=$(cat "$devpath/idProduct" 2>/dev/null || echo "")

            # Skip USB Hubs (Class 09)
            [[ "$bDeviceClass" == "09" ]] && continue

            # Skip pure HID devices (Keyboards/Mice - Class 03) by default unless ALLOW_HID=true
            if [[ "$ALLOW_HID" != "true" && "$bDeviceClass" == "03" ]]; then
                continue
            fi

            # Skip Mass Storage (Class 08) by default unless ALLOW_STORAGE=true
            if [[ "$ALLOW_STORAGE" != "true" && "$bDeviceClass" == "08" ]]; then
                continue
            fi

            # Skip Raspberry Pi onboard SMSC/Microchip USB Ethernet bridges
            [[ "$idVendor:$idProduct" == "0424:ec00" ]] && continue
            [[ "$idVendor:$idProduct" == "0424:7800" ]] && continue

            # Skip USB network adapters (Ethernet / Wi-Fi dongles) to prevent losing SSH connectivity
            if ls "$devpath"/*/net/* >/dev/null 2>&1; then
                continue
            fi

            # Skip USB storage devices that back the running root (/) filesystem
            skip_root=0
            for blk in "$devpath"/*/host*/target*/*/block/* "$devpath"/*/block/*; do
                [ -e "$blk" ] || continue
                blkname=$(basename "$blk")
                if [[ -n "$root_disks" && " $root_disks " == *" $blkname "* ]]; then
                    skip_root=1
                    break
                fi
            done
            [[ "$skip_root" -eq 1 ]] && continue

            # Bind device if not already bound to usbip-host, or recover if in error state (3)
            driver_link=$(readlink "$devpath/driver" 2>/dev/null || echo "")
            if [[ "$driver_link" != *"usbip-host"* ]]; then
                if [[ -z "$driver_link" ]]; then
                    echo -n "$busid" > /sys/bus/usb/drivers/usb/bind 2>/dev/null || true
                fi
                if ! "$USBIP_BIN" bind -b "$busid" >/dev/null 2>&1; then
                    echo -n "del $busid" > /sys/bus/usb/drivers/usbip-host/match_busid 2>/dev/null || true
                    echo -n "$busid" > /sys/bus/usb/drivers/usb/bind 2>/dev/null || true
                    "$USBIP_BIN" bind -b "$busid" >/dev/null 2>&1 || true
                fi
            else
                status=$(cat "$devpath/usbip_status" 2>/dev/null || echo "1")
                if [[ "$status" == "3" ]]; then
                    "$USBIP_BIN" unbind -b "$busid" >/dev/null 2>&1 || true
                    echo -n "$busid" > /sys/bus/usb/drivers/usb/bind 2>/dev/null || true
                    "$USBIP_BIN" bind -b "$busid" >/dev/null 2>&1 || true
                fi
            fi
        done
    ) 9>"$LOCK_FILE"
}

if [[ "${1:-}" == "--watch" ]]; then
    while true; do
        bind_all_usb
        sleep 2
    done
else
    bind_all_usb
fi
EOF
chmod 755 /usr/local/bin/usbanywhere-autobind.sh

# 5. Configure systemd services & udev hotplug rule
echo "[4/4] Configuring systemd daemons and udev hotplug rules..."
export PATH="/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH"
if command -v systemctl >/dev/null 2>&1; then
    USBIPD_BIN=$(command -v usbipd || echo "/usr/sbin/usbipd")
    cat << EOF > /etc/systemd/system/usbanywhere.service
[Unit]
Description=USBAnywhere USB/IP Server Daemon
After=network.target

[Service]
Type=forking
ExecStart=${USBIPD_BIN} -D
Restart=always
RestartSec=2

[Install]
WantedBy=multi-user.target
EOF

    cat << 'EOF' > /etc/systemd/system/usbanywhere-autobind.service
[Unit]
Description=USBAnywhere Plug & Play USB Auto-Bind Watcher
After=usbanywhere.service
Requires=usbanywhere.service

[Service]
Type=simple
ExecStart=/usr/local/bin/usbanywhere-autobind.sh --watch
Restart=always
RestartSec=2

[Install]
WantedBy=multi-user.target
EOF

    systemctl daemon-reload
    systemctl enable usbanywhere.service usbanywhere-autobind.service
    systemctl restart usbanywhere.service || true
    systemctl restart usbanywhere-autobind.service || true
else
    usbipd -D || true
    nohup /usr/local/bin/usbanywhere-autobind.sh --watch >/dev/null 2>&1 &
fi

if [ -d /etc/udev/rules.d ]; then
    cat << 'EOF' > /etc/udev/rules.d/99-usbanywhere-autobind.rules
ACTION=="add", SUBSYSTEM=="usb", ENV{DEVTYPE}=="usb_device", RUN+="/bin/sh -c 'systemd-run --no-block /usr/local/bin/usbanywhere-autobind.sh || /usr/local/bin/usbanywhere-autobind.sh'"
EOF
    udevadm control --reload-rules 2>/dev/null || true
fi

IP_ADDR=$(hostname -I 2>/dev/null | awk '{print $1}')
echo ""
echo "✅ USBAnywhere Server is active and listening on port 3240!"
echo "📡 Server IP: ${IP_ADDR:-<your-server-ip>}"
echo "🛡️ Safe Auto-Export policy active: /etc/default/usbanywhere"
echo "   (HID keyboards/mice and USB disks are protected from accidental exposure)."
if command -v ufw >/dev/null 2>&1 && ufw status 2>/dev/null | grep -qw "active"; then
    echo "🔒 Firewall Notice (ufw is active):"
    echo "   To restrict port 3240 to your local subnet (recommended):"
    echo "   sudo ufw allow from ${IP_ADDR%.*}.0/24 to any port 3240 proto tcp comment 'USBAnywhere LAN'"
fi
echo "================================================================"
