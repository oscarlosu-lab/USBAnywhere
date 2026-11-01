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
        if_classes=""
        for if_dir in "$d/$b:"*; do
            if [ -f "$if_dir/bInterfaceClass" ]; then
                ifc=$(cat "$if_dir/bInterfaceClass" 2>/dev/null || echo "")
                if_classes="$if_classes $ifc"
            fi
        done

        # Skip Hubs (Class 09) at device or interface level
        if [ "$dev_class" = "09" ] || echo "$if_classes" | grep -qw "09"; then
            continue
        fi

        # Skip HID (Keyboards / Mice, Class 03) by default at device or interface level
        if [ "$ALLOW_HID" != "true" ]; then
            if [ "$dev_class" = "03" ] || echo "$if_classes" | grep -qw "03"; then
                continue
            fi
        fi

        # Skip Mass Storage (Class 08) by default at device or interface level
        if [ "$ALLOW_STORAGE" != "true" ]; then
            if [ "$dev_class" = "08" ] || echo "$if_classes" | grep -qw "08"; then
                continue
            fi
        fi

        usbip bind -b "$b" >/dev/null 2>&1 || true
    done
    sleep 5
done
