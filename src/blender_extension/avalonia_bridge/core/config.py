from dataclasses import dataclass


@dataclass(frozen=True)
class BridgeConfig:
    executable_path: str = ""
    width: int = 1100
    height: int = 760
    render_scaling: float = 1.25
    window_mode: str = "headless"
    supports_business: bool = True
    supports_frames: bool = True
    supports_input: bool = True
    host: str = "127.0.0.1"
    show_overlay_debug: bool = False
    overlay_offset_x: int = 0
    overlay_offset_y: int = 0
