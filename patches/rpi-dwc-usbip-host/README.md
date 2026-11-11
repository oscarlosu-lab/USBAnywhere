# Experimental `usbip-host` Kernel Patches for Raspberry Pi (`dwc_otg` / `dwc2` + DualSense)

**Credit & Attribution:** Diagnostic patches contributed by [@Fumanchooo](https://github.com/Fumanchooo) in [USBAnywhere#9](https://github.com/oscarlosu-lab/USBAnywhere/issues/9), tested on Raspberry Pi Zero 2 W (`6.18.60+rpt-rpi-v8`, ARM64) with Sony DualSense (`054c:0ce6`, firmware `0x0110002a`).

## Included Patches

1. **`01-packet-count.patch`** (`drivers/usb/usbip/stub_rx.c`)
   - Restores `number_of_packets = np` (and `0` for non-ISO scatter/gather URBs) immediately after `usbip_pack_pdu()` so incoming Windows USB/IP `number_of_packets = -1` headers cannot overwrite URB packet allocation counts or trigger a kernel Oops in `stub_send_ret_submit`.
2. **`02-iso-interval.patch`** (`drivers/usb/usbip/stub_rx.c`)
   - Converts high-speed isochronous endpoint `bInterval` (1..16) to `1U << (bInterval - 1)` microframes before submitting to `dwc_otg` / `dwc2`, preventing 2x accelerated/choppy DualSense speaker playback on Pi Zero 2 W.
3. **`03-iso-in-length.patch`** (`drivers/usb/usbip/stub_tx.c`)
   - Validates each ISO IN frame descriptor against buffer bounds and sums `actual_length` into `urb->actual_length` before building the response PDU, preventing `dwc2` DualSense microphone reconnect loops (`actual length of urb 0 does not match iso packet sizes`).

## Manual / Helper Usage (Raspberry Pi)

> [!CAUTION]
> These are diagnostic patches targeting the Raspberry Pi Linux `usbip-host` kernel module (base commit `0b41724521c9adf5399409d02aece6c1df0a4432` / Linux 6.18). Building out-of-tree modules requires `linux-headers-$(uname -r)` and matching kernel source for `drivers/usb/usbip/`.

Run the opt-in build script from the Raspberry Pi pointing at a clean matching kernel source tree (`KERNEL_SRC`):

```bash
chmod +x build-patched-usbip-host.sh
KERNEL_SRC=/path/to/rpi-linux ./build-patched-usbip-host.sh
# Temporary test load without modifying disk modules (reboot restores stock module):
sudo rmmod usbip-host 2>/dev/null || true
sudo insmod /tmp/usbip-host-patched/usbip-host.ko
```
