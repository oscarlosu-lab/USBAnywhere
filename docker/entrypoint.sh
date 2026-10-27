#!/bin/sh
set -e

# Load usbip-host kernel module if possible
modprobe usbip_host 2>/dev/null || true

# Start usbip daemon in background
usbipd -D

ALLOW_HID="${ALLOW_HID:-false}"
ALLOW_STORAGE="${ALLOW_STORAGE:-false}"

echo "[*] USBAnywhere Server container started on TCP port 3240."
echo "[*] Safe Auto-Export policy: ALLOW_HID=${ALLOW_HID}, ALLOW_STORAGE=${ALLOW_STORAGE}"

# Auto-export loop
while true; do
    for d in /sys/bus/usb/devices/*; do
        [ -e "$d" ] || continue
        b=$(basename "$d")
        case "$b" in
            *:*) continue ;;
            usb*) continue ;;
        esac
        [ ! -f "$d/idVendor" ] && continue

        dev_class=$(cat "$d/bDeviceClass" 2>/dev/null || echo "")

        # Skip Hubs (Class 09)
        [ "$dev_class" = "09" ] && continue

        # Skip HID (Keyboards / Mice, Class 03) by default
        if [ "$ALLOW_HID" != "true" ] && [ "$dev_class" = "03" ]; then
            continue
        fi

        # Skip Mass Storage (Class 08) by default
        if [ "$ALLOW_STORAGE" != "true" ] && [ "$dev_class" = "08" ]; then
            continue
        fi

        usbip bind -b "$b" >/dev/null 2>&1 || true
    done
    sleep 5
done
