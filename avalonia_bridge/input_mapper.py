KEYBOARD_EVENT_TYPES = {
    "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
    "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
    "SPACE", "TAB", "RET", "NUMPAD_ENTER", "BACK_SPACE", "DEL",
    "LEFT_ARROW", "RIGHT_ARROW", "UP_ARROW", "DOWN_ARROW", "HOME", "END", "ESC",
}


def overlay_rect(region_width, region_height, overlay_width, overlay_height, margin=40):
    max_width = max(64, region_width - margin * 2)
    max_height = max(64, region_height - margin * 2)
    scale = min(1.0, max_width / max(1, overlay_width), max_height / max(1, overlay_height))
    draw_width = max(64, int(round(overlay_width * scale)))
    draw_height = max(64, int(round(overlay_height * scale)))
    return {
        "x": max(margin, int((region_width - draw_width) * 0.5)),
        "y": max(margin, int((region_height - draw_height) * 0.5)),
        "width": draw_width,
        "height": draw_height,
        "source_width": overlay_width,
        "source_height": overlay_height,
        "scale": scale,
    }


def contains_point(rect, x, y):
    return rect["x"] <= x <= rect["x"] + rect["width"] and rect["y"] <= y <= rect["y"] + rect["height"]


def clamp(value, minimum, maximum):
    return max(minimum, min(maximum, value))


def to_avalonia_coords(rect, x, y):
    normalized_x = clamp((x - rect["x"]) / max(1, rect["width"]), 0.0, 0.999999)
    normalized_y = clamp((y - rect["y"]) / max(1, rect["height"]), 0.0, 0.999999)
    local_x = int(normalized_x * max(1, rect.get("source_width", rect["width"])))
    local_y_from_bottom = int(normalized_y * max(1, rect.get("source_height", rect["height"])))
    local_y = max(0, rect.get("source_height", rect["height"]) - 1 - local_y_from_bottom)
    return local_x, local_y


def modifiers_from_event(event):
    modifiers = []
    if getattr(event, "shift", False):
        modifiers.append("shift")
    if getattr(event, "ctrl", False):
        modifiers.append("ctrl")
    if getattr(event, "alt", False):
        modifiers.append("alt")
    return modifiers


def wheel_delta(event_type):
    if event_type == "WHEELUPMOUSE":
        return 0.0, 1.0
    if event_type == "WHEELDOWNMOUSE":
        return 0.0, -1.0
    return 0.0, 0.0
