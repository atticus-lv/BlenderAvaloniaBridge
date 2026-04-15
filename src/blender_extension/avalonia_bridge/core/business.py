from __future__ import annotations

import json
import threading
import weakref
from collections.abc import Callable
from contextlib import nullcontext
from dataclasses import dataclass
from typing import Any

import bpy

PROTOCOL_VERSION = 1
SCHEMA_VERSION = 1

JsonObject = dict[str, Any]
JsonArray = list[Any]


def _json_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=True)


def _result_list(operator_result: Any) -> list[str]:
    if isinstance(operator_result, set):
        return sorted(str(item) for item in operator_result)
    if isinstance(operator_result, (list, tuple)):
        return [str(item) for item in operator_result]
    if operator_result is None:
        return []
    return [str(operator_result)]


def _schema_error(code: str, message: str, *, details: object | None = None) -> BusinessError:
    return BusinessError(code=code, message=message, details=details)


class UnsupportedBusinessRequestError(ValueError):
    pass


class UnsupportedValueTypeError(ValueError):
    pass


@dataclass
class BusinessError:
    code: str
    message: str
    details: object | None = None

    def to_dict(self) -> JsonObject:
        payload = {
            "code": self.code,
            "message": self.message,
        }
        if self.details is not None:
            payload["details"] = self.details
        return payload


@dataclass
class BusinessResponse:
    protocol_version: int
    schema_version: int
    message_id: int
    reply_to: int
    ok: bool
    payload: object | None = None
    error: BusinessError | None = None

    def to_header(self) -> JsonObject:
        payload = {
            "type": "business_response",
            "protocolVersion": int(self.protocol_version),
            "schemaVersion": int(self.schema_version),
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
class BusinessEvent:
    protocol_version: int
    schema_version: int
    name: str
    payload: object | None = None

    def to_header(self) -> JsonObject:
        header = {
            "type": "business_event",
            "protocolVersion": int(self.protocol_version),
            "schemaVersion": int(self.schema_version),
            "name": self.name,
        }
        if self.payload is not None:
            header["payload"] = self.payload
        return header


@dataclass
class BusinessRequest:
    protocol_version: int
    schema_version: int
    message_id: int
    name: str
    payload: object | None

    @classmethod
    def response(
        cls,
        reply_to: int,
        payload: object | None = None,
        ok: bool = True,
        error: BusinessError | None = None,
        protocol_version: int = PROTOCOL_VERSION,
        schema_version: int = SCHEMA_VERSION,
        message_id: int = 0,
    ) -> BusinessResponse:
        return BusinessResponse(
            protocol_version=protocol_version,
            schema_version=schema_version,
            message_id=message_id,
            reply_to=reply_to,
            ok=ok,
            payload=payload,
            error=error,
        )

    @classmethod
    def from_header(cls, header: JsonObject) -> BusinessRequest:
        return cls(
            protocol_version=int(header.get("protocolVersion", 0)),
            schema_version=int(header.get("schemaVersion", 0)),
            message_id=int(header.get("message_id", 0)),
            name=header.get("name", ""),
            payload=header.get("payload"),
        )


class BusinessEndpoint:
    def invoke(self, request: BusinessRequest) -> BusinessResponse:
        raise NotImplementedError


def _success_response(
    reply_to: int,
    payload: object | None = None,
    *,
    protocol_version: int = PROTOCOL_VERSION,
    schema_version: int = SCHEMA_VERSION,
    message_id: int = 0,
) -> BusinessResponse:
    return BusinessResponse(
        protocol_version=protocol_version,
        schema_version=schema_version,
        message_id=message_id,
        reply_to=reply_to,
        ok=True,
        payload=payload,
    )


def _error_response(
    code: str,
    message: str,
    reply_to: int,
    *,
    details: object | None = None,
    protocol_version: int = PROTOCOL_VERSION,
    schema_version: int = SCHEMA_VERSION,
    message_id: int = 0,
) -> BusinessResponse:
    return BusinessResponse(
        protocol_version=protocol_version,
        schema_version=schema_version,
        message_id=message_id,
        reply_to=reply_to,
        ok=False,
        error=BusinessError(code=code, message=message, details=details),
    )


@dataclass
class _ResolvedPath:
    path: str
    value: object
    parent: object | None
    segment: object | None


class PathResolver:
    def resolve(self, path: str) -> _ResolvedPath:
        if not isinstance(path, str) or not path.startswith("bpy."):
            raise ValueError("Path must start with 'bpy.'.")

        index = 0
        current = bpy
        parent = None
        segment = None

        while index < len(path):
            if path.startswith("bpy", index):
                index += 3
                continue

            token = path[index]
            if token == ".":
                index += 1
                start = index
                while index < len(path) and (path[index].isalnum() or path[index] == "_"):
                    index += 1
                if start == index:
                    raise ValueError(f"Invalid path near '{path[index:]}'")
                segment = path[start:index]
                parent = current
                current = getattr(current, segment)
                continue

            if token == "[":
                end = path.find("]", index)
                if end < 0:
                    raise ValueError("Unterminated index segment.")
                raw_segment = path[index + 1:end]
                if raw_segment.startswith("\"") and raw_segment.endswith("\""):
                    segment = json.loads(raw_segment)
                else:
                    segment = int(raw_segment)
                parent = current
                current = current[segment]
                index = end + 1
                continue

            raise ValueError(f"Unsupported token '{token}' in path '{path}'.")

        return _ResolvedPath(path=path, value=current, parent=parent, segment=segment)


def _is_collection_value(value: object) -> bool:
    if isinstance(value, dict):
        return True
    if isinstance(value, (str, bytes, bytearray)):
        return False
    if isinstance(value, (list, tuple)):
        return True
    return hasattr(value, "__iter__") and hasattr(value, "__getitem__")


def _sequence_items(value: object) -> list[Any] | None:
    if isinstance(value, (str, bytes, bytearray, dict)):
        return None
    if isinstance(value, (list, tuple)):
        return list(value)
    if not hasattr(value, "__len__") or not hasattr(value, "__getitem__"):
        return None
    try:
        return [value[index] for index in range(len(value))]
    except Exception:
        return None


def _flatten_scalar_sequence(value: object) -> list[bool | int | float] | None:
    if isinstance(value, bool):
        return [value]
    if isinstance(value, (int, float)):
        return [value]
    items = _sequence_items(value)
    if items is None:
        return None

    flattened = []
    for item in items:
        nested = _flatten_scalar_sequence(item)
        if nested is None:
            return None
        flattened.extend(nested)
    return flattened


def _property_definition(resolved: _ResolvedPath) -> object | None:
    if resolved.parent is None or not isinstance(resolved.segment, str):
        return None
    bl_rna = getattr(resolved.parent, "bl_rna", None)
    properties = getattr(bl_rna, "properties", None)
    if properties is None:
        return None
    try:
        return properties[resolved.segment]
    except Exception:
        return None


def _enum_items(property_definition: object | None) -> list[str] | None:
    if getattr(property_definition, "type", None) != "ENUM":
        return None
    items = getattr(property_definition, "enum_items", None)
    if items is None:
        return None
    return [getattr(item, "identifier", str(item)) for item in items]


def _fixed_type(property_definition: object | None) -> str | None:
    fixed_type = getattr(property_definition, "fixed_type", None)
    if fixed_type is None:
        return None
    return getattr(fixed_type, "identifier", None)


def _value_type_from_property(property_definition: object | None) -> str | None:
    property_type = getattr(property_definition, "type", None)
    is_array = bool(getattr(property_definition, "is_array", False))
    is_enum_flag = bool(getattr(property_definition, "is_enum_flag", False))
    if property_type == "BOOLEAN":
        return "bool_array" if is_array else "bool"
    if property_type == "INT":
        return "int_array" if is_array else "int"
    if property_type == "FLOAT":
        return "float_array" if is_array else "float"
    if property_type == "STRING":
        return "string_array" if is_array else "string"
    if property_type == "ENUM":
        return "enum_flags" if is_enum_flag else "enum"
    if property_type == "POINTER":
        return "rna_ref"
    if property_type == "COLLECTION":
        return "collection"
    return None


def _safe_getattr(value: object, name: str) -> object | None:
    try:
        return getattr(value, name)
    except Exception:
        return None


def _module_path(value: object) -> str | None:
    path_from_module = _safe_getattr(value, "path_from_module")
    if not callable(path_from_module):
        return None

    try:
        module_path = path_from_module()
    except Exception:
        return None

    return module_path if isinstance(module_path, str) and module_path else None


def _is_rna_value(value: object) -> bool:
    if _safe_getattr(value, "bl_rna") is not None:
        return True
    if _safe_getattr(value, "rna_type") is not None:
        return True
    return _module_path(value) is not None


def _serialize_value(value: object, path: str = "", property_definition: object | None = None) -> object | None:
    if value is None:
        return None
    if isinstance(value, (str, int, float, bool)):
        return value
    if property_definition is not None:
        property_type = getattr(property_definition, "type", None)
        is_array = bool(getattr(property_definition, "is_array", False))
        if property_type in {"BOOLEAN", "INT", "FLOAT"} and is_array:
            flattened = _flatten_scalar_sequence(value)
            if flattened is not None:
                return flattened
        if property_type == "ENUM":
            if bool(getattr(property_definition, "is_enum_flag", False)):
                if isinstance(value, set):
                    return sorted(str(item) for item in value)
                if isinstance(value, (list, tuple)):
                    return [str(item) for item in value]
            return str(value)
        if property_type == "POINTER":
            return _to_item_ref(value, path) if value is not None else None
        if property_type == "COLLECTION":
            return None
    sequence_items = _sequence_items(value)
    if sequence_items is not None:
        return [_serialize_value(item) for item in sequence_items]
    if isinstance(value, dict):
        return {str(key): _serialize_value(item) for key, item in value.items()}
    if _is_rna_value(value):
        return _to_item_ref(value, path)
    return value


def _value_type(value: object, property_definition: object | None = None) -> str:
    if property_definition is not None:
        explicit_value_type = _value_type_from_property(property_definition)
        if explicit_value_type is not None:
            return explicit_value_type
    if value is None:
        return "null"
    if isinstance(value, bool):
        return "bool"
    if isinstance(value, str):
        return "string"
    if isinstance(value, int):
        return "int"
    if isinstance(value, float):
        return "float"
    sequence_items = _sequence_items(value)
    if sequence_items is not None:
        if all(isinstance(item, (int, float)) for item in sequence_items):
            return "float_array"
        return "array"
    if _is_rna_value(value):
        return "rna_ref"
    if _is_collection_value(value):
        return "collection"
    return type(value).__name__


def _readonly(property_definition: object | None, value: object) -> bool:
    if property_definition is not None:
        return bool(getattr(property_definition, "is_readonly", False))
    return _is_collection_value(value)


def _array_length(property_definition: object | None, value: object) -> int | None:
    if property_definition is not None and bool(getattr(property_definition, "is_array", False)):
        return int(getattr(property_definition, "array_length", 0)) or None
    sequence_items = _sequence_items(value)
    return len(sequence_items) if sequence_items is not None else None


def _subtype(property_definition: object | None) -> str | None:
    subtype = getattr(property_definition, "subtype", None)
    return str(subtype) if subtype not in (None, "", "NONE") else None


def _unit(property_definition: object | None) -> str | None:
    unit = getattr(property_definition, "unit", None)
    return str(unit) if unit not in (None, "", "NONE") else None


def _animatable(property_definition: object | None) -> bool:
    return bool(getattr(property_definition, "is_animatable", False))


def _is_enum_flag(property_definition: object | None) -> bool:
    return bool(getattr(property_definition, "is_enum_flag", False))


def _coerce_supported_value(value: object, *, allow_mappings: bool = False) -> object:
    if value is None or isinstance(value, (bool, int, float, str)):
        return value
    if isinstance(value, list):
        return [_coerce_supported_value(item, allow_mappings=allow_mappings) for item in value]
    if isinstance(value, dict):
        path = value.get("path")
        if isinstance(path, str) and path:
            return path
        if allow_mappings:
            return {
                str(key): _coerce_supported_value(item, allow_mappings=True)
                for key, item in value.items()
            }
    raise UnsupportedValueTypeError(f"Unsupported payload value type: {type(value).__name__}")


def _resolve_path_reference(value: object, resolver: PathResolver) -> object:
    if isinstance(value, str) and value.startswith("bpy."):
        return resolver.resolve(value).value
    if isinstance(value, list):
        return [_resolve_path_reference(item, resolver) for item in value]
    if isinstance(value, dict):
        return {
            key: _resolve_path_reference(item, resolver)
            for key, item in value.items()
        }
    return value


def _rna_type(value: object) -> str:
    bl_rna = _safe_getattr(value, "bl_rna")
    if bl_rna is None:
        rna_type = _safe_getattr(value, "rna_type")
        if rna_type is not None:
            return str(_safe_getattr(rna_type, "identifier") or type(value).__name__)
        return type(value).__name__
    return getattr(bl_rna, "identifier", type(value).__name__)


def _id_type(value: object) -> str | None:
    id_type = _safe_getattr(value, "id_type")
    if isinstance(id_type, str):
        return id_type
    return None


def _session_uid(value: object) -> int | None:
    session_uid = _safe_getattr(value, "session_uid")
    return int(session_uid) if isinstance(session_uid, int) else None


def _to_item_ref(value: object, path: str, *, label: str | None = None) -> JsonObject:
    path = path or _module_path(value) or ""
    display_name = _safe_getattr(value, "name")
    name = display_name if isinstance(display_name, str) and display_name else path
    return {
        "kind": "rna",
        "path": path,
        "name": name,
        "label": label or name,
        "rnaType": _rna_type(value),
        "idType": _id_type(value),
        "sessionUid": _session_uid(value),
    }


def _collection_item_path(base_path: str, key: object, value: object, index: int) -> str:
    name = getattr(value, "name", None)
    if isinstance(name, str) and name:
        return f"{base_path}[{_json_string(name)}]"
    if isinstance(key, str):
        return f"{base_path}[{_json_string(key)}]"
    return f"{base_path}[{index}]"


class RnaService:
    def __init__(self, resolver: PathResolver) -> None:
        self._resolver = resolver

    def list(self, path: str) -> JsonObject:
        resolved = self._resolver.resolve(path)
        collection = resolved.value
        items = []
        if isinstance(collection, dict):
            iterable = list(collection.items())
        else:
            iterable = list(enumerate(collection))

        for index, entry in enumerate(iterable):
            if isinstance(collection, dict):
                key, value = entry
            else:
                key, value = entry
            item_path = _collection_item_path(path, key, value, index)
            items.append(
                _to_item_ref(
                    value,
                    item_path,
                )
            )

        item_rna_type = _rna_type(items[0] if False else iterable[0][1]) if iterable else None
        return {
            "path": path,
            "itemRnaType": item_rna_type,
            "items": items,
        }

    def get(self, path: str) -> JsonObject:
        resolved = self._resolver.resolve(path)
        property_definition = _property_definition(resolved)
        return {
            "path": path,
            "value": _serialize_value(resolved.value, path, property_definition),
            "valueType": _value_type(resolved.value, property_definition),
            "readonly": _readonly(property_definition, resolved.value),
            "rnaType": _rna_type(resolved.parent) if resolved.parent is not None else _rna_type(resolved.value),
            "arrayLength": _array_length(property_definition, resolved.value),
        }

    def set(self, path: str, value: object) -> JsonObject:
        resolved = self._resolver.resolve(path)
        parent = resolved.parent
        segment = resolved.segment
        if parent is None or segment is None:
            raise ValueError(f"Path '{path}' is not writable.")
        property_definition = _property_definition(resolved)
        if _readonly(property_definition, resolved.value):
            raise ValueError(f"Path '{path}' is read-only.")
        if getattr(property_definition, "type", None) == "COLLECTION":
            raise ValueError(f"Path '{path}' does not support collection replacement.")

        coerced_value = _coerce_supported_value(value)
        if property_definition is not None:
            property_type = getattr(property_definition, "type", None)
            if property_type == "POINTER":
                coerced_value = None if coerced_value is None else _resolve_path_reference(coerced_value, self._resolver)
            elif property_type == "ENUM" and bool(getattr(property_definition, "is_enum_flag", False)):
                coerced_value = set(coerced_value or [])

        if isinstance(segment, str):
            setattr(parent, segment, coerced_value)
        else:
            parent[segment] = coerced_value

        return self.get(path)

    def describe(self, path: str) -> JsonObject:
        resolved = self._resolver.resolve(path)
        value = resolved.value
        property_definition = _property_definition(resolved)
        return {
            "path": path,
            "rnaType": _rna_type(value),
            "valueType": _value_type(value, property_definition),
            "readonly": _readonly(property_definition, value),
            "animatable": _animatable(property_definition),
            "subtype": _subtype(property_definition),
            "unit": _unit(property_definition),
            "arrayLength": _array_length(property_definition, value),
            "enumItems": _enum_items(property_definition),
            "isEnumFlag": _is_enum_flag(property_definition),
            "fixedType": _fixed_type(property_definition),
            "properties": None,
        }

    def call(
        self,
        path: str,
        method: str,
        args: list[object] | None = None,
        kwargs: dict[str, object] | None = None,
    ) -> JsonObject:
        resolved = self._resolver.resolve(path)
        target = resolved.value
        member = getattr(target, method)
        args = [_resolve_path_reference(_coerce_supported_value(arg), self._resolver) for arg in list(args or [])]
        kwargs = {
            key: _resolve_path_reference(_coerce_supported_value(value), self._resolver)
            for key, value in dict(kwargs or {}).items()
        }
        result = member(*args, **kwargs)
        result_path = path
        if method == "preview_ensure":
            result_path = f"{path}.preview"
        elif hasattr(result, "name") and getattr(result, "name", None):
            result_path = f"{path}[{_json_string(result.name)}]"
        return {
            "return": _serialize_value(result, result_path),
        }


class OpsService:
    def __init__(self, resolver: PathResolver) -> None:
        self._resolver = resolver

    def poll(
        self,
        operator_name: str,
        operator_context: str = "EXEC_DEFAULT",
        context_override: dict[str, object] | None = None,
    ) -> JsonObject:
        operator = self._resolve_operator(operator_name)
        with self._override_context(_coerce_supported_value(context_override or {}, allow_mappings=True)):
            poll = getattr(operator, "poll", None)
            can_execute = bool(poll()) if callable(poll) else True
        return {
            "operator": operator_name,
            "canExecute": can_execute,
            "failureReason": None if can_execute else "Operator poll() returned false.",
        }

    def call(
        self,
        operator_name: str,
        properties: dict[str, object] | None = None,
        operator_context: str = "EXEC_DEFAULT",
        context_override: dict[str, object] | None = None,
    ) -> JsonObject:
        operator = self._resolve_operator(operator_name)
        coerced_properties = {
            key: _resolve_path_reference(_coerce_supported_value(value), self._resolver)
            for key, value in dict(properties or {}).items()
        }
        with self._override_context(_coerce_supported_value(context_override or {}, allow_mappings=True)):
            result = operator(operator_context, **coerced_properties)
        return {
            "operator": operator_name,
            "result": _result_list(result),
        }

    def _resolve_operator(self, operator_name: str) -> object:
        current = bpy.ops
        for part in operator_name.split("."):
            current = getattr(current, part)
        return current

    def _override_context(self, context_override: dict[str, object]) -> Any:
        if not context_override:
            return nullcontext()

        temp_override = getattr(bpy.context, "temp_override", None)
        if temp_override is None:
            return nullcontext()

        return temp_override(**self._resolve_override(context_override))

    def _resolve_override(self, value: object) -> object:
        if isinstance(value, str) and value.startswith("bpy."):
            return self._resolver.resolve(value).value
        if isinstance(value, dict):
            if set(value.keys()) == {"path"} and isinstance(value.get("path"), str):
                return self._resolver.resolve(value["path"]).value
            return {key: self._resolve_override(item) for key, item in value.items()}
        if isinstance(value, list):
            return [self._resolve_override(item) for item in value]
        return value


@dataclass
class _WatchRecord:
    watch_id: str
    source: str
    path: str
    revision: int = 0


class WatchService:
    _instances = weakref.WeakSet()
    _handlers_registered = False
    _handlers_lock = threading.Lock()

    def __init__(self, rna_service: RnaService) -> None:
        self._rna_service = rna_service
        self._sender: Callable[[JsonObject], None] | None = None
        self._lock = threading.Lock()
        self._watches: dict[str, _WatchRecord] = {}
        WatchService._instances.add(self)
        self._ensure_handlers()

    def set_event_sender(self, sender: Callable[[JsonObject], None] | None) -> None:
        self._sender = sender

    def subscribe(self, watch_id: str, source: str, path: str) -> JsonObject:
        if source not in {"depsgraph", "frame", "lifecycle"}:
            raise ValueError(f"Unsupported watch source: {source}")
        with self._lock:
            self._watches[watch_id] = _WatchRecord(watch_id=watch_id, source=source, path=path, revision=0)
            watch = self._watches[watch_id]
        return {
            "watchId": watch.watch_id,
            "revision": watch.revision,
            "source": watch.source,
        }

    def unsubscribe(self, watch_id: str) -> JsonObject:
        with self._lock:
            self._watches.pop(watch_id, None)
        return {
            "watchId": watch_id,
        }

    def read(self, watch_id: str) -> JsonObject:
        with self._lock:
            watch = self._watches.get(watch_id)
        if watch is None:
            raise ValueError(f"Unknown watch id: {watch_id}")
        payload = self._snapshot_payload(watch.path)
        return {
            "watchId": watch.watch_id,
            "revision": watch.revision,
            "source": watch.source,
            "payload": payload,
        }

    def mark_dirty(self, source: str, dirty_refs: list[JsonObject] | None = None) -> None:
        dirty_events: list[JsonObject] = []
        with self._lock:
            for watch in self._watches.values():
                if watch.source != source:
                    continue
                watch.revision += 1
                dirty_events.append(
                    {
                        "watchId": watch.watch_id,
                        "revision": watch.revision,
                        "source": watch.source,
                        "dirtyRefs": dirty_refs,
                    }
                )

        sender = self._sender
        if sender is None:
            return

        for payload in dirty_events:
            sender(
                BusinessEvent(
                    protocol_version=PROTOCOL_VERSION,
                    schema_version=SCHEMA_VERSION,
                    name="watch.dirty",
                    payload=payload,
                ).to_header()
            )

    def _snapshot_payload(self, path: str) -> JsonObject:
        resolved = self._rna_service._resolver.resolve(path)
        if _is_collection_value(resolved.value):
            return self._rna_service.list(path)
        return self._rna_service.get(path)

    @classmethod
    def _ensure_handlers(cls) -> None:
        with cls._handlers_lock:
            if cls._handlers_registered:
                return

            app = getattr(bpy, "app", None)
            handlers = getattr(app, "handlers", None)
            if handlers is None:
                cls._handlers_registered = True
                return

            persistent = getattr(handlers, "persistent", lambda fn: fn)

            @persistent
            def _depsgraph_update_post(_scene, _depsgraph):
                cls._notify_instances("depsgraph")

            @persistent
            def _frame_change_post(_scene, _depsgraph=None):
                cls._notify_instances("frame")

            @persistent
            def _load_post(_dummy=None):
                cls._notify_instances("lifecycle")

            @persistent
            def _undo_post(_dummy=None):
                cls._notify_instances("lifecycle")

            @persistent
            def _redo_post(_dummy=None):
                cls._notify_instances("lifecycle")

            for handler_list, handler in (
                (getattr(handlers, "depsgraph_update_post", None), _depsgraph_update_post),
                (getattr(handlers, "frame_change_post", None), _frame_change_post),
                (getattr(handlers, "load_post", None), _load_post),
                (getattr(handlers, "undo_post", None), _undo_post),
                (getattr(handlers, "redo_post", None), _redo_post),
            ):
                if handler_list is not None and handler not in handler_list:
                    handler_list.append(handler)

            cls._handlers_registered = True

    @classmethod
    def _notify_instances(cls, source: str) -> None:
        for instance in list(cls._instances):
            instance.mark_dirty(source)


class BusinessDispatcher(BusinessEndpoint):
    def __init__(self) -> None:
        self._resolver = PathResolver()
        self._rna = RnaService(self._resolver)
        self._ops = OpsService(self._resolver)
        self._watch = WatchService(self._rna)

    def set_event_sender(self, sender: Callable[[JsonObject], None] | None) -> None:
        self._watch.set_event_sender(sender)

    def invoke(self, request: BusinessRequest) -> BusinessResponse:
        if int(request.protocol_version) != PROTOCOL_VERSION:
            return _error_response(
                "unsupported_protocol_version",
                f"Unsupported protocol version: {request.protocol_version}",
                request.message_id,
                protocol_version=PROTOCOL_VERSION,
                schema_version=SCHEMA_VERSION,
            )

        if int(request.schema_version) != SCHEMA_VERSION:
            return _error_response(
                "unsupported_schema_version",
                f"Unsupported schema version: {request.schema_version}",
                request.message_id,
                protocol_version=request.protocol_version,
                schema_version=SCHEMA_VERSION,
            )

        payload = request.payload if isinstance(request.payload, dict) else {}

        try:
            response_payload = self._dispatch(request.name, payload)
        except UnsupportedBusinessRequestError as exc:
            return _error_response(
                "unsupported_business_request",
                str(exc),
                request.message_id,
                protocol_version=request.protocol_version,
                schema_version=request.schema_version,
            )
        except UnsupportedValueTypeError as exc:
            return _error_response(
                "unsupported_value_type",
                str(exc),
                request.message_id,
                protocol_version=request.protocol_version,
                schema_version=request.schema_version,
            )
        except ValueError as exc:
            return _error_response(
                "invalid_payload",
                str(exc),
                request.message_id,
                protocol_version=request.protocol_version,
                schema_version=request.schema_version,
            )
        except Exception as exc:
            return _error_response(
                "business_request_failed",
                str(exc),
                request.message_id,
                protocol_version=request.protocol_version,
                schema_version=request.schema_version,
            )

        return _success_response(
            request.message_id,
            payload=response_payload,
            protocol_version=request.protocol_version,
            schema_version=request.schema_version,
        )

    def _dispatch(self, name: str, payload: JsonObject) -> JsonObject:
        if name == "rna.list":
            return self._rna.list(_require_str(payload, "path"))
        if name == "rna.get":
            return self._rna.get(_require_str(payload, "path"))
        if name == "rna.set":
            if "value" not in payload:
                raise ValueError("rna.set expects value.")
            return self._rna.set(_require_str(payload, "path"), payload["value"])
        if name == "rna.describe":
            return self._rna.describe(_require_str(payload, "path"))
        if name == "rna.call":
            return self._rna.call(
                _require_str(payload, "path"),
                _require_str(payload, "method"),
                payload.get("args") or [],
                payload.get("kwargs") or {},
            )
        if name == "ops.poll":
            return self._ops.poll(
                _require_str(payload, "operator"),
                payload.get("operatorContext", "EXEC_DEFAULT"),
                payload.get("contextOverride") or {},
            )
        if name == "ops.call":
            return self._ops.call(
                _require_str(payload, "operator"),
                payload.get("properties") or {},
                payload.get("operatorContext", "EXEC_DEFAULT"),
                payload.get("contextOverride") or {},
            )
        if name == "watch.subscribe":
            return self._watch.subscribe(
                _require_str(payload, "watchId"),
                _require_str(payload, "source"),
                _require_str(payload, "path"),
            )
        if name == "watch.unsubscribe":
            return self._watch.unsubscribe(_require_str(payload, "watchId"))
        if name == "watch.read":
            return self._watch.read(_require_str(payload, "watchId"))
        raise UnsupportedBusinessRequestError(f"Unsupported business request: {name}")


def _require_str(payload: JsonObject, key: str) -> str:
    value = payload.get(key)
    if not isinstance(value, str) or not value:
        raise ValueError(f"{key} expects a non-empty string.")
    return value


class DefaultBusinessEndpoint(BusinessDispatcher):
    pass


class BusinessBridgeHandler:
    def handle_packet(self, header: JsonObject, payload: object) -> JsonObject:
        raise NotImplementedError


class EndpointBusinessBridgeHandler(BusinessBridgeHandler):
    def __init__(self, endpoint: BusinessEndpoint | None = None) -> None:
        self.endpoint = endpoint or DefaultBusinessEndpoint()

    def handle_packet(self, header: JsonObject, payload: object) -> JsonObject:
        request = BusinessRequest.from_header(header)
        response = self.endpoint.invoke(request)
        return response.to_header()


class DefaultBusinessBridgeHandler(EndpointBusinessBridgeHandler):
    def __init__(self) -> None:
        super().__init__(DefaultBusinessEndpoint())


class BlenderBusinessBridge(DefaultBusinessEndpoint):
    def handle(self, request: BusinessRequest) -> BusinessResponse:
        return self.invoke(request)


__all__ = [
    "PROTOCOL_VERSION",
    "SCHEMA_VERSION",
    "BusinessError",
    "BusinessRequest",
    "BusinessResponse",
    "BusinessEvent",
    "BusinessEndpoint",
    "DefaultBusinessEndpoint",
    "BusinessBridgeHandler",
    "DefaultBusinessBridgeHandler",
    "EndpointBusinessBridgeHandler",
    "BlenderBusinessBridge",
]
