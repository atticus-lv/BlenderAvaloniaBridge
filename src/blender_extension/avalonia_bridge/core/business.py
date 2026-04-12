from __future__ import annotations

from dataclasses import dataclass

import bpy


OBJECT_RNA_TYPE = "bpy.types.Object"
SCENE_RNA_TYPE = "bpy.types.Scene"
BUSINESS_VERSION = 1


def _result_list(operator_result):
    if isinstance(operator_result, set):
        return sorted(str(item) for item in operator_result)
    if isinstance(operator_result, (list, tuple)):
        return [str(item) for item in operator_result]
    if operator_result is None:
        return []
    return [str(operator_result)]


def _object_ref(obj):
    return {
        "rna_type": OBJECT_RNA_TYPE,
        "id_type": "OBJECT",
        "name": obj.name,
        "session_uid": getattr(obj, "session_uid", 0),
    }


@dataclass
class BusinessError:
    code: str
    message: str
    details: object | None = None

    def to_dict(self):
        payload = {
            "code": self.code,
            "message": self.message,
        }
        if self.details is not None:
            payload["details"] = self.details
        return payload


@dataclass
class BusinessResponse:
    business_version: int
    message_id: int
    reply_to: int
    ok: bool
    payload: object | None = None
    error: BusinessError | None = None

    def to_header(self):
        payload = {
            "type": "business_response",
            "business_version": int(self.business_version),
            "message_id": int(self.message_id),
            "reply_to": int(self.reply_to),
            "ok": bool(self.ok),
        }
        if self.payload is not None:
            payload["payload"] = self.payload
        if self.error is not None:
            payload["error"] = self.error.to_dict()
        return payload


@dataclass
class BusinessRequest:
    business_version: int
    message_id: int
    name: str
    payload: object | None

    @classmethod
    def response(
        cls,
        reply_to,
        payload=None,
        ok=True,
        error=None,
        business_version=BUSINESS_VERSION,
        message_id=0,
    ):
        return BusinessResponse(
            business_version=business_version,
            message_id=message_id,
            reply_to=reply_to,
            ok=ok,
            payload=payload,
            error=error,
        )

    @classmethod
    def from_header(cls, header):
        return cls(
            business_version=int(header.get("business_version", BUSINESS_VERSION)),
            message_id=int(header.get("message_id", 0)),
            name=header.get("name", ""),
            payload=header.get("payload"),
        )


class BusinessEndpoint:
    def invoke(self, request):
        raise NotImplementedError


def _success_response(reply_to, payload=None, *, business_version=BUSINESS_VERSION, message_id=0):
    return BusinessResponse(
        business_version=business_version,
        message_id=message_id,
        reply_to=reply_to,
        ok=True,
        payload=payload,
    )


def _error_response(code, message, reply_to, *, details=None, business_version=BUSINESS_VERSION, message_id=0):
    return BusinessResponse(
        business_version=business_version,
        message_id=message_id,
        reply_to=reply_to,
        ok=False,
        error=BusinessError(code=code, message=message, details=details),
    )


def _legacy_to_business_response(reply_to, legacy_response, *, business_version=BUSINESS_VERSION):
    ok = bool(legacy_response.get("ok", False))
    payload = {
        key: value
        for key, value in legacy_response.items()
        if key not in {"type", "seq", "ok", "message"}
    }
    if ok:
        return _success_response(
            reply_to,
            payload=payload,
            business_version=business_version,
        )
    return _error_response(
        "business_request_failed",
        legacy_response.get("message", "Business request failed."),
        reply_to,
        details=payload or None,
        business_version=business_version,
    )


class OperatorBridge:
    ALLOWLIST = {
        "mesh.primitive_cube_add",
        "object.duplicate_move",
        "view3d.view_selected",
    }

    def execute(self, request):
        seq = int(request.get("seq", 0))
        operator_name = request.get("operator", "")
        execution_context = request.get("execution_context", "EXEC_DEFAULT") or "EXEC_DEFAULT"
        properties = request.get("properties") or {}
        target = request.get("target") or {}

        if operator_name not in self.ALLOWLIST:
            return {
                "type": "operator_result",
                "seq": seq,
                "ok": False,
                "message": f"Operator is not allowed: {operator_name}",
                "operator": operator_name,
                "result": ["CANCELLED"],
            }

        try:
            operator = self._resolve_operator(operator_name)
            result = self._invoke_operator(operator, execution_context, properties, target)
            return {
                "type": "operator_result",
                "seq": seq,
                "ok": True,
                "message": "",
                "operator": operator_name,
                "target": target or None,
                "result": _result_list(result),
            }
        except Exception as exc:
            return {
                "type": "operator_result",
                "seq": seq,
                "ok": False,
                "message": str(exc),
                "operator": operator_name,
                "target": target or None,
                "result": ["CANCELLED"],
            }

    def _resolve_operator(self, operator_name):
        current = bpy.ops
        for part in operator_name.split("."):
            current = getattr(current, part)
        return current

    def _invoke_operator(self, operator, execution_context, properties, target):
        if not target:
            return operator(execution_context, **properties)

        obj, error = ObjectPropertyBridge.resolve_object(target)
        if obj is None:
            raise RuntimeError(error)

        view_layer = bpy.context.view_layer
        objects = getattr(view_layer, "objects", None)
        previous_active = getattr(objects, "active", None)
        previous_selection = [selected for selected in objects if selected.select_get()] if objects is not None else []
        for selected in previous_selection:
            selected.select_set(False)
        obj.select_set(True)
        if objects is not None:
            objects.active = obj
        try:
            if self._is_view3d_operator(operator):
                return self._invoke_view3d_operator(operator, execution_context, properties)
            return operator(execution_context, **properties)
        finally:
            self._restore_selection(view_layer, previous_active, previous_selection)

    def _is_view3d_operator(self, operator):
        identifier = getattr(operator, "idname_py", lambda: "")()
        return identifier == "view3d.view_selected"

    def _invoke_view3d_operator(self, operator, execution_context, properties):
        context_override = self._find_view3d_override()
        if context_override is None:
            raise RuntimeError("No VIEW_3D area available for view3d.view_selected.")

        with bpy.context.temp_override(**context_override):
            return operator(execution_context, **properties)

    def _find_view3d_override(self):
        window_manager = bpy.context.window_manager
        for window in getattr(window_manager, "windows", []):
            screen = getattr(window, "screen", None)
            if screen is None:
                continue
            for area in getattr(screen, "areas", []):
                if getattr(area, "type", "") != "VIEW_3D":
                    continue
                for region in getattr(area, "regions", []):
                    if getattr(region, "type", "") == "WINDOW":
                        return {
                            "window": window,
                            "screen": screen,
                            "area": area,
                            "region": region,
                        }
        return None

    def _restore_selection(self, view_layer, previous_active, previous_selection):
        objects = getattr(view_layer, "objects", None)
        if objects is None:
            return
        for selected in objects:
            selected.select_set(False)
        for selected in previous_selection:
            if bpy.data.objects.get(selected.name) is not None:
                selected.select_set(True)
        objects.active = previous_active if previous_active and bpy.data.objects.get(previous_active.name) is not None else None


class ObjectPropertyBridge:
    SUPPORTED_PATHS = {
        "name",
        "location",
    }

    @staticmethod
    def resolve_object(target):
        if not isinstance(target, dict):
            return None, "Target must be an rna_ref object."
        if target.get("rna_type") not in {None, OBJECT_RNA_TYPE}:
            return None, f"Unsupported target rna_type: {target.get('rna_type')}"

        scene = bpy.context.scene
        session_uid = target.get("session_uid")
        if session_uid:
            for obj in getattr(scene, "objects", []):
                if getattr(obj, "session_uid", None) == session_uid:
                    return obj, ""

        name = target.get("name")
        if name:
            obj = bpy.data.objects.get(name)
            if obj is not None:
                return obj, ""

        return None, f"Object not found: {name or session_uid}"

    def execute_get(self, request):
        seq = int(request.get("seq", 0))
        target = request.get("target") or {}
        data_path = request.get("data_path", "")
        obj, error = self.resolve_object(target)
        if obj is None:
            return {
                "type": "property_result",
                "seq": seq,
                "ok": False,
                "message": error,
                "target": target,
                "data_path": data_path,
            }
        if data_path not in self.SUPPORTED_PATHS:
            return {
                "type": "property_result",
                "seq": seq,
                "ok": False,
                "message": f"Unsupported data_path: {data_path}",
                "target": target,
                "data_path": data_path,
            }

        value = obj.name if data_path == "name" else [float(component) for component in obj.location]
        return {
            "type": "property_result",
            "seq": seq,
            "ok": True,
            "message": "",
            "target": _object_ref(obj),
            "data_path": data_path,
            "value": value,
        }

    def execute_set(self, request):
        seq = int(request.get("seq", 0))
        target = request.get("target") or {}
        data_path = request.get("data_path", "")
        obj, error = self.resolve_object(target)
        if obj is None:
            return {
                "type": "property_result",
                "seq": seq,
                "ok": False,
                "message": error,
                "target": target,
                "data_path": data_path,
            }
        if data_path not in self.SUPPORTED_PATHS:
            return {
                "type": "property_result",
                "seq": seq,
                "ok": False,
                "message": f"Unsupported data_path: {data_path}",
                "target": target,
                "data_path": data_path,
            }

        try:
            if data_path == "name":
                value = request.get("value")
                if not isinstance(value, str):
                    raise TypeError("name expects a string value.")
                obj.name = value
            elif data_path == "location":
                value = request.get("value")
                if not isinstance(value, (list, tuple)) or len(value) != 3:
                    raise TypeError("location expects a [x, y, z] array.")
                obj.location = [float(component) for component in value]
        except Exception as exc:
            return {
                "type": "property_result",
                "seq": seq,
                "ok": False,
                "message": str(exc),
                "target": _object_ref(obj),
                "data_path": data_path,
            }

        return self.execute_get(
            {
                "seq": seq,
                "target": _object_ref(obj),
                "data_path": data_path,
            }
        )


class ObjectCollectionBridge:
    def execute(self, request):
        seq = int(request.get("seq", 0))
        owner = request.get("owner") or {}
        data_path = request.get("data_path", "")
        if owner.get("rna_type") not in {None, SCENE_RNA_TYPE}:
            return {
                "type": "collection_result",
                "seq": seq,
                "ok": False,
                "message": f"Unsupported owner rna_type: {owner.get('rna_type')}",
                "owner": owner,
                "data_path": data_path,
                "item_rna_type": OBJECT_RNA_TYPE,
                "items": [],
            }
        if data_path != "objects":
            return {
                "type": "collection_result",
                "seq": seq,
                "ok": False,
                "message": f"Unsupported data_path: {data_path}",
                "owner": owner,
                "data_path": data_path,
                "item_rna_type": OBJECT_RNA_TYPE,
                "items": [],
            }

        scene = bpy.context.scene
        active_object = getattr(getattr(bpy.context, "view_layer", None), "objects", None)
        active_object = getattr(active_object, "active", None)
        items = []
        for obj in getattr(scene, "objects", []):
            items.append(
                {
                    "rna_ref": _object_ref(obj),
                    "label": obj.name,
                    "meta": {
                        "object_type": getattr(obj, "type", ""),
                        "is_active": active_object is obj,
                    },
                }
            )

        scene_ref = {
            "rna_type": SCENE_RNA_TYPE,
            "id_type": "SCENE",
            "name": getattr(scene, "name", "Scene"),
            "session_uid": getattr(scene, "session_uid", 0),
        }
        return {
            "type": "collection_result",
            "seq": seq,
            "ok": True,
            "message": "",
            "owner": scene_ref,
            "data_path": "objects",
            "item_rna_type": OBJECT_RNA_TYPE,
            "items": items,
        }


class DefaultBusinessEndpoint(BusinessEndpoint):
    def __init__(self):
        self.operators = OperatorBridge()
        self.properties = ObjectPropertyBridge()
        self.collections = ObjectCollectionBridge()

    def invoke(self, request):
        if int(request.business_version) != BUSINESS_VERSION:
            return _error_response(
                "business_version_unsupported",
                f"Unsupported business version: {request.business_version}",
                request.message_id,
            )

        payload = request.payload if isinstance(request.payload, dict) else {}
        if request.name == "scene.objects.list":
            legacy = self.collections.execute(
                {
                    "seq": request.message_id,
                    "owner": payload.get("owner") or {"rna_type": SCENE_RNA_TYPE},
                    "data_path": payload.get("data_path", "objects"),
                }
            )
            return _legacy_to_business_response(request.message_id, legacy, business_version=request.business_version)

        if request.name == "object.property.get":
            if not isinstance(payload.get("target"), dict) or not isinstance(payload.get("data_path"), str):
                return _error_response(
                    "invalid_payload",
                    "object.property.get expects target and data_path.",
                    request.message_id,
                )
            legacy = self.properties.execute_get(
                {
                    "seq": request.message_id,
                    "target": payload.get("target"),
                    "data_path": payload.get("data_path"),
                }
            )
            return _legacy_to_business_response(request.message_id, legacy, business_version=request.business_version)

        if request.name == "object.property.set":
            if not isinstance(payload.get("target"), dict) or not isinstance(payload.get("data_path"), str) or "value" not in payload:
                return _error_response(
                    "invalid_payload",
                    "object.property.set expects target, data_path, and value.",
                    request.message_id,
                )
            legacy = self.properties.execute_set(
                {
                    "seq": request.message_id,
                    "target": payload.get("target"),
                    "data_path": payload.get("data_path"),
                    "value": payload.get("value"),
                }
            )
            return _legacy_to_business_response(request.message_id, legacy, business_version=request.business_version)

        if request.name == "operator.call":
            if not isinstance(payload.get("operator"), str):
                return _error_response(
                    "invalid_payload",
                    "operator.call expects operator.",
                    request.message_id,
                )
            legacy = self.operators.execute(
                {
                    "seq": request.message_id,
                    "operator": payload.get("operator"),
                    "execution_context": payload.get("execution_context", "EXEC_DEFAULT"),
                    "target": payload.get("target"),
                    "properties": payload.get("properties") or {},
                }
            )
            return _legacy_to_business_response(request.message_id, legacy, business_version=request.business_version)

        return _error_response(
            "unsupported_business_request",
            f"Unsupported business request: {request.name}",
            request.message_id,
        )


class BusinessBridgeHandler:
    def handle_packet(self, header, payload):
        raise NotImplementedError


class EndpointBusinessBridgeHandler(BusinessBridgeHandler):
    def __init__(self, endpoint=None):
        self.endpoint = endpoint or DefaultBusinessEndpoint()

    def handle_packet(self, header, payload):
        request = BusinessRequest.from_header(header)
        response = self.endpoint.invoke(request)
        return response.to_header()


class DefaultBusinessBridgeHandler(EndpointBusinessBridgeHandler):
    def __init__(self):
        super().__init__(DefaultBusinessEndpoint())


class BlenderBusinessBridge(DefaultBusinessEndpoint):
    def handle(self, request):
        return self.invoke(request)


__all__ = [
    "BUSINESS_VERSION",
    "BusinessError",
    "BusinessRequest",
    "BusinessResponse",
    "BusinessEndpoint",
    "DefaultBusinessEndpoint",
    "BusinessBridgeHandler",
    "DefaultBusinessBridgeHandler",
    "EndpointBusinessBridgeHandler",
    "BlenderBusinessBridge",
]
