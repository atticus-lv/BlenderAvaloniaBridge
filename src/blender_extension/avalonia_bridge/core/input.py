from __future__ import annotations

from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .controller import BridgeController, OverlayRect


KEYBOARD_EVENT_TYPES = {
    "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
    "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE",
    "SPACE", "TAB", "RET", "NUMPAD_ENTER", "BACK_SPACE", "DEL",
    "LEFT_ARROW", "RIGHT_ARROW", "UP_ARROW", "DOWN_ARROW", "HOME", "END", "ESC",
}

TITLE_BAR_HEIGHT = 28


def clamp(value: float, minimum: float, maximum: float) -> float:
    return max(minimum, min(maximum, value))


def overlay_rect(
    region_width: int,
    region_height: int,
    overlay_width: int,
    overlay_height: int,
    source_width: int | None = None,
    source_height: int | None = None,
    margin: int = 40,
    offset_x: int = 0,
    offset_y: int = 0,
    title_bar_height: int = TITLE_BAR_HEIGHT,
    display_scale: float = 1.0,
) -> "OverlayRect":
    display_scale = max(0.25, float(display_scale))
    scaled_title_bar_height = max(20, int(round(title_bar_height * display_scale)))
    desired_width = max(64.0, float(overlay_width) * display_scale)
    desired_height = max(64.0, float(overlay_height) * display_scale)
    max_width = max(64, region_width - margin * 2)
    max_height = max(64, region_height - margin * 2 - scaled_title_bar_height)
    fit_scale = min(1.0, max_width / max(1.0, desired_width), max_height / max(1.0, desired_height))
    draw_width = max(64, int(round(desired_width * fit_scale)))
    draw_height = max(64, int(round(desired_height * fit_scale)))
    total_height = draw_height + scaled_title_bar_height
    centered_x = int((region_width - draw_width) * 0.5)
    centered_y = int((region_height - total_height) * 0.5)
    x = clamp(centered_x + int(offset_x), margin, max(margin, region_width - margin - draw_width))
    y = clamp(centered_y + int(offset_y), margin, max(margin, region_height - margin - total_height))
    return {
        "x": x,
        "y": y,
        "width": draw_width,
        "height": total_height,
        "content_x": x,
        "content_y": y,
        "content_width": draw_width,
        "content_height": draw_height,
        "title_bar_height": scaled_title_bar_height,
        "title_bar_y": y + draw_height,
        "source_width": source_width if source_width is not None else overlay_width,
        "source_height": source_height if source_height is not None else overlay_height,
        "display_scale": display_scale,
        "fit_scale": fit_scale,
        "scale": display_scale * fit_scale,
    }


def contains_point(rect: "OverlayRect", x: int, y: int) -> bool:
    return rect["x"] <= x <= rect["x"] + rect["width"] and rect["y"] <= y <= rect["y"] + rect["height"]


def contains_content_point(rect: "OverlayRect", x: int, y: int) -> bool:
    return (
            rect["content_x"] <= x <= rect["content_x"] + rect["content_width"]
            and rect["content_y"] <= y <= rect["content_y"] + rect["content_height"]
    )


def contains_title_bar(rect: "OverlayRect", x: int, y: int) -> bool:
    return (
            rect["x"] <= x <= rect["x"] + rect["width"]
            and rect["title_bar_y"] <= y <= rect["title_bar_y"] + rect["title_bar_height"]
    )


def to_avalonia_coords(rect: "OverlayRect", x: int, y: int) -> tuple[int, int]:
    normalized_x = clamp((x - rect["content_x"]) / max(1, rect["content_width"]), 0.0, 0.999999)
    normalized_y = clamp((y - rect["content_y"]) / max(1, rect["content_height"]), 0.0, 0.999999)
    local_x = int(normalized_x * max(1, rect.get("source_width", rect["width"])))
    local_y_from_bottom = int(normalized_y * max(1, rect.get("source_height", rect["height"])))
    local_y = max(0, rect.get("source_height", rect["height"]) - 1 - local_y_from_bottom)
    return local_x, local_y


def modifiers_from_event(event) -> list[str]:
    modifiers = []
    if getattr(event, "shift", False):
        modifiers.append("shift")
    if getattr(event, "ctrl", False):
        modifiers.append("ctrl")
    if getattr(event, "alt", False):
        modifiers.append("alt")
    return modifiers


def wheel_delta(event_type: str) -> tuple[float, float]:
    if event_type == "WHEELUPMOUSE":
        return 0.0, 1.0
    if event_type == "WHEELDOWNMOUSE":
        return 0.0, -1.0
    return 0.0, 0.0


def mouse_button_name(event_type: str) -> str | None:
    if event_type == "LEFTMOUSE":
        return "left"
    if event_type == "RIGHTMOUSE":
        return "right"
    if event_type == "MIDDLEMOUSE":
        return "middle"
    return None


class InputRouter:
    def __init__(self, controller: "BridgeController") -> None:
        self._controller = controller

    def handle_event(self, context, event) -> bool:
        controller = self._controller
        rect = controller.get_overlay_rect(context)
        x = getattr(event, "mouse_region_x", -1)
        y = getattr(event, "mouse_region_y", -1)
        inside = contains_content_point(rect, x, y)
        in_title_bar = contains_title_bar(rect, x, y)
        modifiers = modifiers_from_event(event)
        button = mouse_button_name(event.type)
        pointer_event_types = {"MOUSEMOVE", "LEFTMOUSE", "RIGHTMOUSE", "MIDDLEMOUSE", "WHEELUPMOUSE", "WHEELDOWNMOUSE"}

        if event.type == "LEFTMOUSE" and event.value == "PRESS" and in_title_bar:
            if controller.capture_input:
                controller.release_capture_input()
            controller._drag_state = {
                "mouse_x": x,
                "mouse_y": y,
                "offset_x": controller._state.overlay_offset_x,
                "offset_y": controller._state.overlay_offset_y,
            }
            controller._replace_state(last_message="Dragging overlay")
            controller.tag_redraw()
            return True

        if event.type == "MOUSEMOVE" and controller._drag_state is not None:
            controller._replace_state(
                overlay_offset_x=int(controller._drag_state["offset_x"] + (x - controller._drag_state["mouse_x"])),
                overlay_offset_y=int(controller._drag_state["offset_y"] + (y - controller._drag_state["mouse_y"])),
            )
            controller.tag_redraw()
            return True

        if event.type == "LEFTMOUSE" and event.value == "RELEASE" and controller._drag_state is not None:
            controller._drag_state = None
            controller._replace_state(last_message="Overlay moved")
            controller.tag_redraw()
            return True

        if event.type in pointer_event_types:
            controller._sync_hover_capture(inside)

        if button is not None and event.value == "PRESS":
            if inside:
                px, py = to_avalonia_coords(rect, x, y)
                controller._flush_pointer_move()
                sent = controller.send_message(
                    {"type": "pointer_down", "seq": 3, "x": px, "y": py, "button": button, "modifiers": modifiers}
                )
                if sent:
                    controller._forwarded_buttons.add(button)
                controller._replace_state(last_message=f"PointerDown {px},{py}")
                controller.tag_redraw()
                return True
            return False

        if button is not None and event.value == "RELEASE" and (
                controller.capture_input or button in controller._forwarded_buttons):
            px, py = to_avalonia_coords(rect, x, y)
            controller._flush_pointer_move()
            controller.send_message(
                {"type": "pointer_up", "seq": 5, "x": px, "y": py, "button": button, "modifiers": modifiers}
            )
            controller._forwarded_buttons.discard(button)
            controller._replace_state(last_message=f"PointerUp {px},{py}")
            controller.tag_redraw()
            return True

        if event.type == "MOUSEMOVE" and controller.capture_input:
            px, py = to_avalonia_coords(rect, x, y)
            controller._diagnostics.record_pointer_move_received(controller._pending_pointer_move is not None)
            controller._pending_pointer_move = {"type": "pointer_move", "seq": 6, "x": px, "y": py,
                                                "modifiers": modifiers}
            return True

        if event.type in {"WHEELUPMOUSE", "WHEELDOWNMOUSE"} and controller.capture_input:
            px, py = to_avalonia_coords(rect, x, y)
            dx, dy = wheel_delta(event.type)
            controller._flush_pointer_move()
            controller.send_message(
                {"type": "wheel", "seq": 7, "x": px, "y": py, "delta_x": dx, "delta_y": dy, "modifiers": modifiers}
            )
            controller._replace_state(last_message=f"Wheel {dy:+.0f}")
            controller.tag_redraw()
            return True

        if controller.capture_input and event.type in KEYBOARD_EVENT_TYPES and event.value in {"PRESS", "RELEASE"}:
            message_type = "key_down" if event.value == "PRESS" else "key_up"
            controller._flush_pointer_move()
            controller.send_message(
                {"type": message_type, "seq": 8, "key": event.type, "text": event.unicode or "", "modifiers": modifiers}
            )
            if event.value == "PRESS" and event.unicode:
                controller.send_message({"type": "text", "seq": 9, "text": event.unicode})
            controller._replace_state(last_message=f"{message_type} {event.type}")
            controller.tag_redraw()
            return True

        return False
