#!/usr/bin/env python3
"""
USBAnywhere YouTube-Style Video Tutorial Generator (Pro Neural Audio Edition)
Features:
- Real UI captures (MainWindow, SshDialog, VpnDialog, Light/Dark themes)
- ElevenLabs-grade natural neural voiceover with phonetic English term tuning in Spanish
- Synchronized subtitles and YouTube chapter cards
"""

import os
import sys
import asyncio
import subprocess
import edge_tts
from PIL import Image, ImageDraw, ImageFont

if hasattr(sys.stdout, 'reconfigure'):
    sys.stdout.reconfigure(encoding='utf-8')

REPO_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUTPUT_DIR = os.path.join(REPO_ROOT, "docs")
TEMP_DIR = os.path.join(REPO_ROOT, "release", "temp_youtube")
CAPTURES_DIR = os.path.join(REPO_ROOT, "docs", "assets", "captures")

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

# Phonetic tuning dictionary so Spanish TTS pronounces English terms with native English accent
PHONETIC_TUNING_ES = {
    "VirtualHere": "Vírchual-Jir",
    "USBAnywhere": "USB-Anywhere",
    "Raspberry Pi": "Rásberi Pái",
    "Plug & Play": "Plag and Pléi",
    "Plug and Play": "Plag and Pléi",
    "Moonlight": "Mún-lait",
    "Sunshine": "Sán-shain",
    "Steam Deck": "Stím Dek",
    "SimRacing": "Sim-Réis-ing",
    "Zero-Copy": "Cí-ro Cópi",
    "Low-Latency": "Lóu-Léitensi",
    "Force Feedback": "Fórs Fídbak",
    "Winget": "Uín-yet",
    "Scoop": "Scup",
    "Docker": "Dóker",
    "Tailscale": "Téil-skeil",
    "ZeroTier": "Zíro-Tíer",
    "WHQL": "Doble-ve Hache Cu Ele",
    "UDE": "U D E",
    "udev": "u-dev",
    "systemd": "sístem-di",
    "hotplug": "jot-plag",
    "NAT Traversal": "Nat Traversal"
}

def apply_phonetic_tuning(text, lang):
    if lang != "es":
        return text
    tuned = text
    for term, phonetic in PHONETIC_TUNING_ES.items():
        tuned = tuned.replace(term, phonetic)
    return tuned

CHAPTERS = [
    {
        "id": "ch1_intro",
        "num": "01",
        "chapter_es": "CAPÍTULO 1: ADIÓS A VIRTUALHERE",
        "chapter_en": "CHAPTER 1: THE VIRTUALHERE ALTERNATIVE",
        "title_es": "¿Por qué USBAnywhere supera a VirtualHere?",
        "title_en": "Why USBAnywhere Outperforms VirtualHere",
        "badge_es": "100% Free & Open Source  •  Ilimitados Dispositivos",
        "badge_en": "100% Free & Open Source  •  Unlimited Devices",
        "capture": "capture_01_mainwindow_dark.png",
        "audio_es": (
            "Bienvenidos a este tutorial completo de USBAnywhere, la alternativa libre y de código abierto para compartir periféricos USB por red. "
            "Si utilizas VirtualHere, sabrás que su versión gratuita solo te permite conectar un único dispositivo simultáneo y su licencia cuesta cuarenta y nueve dólares por servidor. "
            "Con USBAnywhere tienes dispositivos simultáneos ilimitados, cero licencias, drivers certificados por Microsoft y modos de latencia optimizados para gaming y simulación."
        ),
        "audio_en": (
            "Welcome to this complete tutorial of USBAnywhere, the free and open-source solution for sharing USB devices over your network. "
            "If you have used VirtualHere, you know that its free tier restricts you to a single simultaneous device, and its license costs forty-nine dollars per server. "
            "With USBAnywhere, you get unlimited simultaneous devices, zero licensing fees, Microsoft WHQL-certified drivers, and ultra-low latency profiles for gaming and simulation."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural",
        "callouts_es": [
            ("⚡ 0.8 ms", "Medidor de latencia en tiempo real"),
            ("Logitech G29 & Xbox", "Soporte multidispositivo simultáneo"),
            ("Drivers: Instalado ✓", "Drivers oficiales certificados WHQL")
        ],
        "callouts_en": [
            ("⚡ 0.8 ms", "Real-time connection latency meter"),
            ("Logitech G29 & Xbox", "Simultaneous multi-device streaming"),
            ("Drivers: Installed ✓", "Official Microsoft WHQL drivers")
        ]
    },
    {
        "id": "ch2_server",
        "num": "02",
        "chapter_es": "CAPÍTULO 2: SERVIDOR LINUX Y RASPBERRY PI",
        "chapter_en": "CHAPTER 2: LINUX & RASPBERRY PI SERVER",
        "title_es": "Instalación Plug & Play en 1-Click",
        "title_en": "1-Click Plug & Play Server Setup",
        "badge_es": "Auto-Export por udev  •  Zero Comandos Manuales",
        "badge_en": "udev Hotplug Auto-Export  •  Zero Manual Commands",
        "capture": "capture_02_ssh_dialog.png",
        "audio_es": (
            "Configurar el servidor en tu Raspberry Pi o máquina Linux es instantáneo. "
            "Desde la propia aplicación de Windows, pulsa en el botón SSH, introduce tu usuario y tu IP, y haz clic en el botón verde: Uno Clic Auto Instalar. "
            "Nuestro instalador oficial configura el servicio en segundo plano y activa una regla inteligente en udev que exporta automáticamente cualquier periférico USB en el momento exacto en que lo enchufas, igual que VirtualHere, pero sin límites."
        ),
        "audio_en": (
            "Configuring the server on your Raspberry Pi or Linux board is instantaneous. "
            "From the Windows client, click the SSH button, enter your user and IP address, and click the green One-Click Auto-Setup button. "
            "Our automated script installs the background daemon and enables an intelligent udev hotplug rule that automatically shares any USB peripheral the second you plug it in, with zero manual commands."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural",
        "callouts_es": [
            ("⚡ 1-Click Auto-Instalar", "Configura usbipd y udev automáticamente"),
            ("Terminal SSH Directa", "Comandos validados con InputValidator")
        ],
        "callouts_en": [
            ("⚡ 1-Click Auto-Setup", "Installs usbipd and udev hotplug in seconds"),
            ("Direct SSH Terminal", "Commands secured against injection")
        ]
    },
    {
        "id": "ch3_client",
        "num": "03",
        "chapter_es": "CAPÍTULO 3: CLIENTE WINDOWS Y CONEXIÓN",
        "chapter_en": "CHAPTER 3: WINDOWS CLIENT & WORKFLOW",
        "title_es": "Escaneo, Conexión y Gestión de Favoritos",
        "title_en": "Scanning, Connecting & Managing Favorites",
        "badge_es": "Autodescubrimiento  •  Reconexión Automática",
        "badge_en": "Auto-Discovery  •  Automatic Reconnection",
        "capture": "capture_01_mainwindow_dark.png",
        "audio_es": (
            "Al abrir USBAnywhere en Windows diez u once, la aplicación escanea tu red local automáticamente y detecta todos los servidores activos, mostrándote el ping exacto de cada uno. "
            "Para conectar cualquier periférico, simplemente haz doble clic sobre él o pulsa Conectar en el menú contextual. "
            "Además, puedes guardar tus periféricos en Favoritos para que se reconecten automáticamente cada vez que enciendas tu ordenador."
        ),
        "audio_en": (
            "When launching USBAnywhere on Windows ten or eleven, the app automatically scans your local subnet, discovers active servers, and displays their exact ping in milliseconds. "
            "To connect any peripheral, simply double-click it or right-click and select Connect. "
            "You can also save devices to Favorites so they automatically reconnect whenever your computer starts."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural",
        "callouts_es": [
            ("🔍 Escanear & Listar", "Detección instantánea de hubs en tu red"),
            ("⭐ Favoritos", "Auto-reconexión a prueba de reinicios"),
            ("📋 Log de Actividad", "Trazabilidad completa en tiempo real")
        ],
        "callouts_en": [
            ("🔍 Scan & List", "Instant hub discovery on your network"),
            ("⭐ Favorites", "Reboot-proof automatic reconnection"),
            ("📋 Activity Log", "Complete real-time event tracing")
        ]
    },
    {
        "id": "ch4_modes",
        "num": "04",
        "chapter_es": "CAPÍTULO 4: GAMING, SIMRACING Y RENDIMIENTO",
        "chapter_en": "CHAPTER 4: GAMING, SIMRACING & PERFORMANCE",
        "title_es": "Modos Low-Latency y Zero-Copy para Moonlight",
        "title_en": "Low-Latency & Zero-Copy for Moonlight Streamers",
        "badge_es": "Zero-Lag HID  •  Fuerza Háptica Completa",
        "badge_en": "Zero-Lag HID  •  Full Force Feedback Support",
        "capture": "capture_04_mainwindow_light.png",
        "audio_es": (
            "USBAnywhere cuenta con dos motores de recepción en el kernel de Windows. "
            "El modo Low-Latency utiliza callbacks directos diseñados para eliminar el retardo en mandos de Xbox y PlayStation, volantes de SimRacing, pedaleras de celda de carga y gafas de realidad virtual con vibración háptica completa. "
            "Es la solución perfecta para Moonlight, Sunshine y Steam Deck. "
            "Para cámaras web, impresoras tres D o memorias USB, el modo Zero-Copy garantiza la máxima tasa de transferencia sin saturar la CPU."
        ),
        "audio_en": (
            "USBAnywhere features two dedicated reception engines within the Windows kernel. "
            "Low-Latency mode uses direct callbacks designed to eliminate input lag on Xbox and PlayStation controllers, SimRacing steering wheels, load-cell pedals, and VR trackers with full force feedback. "
            "It is the ideal companion for Moonlight, Sunshine, and Steam Deck streaming. "
            "For webcams, 3D printers, or flash drives, Zero-Copy mode provides maximum throughput without CPU overhead."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural",
        "callouts_es": [
            ("🏎️ Low-Latency WSK", "Cero retardo para SimRacing y mandos"),
            ("📹 Zero-Copy", "Máxima velocidad para cámaras y 3D printers"),
            ("🤖 Flags CLI", "Automatización con --minimized y --connect")
        ],
        "callouts_en": [
            ("🏎️ Low-Latency WSK", "Zero lag for SimRacing wheels & controllers"),
            ("📹 Zero-Copy", "Maximum throughput for cameras & 3D printers"),
            ("🤖 CLI Automation", "Sunshine integration with --minimized & --connect")
        ]
    },
    {
        "id": "ch5_vpn_install",
        "num": "05",
        "chapter_es": "CAPÍTULO 5: CONEXIÓN POR INTERNET Y DESCARGA",
        "chapter_en": "CHAPTER 5: INTERNET VPN & INSTALLATION",
        "title_es": "Tailscale, ZeroTier, Winget y Scoop",
        "title_en": "Tailscale, ZeroTier, Winget & Scoop",
        "badge_es": "NAT Traversal  •  Instalación en 1 Comando",
        "badge_en": "NAT Traversal  •  1-Command Installation",
        "capture": "capture_03_vpn_dialog.png",
        "audio_es": (
            "¿Necesitas conectar dispositivos fuera de casa? El botón VPN se integra directamente con Tailscale y ZeroTier para conectarte a tus servidores a través de Internet sin abrir puertos en tu router. "
            "USBAnywhere está disponible tanto en versión instalable como portable, y puedes instalarlo en segundos con Winget o Scoop. "
            "Visita nuestro repositorio en GitHub, descarga la versión tres punto cero y di adiós para siempre a los límites de VirtualHere."
        ),
        "audio_en": (
            "Need to access USB peripherals away from home? The VPN dialog integrates directly with Tailscale and ZeroTier, letting you connect to remote servers over the Internet without opening router ports. "
            "USBAnywhere is available in both installer and hybrid portable packages, and installs in seconds using Winget or Scoop. "
            "Visit our official GitHub repository, download version three point zero, and leave VirtualHere paywalls behind forever."
        ),
        "voice_es": "es-ES-AlvaroNeural",
        "voice_en": "en-US-ChristopherNeural",
        "callouts_es": [
            ("📡 VPN Nativa", "Soporte integrado para Tailscale y ZeroTier"),
            ("📦 Winget & Scoop", "winget install USBAnywhere"),
            ("🐳 Docker Ready", "docker compose up -d para HomeLab y NAS")
        ],
        "callouts_en": [
            ("📡 Native VPN", "Built-in Tailscale and ZeroTier discovery"),
            ("📦 Winget & Scoop", "winget install USBAnywhere"),
            ("🐳 Docker Ready", "docker compose up -d for HomeLab and NAS")
        ]
    }
]

async def generate_speech(chapter, lang):
    raw_text = chapter[f"audio_{lang}"]
    # Apply phonetic tuning for Spanish voiceover so English technical terms sound native
    speech_text = apply_phonetic_tuning(raw_text, lang)
    voice = chapter[f"voice_{lang}"]
    out_file = os.path.join(TEMP_DIR, f"{chapter['id']}_{lang}.mp3")
    
    # Conversational pacing (-4% rate) gives that calm, natural YouTube creator tone
    comm = edge_tts.Communicate(speech_text, voice, rate="-4%", pitch="+0Hz")
    await comm.save(out_file)
    return out_file

def get_audio_len(file_path):
    cmd = [
        "ffprobe", "-v", "error", "-show_entries", "format=duration",
        "-of", "default=noprint_wrappers=1:nokey=1", file_path
    ]
    res = subprocess.run(cmd, capture_output=True, text=True, check=True)
    return float(res.stdout.strip())

def render_chapter_slide(chapter, lang, out_path):
    W, H = 1920, 1080
    bg = Image.new("RGB", (W, H), color="#0B0F19") # Deep modern slate
    draw = ImageDraw.Draw(bg)

    # 1. Top Bar: Category & Progress
    draw.rectangle([(0, 0), (W, 10)], fill="#06B6D4") # Cyan progress bar
    font_cat = get_font(FONT_BOLD, 20)
    font_brand = get_font(FONT_BOLD, 26)

    # Top Brand + Chapter Pill
    draw.text((60, 36), "🌐 USBAnywhere  •  v3.0.0", font=font_brand, fill="#38BDF8")
    chapter_text = chapter[f"chapter_{lang}"]
    draw.rounded_rectangle([(W - 480, 30), (W - 60, 75)], radius=10, fill="#1E293B", outline="#0EA5E9", width=1)
    draw.text((W - 460, 42), chapter_text, font=font_cat, fill="#06B6D4")

    # Title & Subtitle Banner
    title = chapter[f"title_{lang}"]
    badge = chapter[f"badge_{lang}"]
    font_title = get_font(FONT_BOLD, 46)
    font_badge = get_font(FONT_BOLD, 20)

    draw.text((60, 95), title, font=font_title, fill="#F8FAFC")
    # Badge Pill
    draw.rounded_rectangle([(60, 160), (60 + len(badge) * 13 + 40, 198)], radius=8, fill="#042F2E", outline="#10B981", width=1)
    draw.text((80, 168), badge, font=font_badge, fill="#34D399")

    # 2. Main Content: Real Screenshot (Left) + YouTube Highlights (Right)
    capture_file = os.path.join(CAPTURES_DIR, chapter["capture"])
    if os.path.exists(capture_file):
        try:
            cap_im = Image.open(capture_file)
            cap_im.thumbnail((900, 780), Image.Resampling.LANCZOS)
            cw, ch = cap_im.size
            draw.rounded_rectangle([(55, 230), (55 + cw + 10, 230 + ch + 10)], radius=14, fill="#1E293B", outline="#334155", width=2)
            bg.paste(cap_im, (60, 235))
        except Exception as e:
            print(f"Error loading capture {capture_file}: {e}")

    # 3. Right Side: Callout Cards & Feature Breakdown
    rx = 1010
    ry = 230
    rw = W - 60

    callouts = chapter[f"callouts_{lang}"]
    font_call_h = get_font(FONT_BOLD, 26)
    font_call_b = get_font(FONT_REGULAR, 20)

    for ch_title, ch_desc in callouts:
        draw.rounded_rectangle([(rx, ry), (rw, ry + 110)], radius=12, fill="#131D31", outline="#0284C7", width=2)
        # Accent icon bar on card left
        draw.rounded_rectangle([(rx, ry), (rx + 8, ry + 110)], radius=4, fill="#38BDF8")
        draw.text((rx + 28, ry + 22), ch_title, font=font_call_h, fill="#F1F5F9")
        draw.text((rx + 28, ry + 62), ch_desc, font=font_call_b, fill="#94A3B8")
        ry += 135

    # YouTube Pro Tip Card at bottom right
    tip_title = "💡 YOUTUBE PRO TIP" if lang == "en" else "💡 CONSEJO PRO"
    tip_text = (
        "No complex network setup required: USBAnywhere handles NAT traversal, UDE driver nodes and auto-reconnection out of the box."
        if lang == "en" else
        "Sin configuraciones complejas: USBAnywhere gestiona NAT traversal, controladores UDE y auto-reconexión automáticamente."
    )
    draw.rounded_rectangle([(rx, 800), (rw, 950)], radius=12, fill="#1A1825", outline="#A855F7", width=2)
    draw.rounded_rectangle([(rx, 800), (rx + 8, 950)], radius=4, fill="#C084FC")
    draw.text((rx + 28, 818), tip_title, font=get_font(FONT_BOLD, 22), fill="#E9D5FF")
    draw.text((rx + 28, 856), tip_text, font=get_font(FONT_REGULAR, 19), fill="#CBD5E1")

    # Bottom Timeline Bar
    font_ft = get_font(FONT_REGULAR, 18)
    draw.text((60, 1040), "USBAnywhere Official Tutorial  •  GPL-3.0  •  github.com/oscarlosu-lab/USBAnywhere", font=font_ft, fill="#64748B")

    bg.save(out_path, format="PNG")
    print(f"Rendered slide: {out_path}")

async def render_full_tutorial(lang):
    print(f"\n=======================================================")
    print(f"🎬 RENDERING YOUTUBE-STYLE FULL TUTORIAL: [{lang.upper()}]")
    print(f"=======================================================")

    clips = []
    subtitles = []
    current_time = 0.0

    for i, ch in enumerate(CHAPTERS):
        print(f"Generating Chapter {i+1}/{len(CHAPTERS)}: {ch['id']} ({lang})...")
        audio_file = await generate_speech(ch, lang)
        duration = get_audio_len(audio_file) + 0.8 # small pause between chapters

        slide_file = os.path.join(TEMP_DIR, f"{ch['id']}_{lang}.png")
        render_chapter_slide(ch, lang, slide_file)

        clip_file = os.path.join(TEMP_DIR, f"{ch['id']}_{lang}.mp4")
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

        # Subtitle entry keeps official English terms for clean on-screen display
        start_sec = current_time
        end_sec = current_time + duration
        subtitles.append((i + 1, start_sec, end_sec, ch[f"audio_{lang}"]))
        current_time += duration

    # Concat clips
    concat_list = os.path.join(TEMP_DIR, f"yt_concat_{lang}.txt")
    with open(concat_list, "w", encoding="utf-8") as f:
        for c in clips:
            f.write(f"file '{c}'\n")

    output_video = os.path.join(OUTPUT_DIR, f"youtube_tutorial_{lang}.mp4")
    print(f"Concatenating video to: {output_video}...")
    cmd_concat = [
        "ffmpeg", "-y",
        "-f", "concat", "-safe", "0",
        "-i", concat_list,
        "-c", "copy",
        output_video
    ]
    subprocess.run(cmd_concat, capture_output=True, check=True)

    # Subtitles
    srt_path = os.path.join(OUTPUT_DIR, f"youtube_tutorial_{lang}.srt")
    with open(srt_path, "w", encoding="utf-8") as f:
        for idx, s_start, s_end, text in subtitles:
            def fmt_time(t):
                hrs = int(t // 3600)
                mins = int((t % 3600) // 60)
                secs = int(t % 60)
                millis = int((t - int(t)) * 1000)
                return f"{hrs:02d}:{mins:02d}:{secs:02d},{millis:03d}"
            f.write(f"{idx}\n{fmt_time(s_start)} --> {fmt_time(s_end)}\n{text}\n\n")

    print(f"✅ YouTube Video generated: {output_video} ({current_time:.1f}s)")
    print(f"📝 Subtitles: {srt_path}")

async def main():
    await render_full_tutorial("es")
    await render_full_tutorial("en")
    print("\n🎉 NATURAL YOUTUBE TUTORIALS WITH REAL CAPTURES GENERATED SUCCESSFULLY!")

if __name__ == "__main__":
    asyncio.run(main())
