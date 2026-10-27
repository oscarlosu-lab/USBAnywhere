FROM debian:bookworm-slim

LABEL org.opencontainers.image.title="USBAnywhere Server"
LABEL org.opencontainers.image.description="Lightweight Plug & Play USB/IP Server for HomeLab, Unraid, TrueNAS & Proxmox"
LABEL org.opencontainers.image.url="https://github.com/oscarlosu-lab/USBAnywhere"
LABEL org.opencontainers.image.licenses="GPL-3.0"

RUN apt-get update && \
    apt-get install -y --no-install-recommends usbip hwdata usbutils kmod ca-certificates && \
    rm -rf /var/lib/apt/lists/*

COPY scripts/install-server.sh /usr/local/bin/install-server.sh
COPY docker/entrypoint.sh /usr/local/bin/entrypoint.sh

RUN chmod +x /usr/local/bin/install-server.sh /usr/local/bin/entrypoint.sh

EXPOSE 3240/tcp

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
