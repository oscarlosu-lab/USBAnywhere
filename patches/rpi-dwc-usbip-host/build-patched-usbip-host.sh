#!/usr/bin/env bash
set -euo pipefail

# Diagnostic out-of-tree builder for patched usbip-host.ko (Raspberry Pi dwc_otg/dwc2 + DualSense)
# Credit: @Fumanchooo (https://github.com/oscarlosu-lab/USBAnywhere/issues/9)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
KVER="${KVER:-$(uname -r)}"
KBUILD_DIR="${KBUILD_DIR:-/lib/modules/${KVER}/build}"
KERNEL_SRC="${KERNEL_SRC:-}"
WORK_DIR="${WORK_DIR:-/tmp/usbip-host-patched}"

if [[ -z "${KERNEL_SRC}" || ! -d "${KERNEL_SRC}/drivers/usb/usbip" ]]; then
    echo "Usage: KERNEL_SRC=/path/to/linux-source $0"
    echo "Error: KERNEL_SRC must point to a matching kernel source tree containing drivers/usb/usbip/."
    exit 2
fi

if [[ ! -d "${KBUILD_DIR}" ]]; then
    echo "Error: Kernel headers not found at ${KBUILD_DIR}."
    echo "Install with: sudo apt-get install -y raspberrypi-kernel-headers || sudo apt-get install -y linux-headers-${KVER}"
    exit 1
fi

rm -rf "${WORK_DIR}"
mkdir -p "${WORK_DIR}"
cp -r "${KERNEL_SRC}/drivers/usb/usbip/"* "${WORK_DIR}/"

for p in \
    "${SCRIPT_DIR}/01-packet-count.patch" \
    "${SCRIPT_DIR}/02-iso-interval.patch" \
    "${SCRIPT_DIR}/03-iso-in-length.patch"; do
    echo "[*] Applying $(basename "$p")..."
    patch --directory="${WORK_DIR}" --strip=4 --forward < "$p"
done

cat << 'EOF' > "${WORK_DIR}/Kbuild"
obj-m += usbip-host.o
usbip-host-y := stub_dev.o stub_main.o stub_rx.o stub_tx.o
EOF

make -C "${KBUILD_DIR}" M="${WORK_DIR}" modules

echo "[+] Built patched module: ${WORK_DIR}/usbip-host.ko"
echo "    To test temporarily (reverted on reboot):"
echo "      sudo rmmod usbip-host 2>/dev/null || true"
echo "      sudo insmod ${WORK_DIR}/usbip-host.ko"
