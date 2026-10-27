#!/usr/bin/env python3
"""
USBAnywhere Video Tutorial Generator
Generates high-definition (1080p) video tutorials with neural AI voiceover in English and Spanish.
"""

import os
import sys
import asyncio
import subprocess
import edge_tts
from PIL import Image, ImageDraw, ImageFont

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

WORKDIR = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(WORKDIR)
OUTPUT_DIR = os.path.join(REPO_ROOT, "docs")
TEMP_DIR = os.path.join(REPO_ROOT, "release", "temp_video")
ASSETS_DIR = os.path.join(REPO_ROOT, "docs", "assets")

os.makedirs(TEMP_DIR, exist_ok=True)
os.makedirs(OUTPUT_DIR, exist_ok=True)

FONT_REGULAR = r"C:\Windows\Fonts\segoeui.ttf"
FONT_BOLD = r"C:\Windows\Fonts\segoeuib.ttf"
FONT_CODE = r"C:\Windows\Fonts\consola.ttf"

def get_font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()

SCENES = [
    {
        "id": "scene1",
        "title_es": "USBAnywhere vs VirtualHere",
        "title_en": "USBAnywhere vs VirtualHere",
        "subtitle_es": "La alternativa libre y sin límites para periféricos USB por red",
        "subtitle_en": "The free, unrestricted alternative for USB over IP",
        "audio_es": (
            "¿Cansado de pagar licencias de cuarenta y nueve dólares por servidor y estar limitado a un solo dispositivo USB con VirtualHere? "
            "Te presentamos USBAnywhere. La alternativa libre, gratuita e ilimitada para Windows y Linux. "
            "Conecta todos tus dispositivos USB por red local, Wi-Fi o Internet con latencia ultrabaja y drivers certificados por Microsoft."
        ),
        "audio_en": (
            "Tired of paying forty-nine dollars per server and being restricted to a single USB device with VirtualHere? "
            "Welcome to USBAnywhere. The completely free, unrestricted, and open-source alternative for Windows and Linux. "
            "Connect all your remote USB peripherals over LAN, Wi-Fi, or Internet with ultra-low latency and Microsoft WHQL-certified drivers."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural"
    },
    {
        "id": "scene2",
        "title_es": "Servidor Linux en 1-Click (Plug & Play)",
        "title_en": "1-Click Linux Server Setup (Plug & Play)",
        "subtitle_es": "Instalación automática y auto-exportación USB con udev",
        "subtitle_en": "Automated installation and udev hotplug auto-export",
        "audio_es": (
            "Configurar tu servidor es tan fácil como copiar y pegar una sola línea de comandos en tu Raspberry Pi o máquina Linux. "
            "Nuestro instalador oficial configura el servicio en segundo plano y activa una regla inteligente en udev "
            "que comparte automáticamente cualquier periférico USB en el instante en que lo enchufas, igual que VirtualHere, pero sin límites."
        ),
        "audio_en": (
            "Setting up your server is as simple as copying and pasting a single command on your Raspberry Pi or Linux board. "
            "Our official installer not only configures the background daemon, but also activates an intelligent udev rule "
            "that automatically shares any USB peripheral the second you plug it in, just like VirtualHere, but with zero restrictions."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural"
    },
    {
        "id": "scene3",
        "title_es": "Cliente Nativo para Windows 10 y 11",
        "title_en": "Native Client for Windows 10 & 11",
        "subtitle_es": "Autodescubrimiento, medidor de ping en tiempo real y conexión en 1-click",
        "subtitle_en": "Auto-discovery, real-time latency ping meter, and 1-click connect",
        "audio_es": (
            "En Windows, USBAnywhere cuenta con una interfaz gráfica moderna y fluida. "
            "Escanea tu red local automáticamente, detecta servidores en segundos y mide la latencia de conexión en tiempo real en milisegundos. "
            "Puedes conectar y desconectar periféricos con un solo clic, y guardar favoritos para que se reconecten solos al iniciar Windows."
        ),
        "audio_en": (
            "On Windows, USBAnywhere features a modern and responsive native interface. "
            "It scans your local subnet automatically, discovers servers in seconds, and measures real-time connection latency in milliseconds. "
            "You can attach and detach peripherals with a single click, and save favorites to auto-reconnect whenever Windows starts."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural"
    },
    {
        "id": "scene4",
        "title_es": "Máximo Rendimiento: Gaming y SimRacing",
        "title_en": "Maximum Performance: Gaming & SimRacing",
        "subtitle_es": "Modo Low-Latency para Moonlight/Sunshine y Zero-Copy para cámaras y discos",
        "subtitle_en": "Low-Latency mode for Moonlight/Sunshine and Zero-Copy for webcams & drives",
        "audio_es": (
            "Para los gamers y pilotos de SimRacing que transmiten con Moonlight o Sunshine, USBAnywhere incluye el modo Low-Latency "
            "con callbacks directos en el kernel para eliminar el retardo en mandos, volantes y pedales con vibración háptica completa. "
            "Y para cámaras web o impresoras tres D, el modo Zero-Copy garantiza el máximo ancho de banda."
        ),
        "audio_en": (
            "For gamers, simracers, and streamers using Moonlight or Sunshine, USBAnywhere includes a dedicated Low-Latency mode "
            "with direct kernel event callbacks, eliminating input lag on controllers, steering wheels, and pedals with full rumble support. "
            "And for webcams or 3D printers, Zero-Copy mode delivers maximum throughput."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural"
    },
    {
        "id": "scene5",
        "title_es": "Descarga e Instalación en Segundos",
        "title_en": "Download & Get Started in Seconds",
        "subtitle_es": "100% Free y Open Source en GitHub, Winget y Scoop",
        "subtitle_en": "100% Free and Open Source on GitHub, Winget & Scoop",
        "audio_es": (
            "USBAnywhere es cien por cien gratuito, de código abierto y compatible con Winget, Scoop y Docker. "
            "Descarga ya la versión tres punto cero desde GitHub, dale una estrella al repositorio y disfruta de periféricos USB ilimitados por red."
        ),
        "audio_en": (
            "USBAnywhere is one hundred percent free, open source, and ready for Winget, Scoop, and Docker. "
            "Download version three point zero today from GitHub, leave a star on the repository, and enjoy unlimited USB peripherals over your network."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural"
    }
]

async def generate_audio(scene, lang):
    text = scene[f"audio_{lang}"]
    voice = scene[f"voice_{lang}"]
    out_path = os.path.join(TEMP_DIR, f"{scene['id']}_{lang}.mp3")
    comm = edge_tts.Communicate(text, voice)
    await comm.save(out_path)
    return out_path

def get_audio_duration(file_path):
    cmd = [
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", file_path
    ]
    res = subprocess.run(cmd, capture_output=True, text=True, check=True)
    return float(res.stdout.strip())

def create_slide(scene, lang, out_path):
    w, h = 1920, 1080
    im = Image.new("RGB", (w, h), color="#0F172A") # Dark slate
    draw = ImageDraw.Draw(im)

    # Accent Header Bar
    draw.rectangle([(0, 0), (w, 12)], fill="#06B6D4") # Cyan

    # Top Brand Header
    font_brand = get_font(FONT_BOLD, 32)
    font_badge = get_font(FONT_BOLD, 22)
    draw.text((80, 50), "🌐 USBAnywhere", font=font_brand, fill="#06B6D4")
    draw.text((360, 56), "•  Virtual USB over IP  •  v3.0.0", font=font_brand, fill="#94A3B8")

    # Title & Subtitle
    title = scene[f"title_{lang}"]
    subtitle = scene[f"subtitle_{lang}"]
    font_title = get_font(FONT_BOLD, 54)
    font_sub = get_font(FONT_REGULAR, 28)
    draw.text((80, 120), title, font=font_title, fill="#F8FAFC")
    draw.text((80, 190), subtitle, font=font_sub, fill="#38BDF8")

    scene_id = scene["id"]

    if scene_id == "scene1":
        # Card 1: VirtualHere
        draw.rounded_rectangle([(80, 270), (900, 900)], radius=20, fill="#1E293B", outline="#475569", width=2)
        font_card_h = get_font(FONT_BOLD, 36)
        font_body = get_font(FONT_REGULAR, 24)
        draw.text((120, 310), "❌ VirtualHere", font=font_card_h, fill="#EF4444")
        vh_points = [
            "• $49.00 USD per server license",
            "• Free tier limited to only 1 device",
            "• Manual Linux command-line setup",
            "• Dated Windows 98-style tree view",
            "• Closed-source proprietary software"
        ] if lang == "en" else [
            "• 49,00 $ por servidor",
            "• Versión gratis limitada a 1 solo dispositivo",
            "• Configuración manual en Linux por comandos",
            "• Interfaz clásica estilo Windows 98",
            "• Software privativo de código cerrado"
        ]
        y = 390
        for pt in vh_points:
            draw.text((120, y), pt, font=font_body, fill="#CBD5E1")
            y += 70

        # Card 2: USBAnywhere
        draw.rounded_rectangle([(1020, 270), (1840, 900)], radius=20, fill="#0F2D37", outline="#06B6D4", width=3)
        draw.text((1060, 310), "✅ USBAnywhere v3.0", font=font_card_h, fill="#10B981")
        ua_points = [
            "• 100% Free & Open Source (GPLv3)",
            "• Unlimited simultaneous devices",
            "• 1-Click Plug & Play Linux auto-installer",
            "• Modern Fluent WPF UI with Dark/Light themes",
            "• Microsoft WHQL-certified drivers (HVCI ready)"
        ] if lang == "en" else [
            "• 100% Gratis y Código Abierto (GPLv3)",
            "• Dispositivos simultáneos ilimitados",
            "• Auto-instalador Linux Plug & Play en 1-Click",
            "• Interfaz moderna WPF con temas Oscuro/Claro",
            "• Drivers certificados WHQL de Microsoft"
        ]
        y = 390
        for pt in ua_points:
            draw.text((1060, y), pt, font=font_body, fill="#F1F5F9")
            y += 70

    elif scene_id == "scene2":
        # Terminal Window Card
        draw.rounded_rectangle([(80, 270), (1840, 900)], radius=20, fill="#020617", outline="#334155", width=2)
        # Terminal Header
        draw.ellipse([(110, 295), (130, 315)], fill="#EF4444")
        draw.ellipse([(145, 295), (165, 315)], fill="#F59E0B")
        draw.ellipse([(180, 295), (200, 315)], fill="#10B981")
        draw.text((220, 292), "bash - raspberrypi ~ usbanywhere server setup", font=get_font(FONT_REGULAR, 20), fill="#64748B")

        font_cmd = get_font(FONT_CODE, 26)
        font_term = get_font(FONT_CODE, 22)
        draw.text((120, 360), "$ curl -sSL https://raw.githubusercontent.com/oscarlosu-lab/USBAnywhere/main/scripts/install-server.sh | sudo bash", font=font_cmd, fill="#38BDF8")

        term_output = [
            "[1/4] Installing USB/IP kernel packages (usbip, hwdata, usbutils)... [DONE]",
            "[2/4] Enabling kernel modules (usbip-core, usbip_host) in /etc/modules-load.d... [DONE]",
            "[3/4] Installing Plug & Play Auto-Bind engine to /usr/local/bin/usbanywhere-autobind.sh...",
            "[4/4] Creating systemd service (usbanywhere.service) and udev hotplug rules...",
            "      --> Auto-reloading udev rules: any USB plugged in will be automatically exported!",
            "",
            "✅ USBAnywhere Server is active and listening on port 3240!",
            "🔌 Plug & Play Auto-Export is ENABLED: zero manual bind commands required."
        ]
        y = 430
        for line in term_output:
            color = "#10B981" if "✅" in line or "ENABLED" in line else "#94A3B8"
            draw.text((120, y), line, font=font_term, fill=color)
            y += 48

    elif scene_id in ("scene3", "scene4"):
        # UI Screenshot / Split View
        app_img_path = os.path.join(ASSETS_DIR, "app_frame.png")
        if os.path.exists(app_img_path):
            try:
                app_im = Image.open(app_img_path)
                app_im.thumbnail((860, 680), Image.Resampling.LANCZOS)
                im.paste(app_im, (80, 260))
            except Exception as e:
                print("Could not load app frame:", e)

        # Right Side Highlights
        draw.rounded_rectangle([(980, 260), (1840, 920)], radius=20, fill="#1E293B", outline="#0EA5E9", width=2)
        font_h = get_font(FONT_BOLD, 32)
        font_body = get_font(FONT_REGULAR, 24)

        if scene_id == "scene3":
            draw.text((1020, 300), "⚡ Real-Time Features" if lang == "en" else "⚡ Funciones en Tiempo Real", font=font_h, fill="#38BDF8")
            pts = [
                "🔍 Subnet Auto-Discovery in seconds",
                "⏱️ Live Latency Badge (e.g. ⚡ 0.8 ms ping)",
                "🔗 1-Click Connect & Disconnect",
                "⭐ Automatic Favorites Reconnection",
                "🛡️ Core Isolation (HVCI) Certified Drivers",
                "🌐 Built-in SSH & VPN (Tailscale / ZeroTier)"
            ] if lang == "en" else [
                "🔍 Autodescubrimiento de red en segundos",
                "⏱️ Medidor de ping en tiempo real (ej. ⚡ 0.8 ms)",
                "🔗 Conexión y desconexión en 1-click",
                "⭐ Reconexión automática de favoritos",
                "🛡️ Drivers compatibles con Aislamiento de Núcleo",
                "🌐 SSH y VPN integradas (Tailscale / ZeroTier)"
            ]
        else:
            draw.text((1020, 300), "🎮 Low-Latency vs Zero-Copy", font=font_h, fill="#F59E0B")
            pts = [
                "🏎️ Low-Latency WSK Mode: Ultra-low lag for controllers,",
                "   steering wheels, load-cell pedals, and HOTAS.",
                "📺 Perfect for Moonlight, Sunshine & Steam Deck streaming.",
                "📹 Zero-Copy Mode: Maximum throughput for webcams,",
                "   3D printers (Klipper), and flash drives.",
                "🤖 Full CLI Automation: USBAnywhere.exe --connect <IP> <ID>"
            ] if lang == "en" else [
                "🏎️ Modo Low-Latency: Cero retraso para mandos,",
                "   volantes SimRacing, pedaleras y joysticks HOTAS.",
                "📺 Ideal para streaming con Moonlight, Sunshine y Steam Deck.",
                "📹 Modo Zero-Copy: Máximo ancho de banda para cámaras,",
                "   impresoras 3D (Klipper) y almacenamiento masivo.",
                "🤖 Automatización CLI: USBAnywhere.exe --connect <IP> <ID>"
            ]
        y = 380
        for pt in pts:
            draw.text((1020, y), pt, font=font_body, fill="#F1F5F9")
            y += 56

    elif scene_id == "scene5":
        # Installation cards
        boxes = [
            ("📦 Windows Package Manager", "winget install USBAnywhere", "#3B82F6"),
            ("🍨 Scoop Package Manager", "scoop install https://raw.github.../usbanywhere.json", "#10B981"),
            ("🐳 Docker / HomeLab", "cd USBAnywhere/docker && docker compose up -d", "#06B6D4"),
            ("🌐 GitHub Releases", "https://github.com/oscarlosu-lab/USBAnywhere", "#F59E0B")
        ]
        y = 280
        font_bh = get_font(FONT_BOLD, 28)
        font_bc = get_font(FONT_CODE, 24)
        for h_text, cmd, border_color in boxes:
            draw.rounded_rectangle([(80, y), (1840, y + 120)], radius=15, fill="#1E293B", outline=border_color, width=2)
            draw.text((120, y + 20), h_text, font=font_bh, fill=border_color)
            draw.text((120, y + 68), cmd, font=font_bc, fill="#F8FAFC")
            y += 150

    # Bottom Footer
    font_footer = get_font(FONT_REGULAR, 22)
    draw.text((80, 1020), "GPL-3.0 License  •  github.com/oscarlosu-lab/USBAnywhere", font=font_footer, fill="#64748B")

    im.save(out_path, format="PNG")
    print(f"Slide created: {out_path}")

async def render_video_for_language(lang):
    print(f"\n=======================================================")
    print(f"🎬 RENDERING TUTORIAL VIDEO: [{lang.upper()}]")
    print(f"=======================================================")

    clips = []
    subtitles = []
    current_time = 0.0

    for i, scene in enumerate(SCENES):
        print(f"Processing scene {i+1}/{len(SCENES)}: {scene['id']} ({lang})...")
        # 1. Audio
        audio_file = await generate_audio(scene, lang)
        duration = get_audio_duration(audio_file) + 0.6 # slight tail padding

        # 2. Slide
        slide_file = os.path.join(TEMP_DIR, f"{scene['id']}_{lang}.png")
        create_slide(scene, lang, slide_file)

        # 3. Clip
        clip_file = os.path.join(TEMP_DIR, f"{scene['id']}_{lang}.mp4")
        cmd_clip = [
            "ffmpeg", "-y",
            "-loop", "1", "-i", slide_file,
            "-i", audio_file,
            "-c:v", "libx264", "-tune", "stillimage",
            "-t", str(duration),
            "-c:a", "aac", "-b:a", "192k",
            "-pix_fmt", "yuv420p",
            "-shortest",
            clip_file
        ]
        subprocess.run(cmd_clip, capture_output=True, check=True)
        clips.append(clip_file)

        # 4. Subtitle entry
        start_sec = current_time
        end_sec = current_time + duration
        subtitles.append((i + 1, start_sec, end_sec, scene[f"audio_{lang}"]))
        current_time += duration

    # Concat list file
    concat_list = os.path.join(TEMP_DIR, f"concat_{lang}.txt")
    with open(concat_list, "w", encoding="utf-8") as f:
        for c in clips:
            f.write(f"file '{c}'\n")

    output_video = os.path.join(OUTPUT_DIR, f"video_tutorial_{lang}.mp4")
    print(f"Concatenating all clips to: {output_video}...")
    cmd_concat = [
        "ffmpeg", "-y",
        "-f", "concat", "-safe", "0",
        "-i", concat_list,
        "-c", "copy",
        output_video
    ]
    subprocess.run(cmd_concat, capture_output=True, check=True)

    # Subtitles .srt file
    srt_path = os.path.join(OUTPUT_DIR, f"video_tutorial_{lang}.srt")
    with open(srt_path, "w", encoding="utf-8") as f:
        for idx, s_start, s_end, text in subtitles:
            def fmt_time(t):
                hrs = int(t // 3600)
                mins = int((t % 3600) // 60)
                secs = int(t % 60)
                millis = int((t - int(t)) * 1000)
                return f"{hrs:02d}:{mins:02d}:{secs:02d},{millis:03d}"
            f.write(f"{idx}\n{fmt_time(s_start)} --> {fmt_time(s_end)}\n{text}\n\n")

    print(f"✅ Video generated successfully: {output_video} ({current_time:.1f}s)")
    print(f"📝 Subtitles generated: {srt_path}")

async def main():
    await render_video_for_language("es")
    await render_video_for_language("en")
    print("\n🎉 BOTH SPANISH & ENGLISH VIDEO TUTORIALS GENERATED SUCCESSFULLY!")

if __name__ == "__main__":
    asyncio.run(main())
