#!/usr/bin/env python3
"""Local web server for the Synty asset library (Python stdlib only).

Usage (from the repo root):
    python3 tools/asset-library/server.py --port 5193
    python3 tools/asset-library/server.py --host 192.168.1.128 --port 5193   # trusted LAN only

Security model: loopback bind by default; an explicit private IPv4 address
(RFC 1918 only, never a wildcard or a public address) can be chosen to reach
the library from a trusted LAN. Peers must be loopback or RFC 1918. Host header
pinned to loopback names, the bound LAN address and the real port (DNS
rebinding); same-origin checks on every write; fixed route allowlist (no free
filesystem paths); strict CSP with no external network. No authentication and
no TLS: the LAN mode trusts every device on the local network.
"""

from __future__ import annotations

import argparse
import base64
import hashlib
import ipaddress
import json
import mimetypes
import re
import socket
import sys
import threading
import time
import traceback
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple
from urllib.parse import parse_qs, unquote, urlsplit

HERE = Path(__file__).resolve().parent
if str(HERE) not in sys.path:
    sys.path.insert(0, str(HERE))

import store as store_mod  # noqa: E402
from store import SelectionError, SelectionStore, is_used, render_markdown, valid_id  # noqa: E402

DEFAULT_PORT = 5193
DEFAULT_LIBRARY = Path("/Volumes/4T_White/50_INBOX/Synty")
LOOPBACK_HOSTS = ("127.0.0.1", "localhost", "::1")
# Explicit RFC 1918 ranges. ipaddress.is_private is not used on purpose: it also
# accepts documentation, benchmark, link-local and other special ranges.
LAN_NETWORKS = (
    ipaddress.IPv4Network("10.0.0.0/8"),
    ipaddress.IPv4Network("172.16.0.0/12"),
    ipaddress.IPv4Network("192.168.0.0/16"),
)
LOOPBACK_V4 = ipaddress.IPv4Network("127.0.0.0/8")
MAX_BODY = 64 * 1024
MAX_PATH = 2048
MAX_FILTER = 200
MAX_LIMIT = 100
DEFAULT_LIMIT = 60
FORBIDDEN_PORTS = {3000, 3001, 5173}

WEB_NAME_RE = re.compile(r"^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,80}\.(html|js|css|svg|png|ico|woff2|json)$")
VENDOR_SEG_RE = re.compile(r"^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,120}$")
VENDOR_EXT = {".js": "text/javascript; charset=utf-8", ".mjs": "text/javascript; charset=utf-8", ".wasm": "application/wasm"}
WEB_TYPES = {
    ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8",
    ".svg": "image/svg+xml", ".png": "image/png", ".ico": "image/x-icon", ".woff2": "font/woff2",
    ".json": "application/json; charset=utf-8",
}
MEDIA_TYPES = {
    "image/png", "image/jpeg", "image/webp", "image/gif", "image/avif", "image/bmp", "image/x-tga", "image/tga",
    "model/gltf-binary", "model/gltf+json", "application/octet-stream", "image/svg+xml", "text/plain",
}
SCRIPT_RE = re.compile(r"<script(?![^>]*\bsrc\s*=)[^>]*>(.*?)</script>", re.S | re.I)

PLACEHOLDER_HTML = """<!doctype html>
<html lang="es"><head><meta charset="utf-8"><title>Biblioteca Synty</title></head>
<body><h1>Biblioteca Synty</h1>
<p>El servidor funciona, pero falta la interfaz web (<code>tools/asset-library/web/index.html</code>).</p>
<p>La API está en <a href="/api/status">/api/status</a>.</p></body></html>
"""


# -- catalog fallback -------------------------------------------------------

class EmptyCatalog:
    """Stand-in used when catalog.py is missing or fails to start."""

    def __init__(self, reason: str):
        self.reason = reason

    def reindex(self) -> None:
        raise RuntimeError(self.reason)

    def status(self) -> dict:
        return {"assets": 0, "packs": 0, "used": 0, "indexedAt": None, "issues": [self.reason], "coverage": {}}

    def packs(self) -> list:
        return []

    def used_ids(self) -> list:
        return []

    def search(self, filters: dict) -> dict:
        return {"items": [], "total": 0, "offset": filters.get("offset", 0), "limit": filters.get("limit", DEFAULT_LIMIT)}

    def get_asset(self, asset_id: str) -> Optional[dict]:
        return None

    def read_media(self, asset_id: str, mode: str = "asset") -> Tuple[bytes, str]:
        raise FileNotFoundError(asset_id)

    def read_pack_thumbnail(self, pack_id: str) -> Tuple[bytes, str]:
        raise FileNotFoundError(pack_id)


def load_catalog(repo_root: Path, library_root: Path, data_dir: Path):
    try:
        from catalog import Catalog  # type: ignore
    except Exception as exc:  # ImportError or a broken module
        log("catalog.py no disponible: %s" % exc.__class__.__name__)
        return EmptyCatalog("El indexador (catalog.py) aún no está disponible; la biblioteca se muestra vacía.")
    try:
        return Catalog(repo_root, library_root, data_dir)
    except Exception as exc:
        log("Catalog no arrancó: %r" % exc)
        return EmptyCatalog("El indexador no pudo arrancar (%s); la biblioteca se muestra vacía." % exc.__class__.__name__)


def log(message: str) -> None:
    sys.stderr.write("[asset-library] %s\n" % message)
    sys.stderr.flush()


# -- application ------------------------------------------------------------

class App:
    def __init__(self, repo_root: Path, library_root: Path, data_dir: Path, selections_dir: Path,
                 catalog: Any = None, web_dir: Optional[Path] = None, three_dir: Optional[Path] = None):
        self.repo_root = Path(repo_root).resolve()
        self.library_root = Path(library_root)
        self.data_dir = Path(data_dir)
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.web_dir = Path(web_dir) if web_dir else HERE / "web"
        three = Path(three_dir) if three_dir else self.repo_root / "node_modules" / "three"
        self.vendor_roots = {"build": three / "build", "examples/jsm": three / "examples" / "jsm"}
        self.catalog = catalog if catalog is not None else load_catalog(self.repo_root, self.library_root, self.data_dir)
        sel = Path(selections_dir)
        self.store = SelectionStore(sel / "selecciones.json", sel / "SELECCION.md", self.data_dir)
        self.index_lock = threading.Lock()
        self.index_state: Dict[str, Any] = {"running": False, "startedAt": None, "finishedAt": None, "error": None, "warning": None}
        self.index_thread: Optional[threading.Thread] = None
        try:
            self.store.ensure_files(self.render_export)
        except Exception as exc:
            log("No se pudieron preparar los archivos de selección: %r" % exc)

    # catalog helpers (never let a catalog bug take the server down)

    def asset(self, asset_id: str) -> Optional[dict]:
        try:
            found = self.catalog.get_asset(asset_id)
        except Exception as exc:
            log("get_asset falló: %r" % exc)
            return None
        return found if isinstance(found, dict) else None

    def used_ids(self) -> List[str]:
        try:
            return [str(x) for x in (self.catalog.used_ids() or [])]
        except Exception as exc:
            log("used_ids falló: %r" % exc)
            return []

    def catalog_status(self) -> dict:
        try:
            status = self.catalog.status()
            return dict(status) if isinstance(status, dict) else {}
        except Exception as exc:
            log("status falló: %r" % exc)
            return {"issues": ["El indexador no responde (%s)." % exc.__class__.__name__]}

    def library_available(self) -> bool:
        try:
            return self.library_root.is_dir()
        except OSError:
            return False

    # decoration

    def decorate(self, asset: dict, entries: Dict[str, dict], revision: int) -> dict:
        out = dict(asset)
        used = is_used(asset)
        entry = entries.get(str(asset.get("id")))
        out["selected"] = entry["selected"] if entry else used
        out["intent"] = entry.get("intent", "") if entry else ""
        out["status"] = "used" if used else ("requested" if out["selected"] else "available")
        out["revision"] = revision
        if entry and entry.get("updatedAt"):
            out["selectionUpdatedAt"] = entry["updatedAt"]
        return out

    def stub_from_entry(self, entry: dict) -> dict:
        loc = entry.get("locator") or {}
        return {
            "id": entry["id"], "name": entry.get("name") or entry["id"], "packId": loc.get("packId"),
            "packName": entry.get("packName") or loc.get("packName"), "family": entry.get("family"),
            "kind": entry.get("kind"), "path": loc.get("path"), "source": loc.get("source"),
            "usages": entry.get("detectedUsages") or [], "thumbnailUrl": None, "thumbnailIsPack": False,
            "preview": None, "available": False, "missingFromCatalog": True,
        }

    def render_export(self, revision: Optional[int] = None, items: Optional[Dict[str, dict]] = None) -> str:
        if revision is None or items is None:
            revision, items = self.store.snapshot()
        used = [self.asset(i) for i in self.used_ids()]
        return render_markdown(revision, items, self.asset, [u for u in used if u], self.catalog_status())

    # reindex

    def start_reindex(self) -> Tuple[bool, str]:
        with self.index_lock:
            if self.index_state["running"]:
                return False, "Ya hay una indexación en curso."
            if not self.library_available():
                self.index_state["warning"] = "No se encuentra la biblioteca en %s (¿disco desconectado?). Se conserva el índice anterior." % self.library_root
                return False, self.index_state["warning"]
            self.index_state.update(running=True, startedAt=time.time(), finishedAt=None, error=None, warning=None)
            self.index_thread = threading.Thread(target=self._reindex_worker, name="asset-reindex", daemon=True)
            self.index_thread.start()
            return True, "Indexación iniciada."

    def _reindex_worker(self) -> None:
        error = None
        try:
            self.catalog.reindex()
        except Exception as exc:
            log("reindex falló: %s" % "".join(traceback.format_exception_only(type(exc), exc)).strip())
            error = "La indexación falló (%s). Se conserva el índice anterior si lo había." % exc.__class__.__name__
        warning = None
        if error is None:
            try:
                # Refreshes the detected-usage snapshot in the export; selections JSON is untouched.
                self.refresh_export()
            except SelectionError as exc:
                warning = exc.message
            except Exception as exc:
                log("No se pudo regenerar SELECCION.md: %r" % exc)
                warning = "No se pudo regenerar SELECCION.md (%s)." % exc.__class__.__name__
        with self.index_lock:
            self.index_state.update(running=False, finishedAt=time.time(), error=error, warning=warning)

    def refresh_export(self) -> str:
        """Renders and writes SELECCION.md atomically with the selection state (409 if the JSON is broken)."""
        return self.store.refresh_export(lambda rev, items: self.render_export(rev, items))

    def missing_stubs(self, ids: List[str], entries: Dict[str, dict], filters: dict) -> List[dict]:
        """Stubs for selection entries no longer in the catalog that pass the text filters."""
        q_terms = str(filters.get("q") or "").lower().split()[:8]
        family = str(filters.get("family") or "").lower()
        pack = str(filters.get("pack") or "")
        kind = str(filters.get("kind") or "")
        out = []
        for asset_id in sorted(ids):
            entry = entries.get(asset_id)
            if entry is None or self.asset(asset_id) is not None:
                continue
            stub = self.stub_from_entry(entry)
            loc = entry.get("locator") or {}
            hay = " ".join(str(x) for x in (stub["name"], stub["path"], loc.get("member"), stub["packName"]) if x).lower()
            if any(t not in hay for t in q_terms):
                continue
            if family and str(stub.get("family") or "").lower() != family:
                continue
            if pack and pack not in (stub.get("packId"), stub.get("packName")):
                continue
            if kind and stub.get("kind") != kind:
                continue
            out.append(stub)
        return out

    def status(self) -> dict:
        out = self.catalog_status()
        with self.index_lock:
            idx = dict(self.index_state)
        issues = list(out.get("issues") or [])
        available = self.library_available()
        if not available:
            issues.append("La biblioteca original (%s) no está accesible; se muestra el índice anterior." % self.library_root)
        if idx.get("error"):
            issues.append(idx["error"])
        info = self.store.info()
        if info.get("broken"):
            issues.append(info["broken"])
        out["issues"] = issues
        out["libraryRoot"] = str(self.library_root)
        out["libraryAvailable"] = available
        out["diskAvailable"] = available
        out["reindex"] = idx
        out["indexing"] = idx["running"] or bool(out.get("indexing"))
        out["revision"] = info["revision"]
        _, entries = self.store.snapshot()
        used = set(self.used_ids())
        selected = {i for i in used if not (i in entries and not entries[i]["selected"])}
        selected |= {i for i, e in entries.items() if e["selected"]}
        out["selected"] = len(selected)
        out["requested"] = len([i for i in selected if i not in used])
        out["selections"] = {"count": info["count"], "broken": info["broken"], "warnings": info["warnings"]}
        return out


# -- HTTP layer -------------------------------------------------------------

class HttpError(Exception):
    def __init__(self, status: int, message: str, extra: Optional[dict] = None):
        super().__init__(message)
        self.status = status
        self.message = message
        self.extra = extra or {}


class Handler(BaseHTTPRequestHandler):
    server_version = "asset-library"
    sys_version = ""
    protocol_version = "HTTP/1.1"

    @property
    def app(self) -> App:
        return self.server.app  # type: ignore[attr-defined]

    def log_message(self, format: str, *args: Any) -> None:  # noqa: A002 (base class name)
        if getattr(self.server, "quiet", False):
            return
        log("%s %s" % (self.address_string(), format % args))

    # entry points

    def do_GET(self) -> None:
        self._dispatch("GET")

    def do_HEAD(self) -> None:
        self._dispatch("HEAD")

    def do_PUT(self) -> None:
        self._dispatch("PUT")

    def do_POST(self) -> None:
        self._dispatch("POST")

    def do_DELETE(self) -> None:
        self._dispatch("DELETE")

    def do_PATCH(self) -> None:
        self._dispatch("PATCH")

    def do_OPTIONS(self) -> None:
        self._dispatch("OPTIONS")

    def _dispatch(self, method: str) -> None:
        self._head_only = method == "HEAD"
        self._body_read = False
        try:
            if len(self.path) > MAX_PATH:
                raise HttpError(414, "La dirección es demasiado larga.")
            self._check_host()
            parts = urlsplit(self.path)
            raw_path = parts.path
            if method in ("PUT", "POST", "DELETE", "PATCH"):
                self._check_origin()
            if raw_path.startswith("/api/") and self.headers.get("Sec-Fetch-Site", "") == "cross-site":
                raise HttpError(403, "Petición de otro sitio rechazada.")
            if method not in ("GET", "HEAD", "PUT", "POST"):
                raise HttpError(405, "Método no permitido.")
            self._route(method if method != "HEAD" else "GET", raw_path, parse_qs(parts.query, keep_blank_values=True))
        except HttpError as exc:
            self._error(exc.status, exc.message, exc.extra)
        except SelectionError as exc:
            self._error(exc.status, exc.message, exc.payload)
        except (BrokenPipeError, ConnectionResetError):
            pass
        except Exception as exc:
            log("Error interno: %s" % "".join(traceback.format_exception(type(exc), exc, exc.__traceback__)))
            self._error(500, "Error interno del servidor.")

    # security checks

    def _allowed_authorities(self) -> set:
        port = int(self.server.server_address[1])  # type: ignore[index]
        allowed = {"127.0.0.1:%d" % port, "localhost:%d" % port, "[::1]:%d" % port}
        lan_host = getattr(self.server, "lan_host", None)
        if lan_host:
            allowed.add("%s:%d" % (lan_host, port))
        return allowed

    def _check_host(self) -> None:
        host = (self.headers.get("Host") or "").strip().lower()
        if host not in self._allowed_authorities():
            raise HttpError(421 if host else 400, "Host no permitido: solo 127.0.0.1, localhost o la IP local del servidor, con su puerto.")

    def _check_origin(self) -> None:
        origin = self.headers.get("Origin")
        if origin is not None:
            origin = origin.strip().lower()
            if origin not in {"http://" + a for a in self._allowed_authorities()}:
                raise HttpError(403, "Origen no permitido para modificar la selección.")
        site = self.headers.get("Sec-Fetch-Site")
        if site is not None and site not in ("same-origin", "none"):
            raise HttpError(403, "Petición de otro sitio rechazada.")
        if origin is None and site is None:
            # Non-browser client (curl, tests). Browsers always send Origin on writes.
            referer = self.headers.get("Referer")
            if referer:
                ref = urlsplit(referer)
                if (ref.netloc or "").lower() not in self._allowed_authorities():
                    raise HttpError(403, "Origen no permitido para modificar la selección.")

    # routing

    def _route(self, method: str, path: str, query: Dict[str, List[str]]) -> None:
        if path.startswith("/api/"):
            return self._route_api(method, path, query)
        if method != "GET":
            raise HttpError(405, "Método no permitido.")
        if path.startswith("/vendor/three/"):
            return self._serve_vendor(path[len("/vendor/three/"):])
        if path in ("/", "/index.html"):
            return self._serve_index()
        if path == "/favicon.ico" and not (self.app.web_dir / "favicon.ico").is_file():
            return self._send(204, b"", None)
        name = path[1:]
        if "/" in name or not WEB_NAME_RE.match(name):
            raise HttpError(404, "No encontrado.")
        return self._serve_web_file(name)

    def _route_api(self, method: str, path: str, query: Dict[str, List[str]]) -> None:
        app = self.app
        segs = path[len("/api/"):].split("/")
        if segs == ["status"]:
            self._only(method, "GET")
            return self._json(200, app.status())
        if segs == ["packs"]:
            self._only(method, "GET")
            return self._json(200, {"items": self._packs()})
        if len(segs) == 3 and segs[0] == "packs" and segs[2] == "thumbnail":
            self._only(method, "GET")
            pack_id = self._decode_id(segs[1])
            return self._media(lambda: app.catalog.read_pack_thumbnail(pack_id))
        if segs == ["assets"]:
            self._only(method, "GET")
            return self._json(200, self._search(query))
        if len(segs) >= 2 and segs[0] == "assets":
            rest = segs[1:]
            if len(rest) == 2 and rest[1] in ("appearance", "file"):
                asset_id = self._decode_id(rest[0])
                self._only(method, "GET")
                if app.asset(asset_id) is None:
                    raise HttpError(404, "Ese recurso no está en el catálogo.")
                if rest[1] == "file":
                    return self._media(lambda: app.catalog.read_file(asset_id))
                return self._appearance(asset_id)
            if len(rest) == 2 and rest[1] in ("media", "thumbnail"):
                asset_id = self._decode_id(rest[0])
                self._only(method, "GET")
                mode = "asset" if rest[1] == "media" else "thumbnail"
                if app.asset(asset_id) is None:
                    raise HttpError(404, "Ese recurso no está en el catálogo.")
                return self._media(lambda: app.catalog.read_media(asset_id, mode=mode))
            if len(rest) != 1:
                raise HttpError(404, "No encontrado.")
            asset_id = self._decode_id(rest[0])
            self._only(method, "GET")
            asset = app.asset(asset_id)
            revision, entries = app.store.snapshot()
            if asset is None:
                if asset_id in entries:
                    return self._json(200, app.decorate(app.stub_from_entry(entries[asset_id]), entries, revision))
                raise HttpError(404, "Ese recurso no está en el catálogo.")
            return self._json(200, app.decorate(asset, entries, revision))
        if segs == ["selections"]:
            self._only(method, "GET")
            return self._json(200, self._selections())
        if len(segs) == 2 and segs[0] == "selections":
            self._only(method, "PUT")
            return self._put_selection(self._decode_id(segs[1]))
        if segs == ["reindex"]:
            self._only(method, "POST")
            self._read_body(optional=True)
            started, message = app.start_reindex()
            if not started:
                status = 409 if app.index_state.get("running") else 503
                raise HttpError(status, message, {"status": app.status()})
            return self._json(202, {"started": True, "message": message, "status": app.status()})
        if segs == ["export"]:
            self._only(method, "GET")
            return self._export(query)
        raise HttpError(404, "No encontrado.")

    def _only(self, method: str, allowed: str) -> None:
        if method != allowed:
            raise HttpError(405, "Método no permitido.", {"allow": allowed})

    def _decode_id(self, raw: str) -> str:
        try:
            value = unquote(raw, errors="strict") if "%" in raw else raw
        except UnicodeDecodeError:
            raise HttpError(400, "Identificador de recurso no válido.")
        if not valid_id(value):
            raise HttpError(400, "Identificador de recurso no válido.")
        return value

    # API handlers

    def _packs(self) -> list:
        try:
            packs = self.app.catalog.packs() or []
        except Exception as exc:
            log("packs falló: %r" % exc)
            return []
        _, entries = self.app.store.snapshot()
        used = set(self.app.used_ids())
        requested: Dict[str, int] = {}
        for asset_id, entry in entries.items():
            # Pending against the current catalog, not the usage snapshot stored in the entry.
            if entry.get("selected") and asset_id not in used:
                pid = (entry.get("locator") or {}).get("packId")
                if pid:
                    requested[pid] = requested.get(pid, 0) + 1
        out = []
        for p in packs:
            if isinstance(p, dict):
                item = dict(p)
                item["requestedCount"] = requested.get(str(p.get("id")), 0)
                out.append(item)
        return out

    def _search(self, query: Dict[str, List[str]]) -> dict:
        def one(name: str) -> str:
            values = query.get(name) or [""]
            value = values[-1].strip()
            if len(value) > MAX_FILTER:
                raise HttpError(400, "El filtro «%s» es demasiado largo." % name)
            return value

        def number(name: str, default: int, low: int, high: int) -> int:
            raw = one(name)
            if raw == "":
                return default
            if not re.fullmatch(r"\d{1,9}", raw):
                raise HttpError(400, "«%s» debe ser un número entero positivo." % name)
            return max(low, min(high, int(raw)))

        state = one("state")
        if state not in ("", "used", "selected", "pending"):
            raise HttpError(400, "Estado no válido: usa used, selected o pending.")
        filters: Dict[str, Any] = {
            "q": one("q"), "family": one("family"), "pack": one("pack"), "kind": one("kind"),
            "limit": number("limit", DEFAULT_LIMIT, 1, MAX_LIMIT), "offset": number("offset", 0, 0, 10 ** 9),
            "state": "",
        }
        revision, entries = self.app.store.snapshot()
        stubs: List[dict] = []
        if state == "used":
            filters["state"] = "used"
        elif state in ("selected", "pending"):
            used = set(self.app.used_ids())
            if state == "selected":
                ids = {i for i in used if not (i in entries and not entries[i]["selected"])}
                ids |= {i for i, e in entries.items() if e["selected"]}
            else:
                ids = {i for i, e in entries.items() if e["selected"] and i not in used}
            if not ids:
                return {"items": [], "total": 0, "offset": filters["offset"], "limit": filters["limit"], "revision": revision}
            # Entries that left the catalog still count (the export lists them too); only
            # selection entries are checked, never the whole catalog.
            stubs = self.app.missing_stubs([i for i in ids if i in entries and i not in used], entries, filters)
            missing = {s["id"] for s in stubs}
            filters["ids"] = sorted(i for i in ids if i not in missing)
        try:
            result = self.app.catalog.search(filters) if filters.get("ids", True) else {}
            result = result or {}
        except Exception as exc:
            log("search falló: %r" % exc)
            raise HttpError(503, "El catálogo no responde ahora mismo; prueba de nuevo en un momento.")
        found = [a for a in (result.get("items") or []) if isinstance(a, dict)]
        total = int(result.get("total") or 0)
        offset, limit = filters["offset"], filters["limit"]
        if stubs and len(found) < limit:
            # Stubs go after the catalog results, continuing the same pagination.
            start = max(0, offset + len(found) - total)
            found += stubs[start:start + limit - len(found)]
        items = [self.app.decorate(a, entries, revision) for a in found]
        return {
            "items": items, "total": total + len(stubs),
            "offset": result.get("offset", offset), "limit": result.get("limit", limit),
            "revision": revision,
        }

    def _selections(self) -> dict:
        revision, entries = self.app.store.snapshot()
        out = []
        for asset_id in sorted(entries):
            entry = entries[asset_id]
            asset = self.app.asset(asset_id)
            locator = store_mod.locator_of(asset) if asset else (entry.get("locator") or {})
            usages = store_mod.usages_of(asset) if asset else (entry.get("detectedUsages") or [])
            item = {
                "id": asset_id, "selected": entry["selected"], "intent": entry.get("intent", ""),
                "updatedAt": entry.get("updatedAt"), "locator": locator,
                "name": (asset or {}).get("name") or entry.get("name"),
                "packName": (asset or {}).get("packName") or entry.get("packName"),
                "detectedUsages": usages, "inCatalog": asset is not None,
                "status": "used" if usages else ("requested" if entry["selected"] else "available"),
            }
            for key, value in locator.items():
                item.setdefault(key, value)
            out.append(item)
        return {"revision": revision, "items": out}

    def _put_selection(self, asset_id: str) -> None:
        body = self._read_body(optional=False)
        try:
            payload = json.loads(body.decode("utf-8"))
        except (UnicodeDecodeError, ValueError):
            raise HttpError(400, "El cuerpo no es JSON válido.")
        if not isinstance(payload, dict):
            raise HttpError(400, "El cuerpo debe ser un objeto JSON.")
        missing = [k for k in ("selected", "intent", "revision") if k not in payload]
        if missing:
            raise HttpError(400, "Faltan campos: %s." % ", ".join(missing))
        app = self.app
        asset = app.asset(asset_id)
        if asset is None:
            _, entries = app.store.snapshot()
            if asset_id not in entries:
                raise HttpError(404, "Ese recurso no está en el catálogo.")
            asset = app.stub_from_entry(entries[asset_id])
        try:
            revision, _ = app.store.update(asset_id, payload["selected"], payload["intent"], payload["revision"],
                                           asset, lambda rev, items: app.render_export(rev, items))
        except SelectionError as exc:
            if exc.status == 409:
                # Give the client the fresh server state so it can show both versions.
                rev, entries = app.store.snapshot()
                exc.payload = dict(exc.payload, revision=rev, asset=app.decorate(asset, entries, rev))
            raise
        _, entries = app.store.snapshot()
        self._json(200, {"revision": revision, "asset": app.decorate(asset, entries, revision)})

    def _export(self, query: Dict[str, List[str]]) -> None:
        # 409 with a Spanish message if selecciones.json is broken; SELECCION.md is kept.
        text = self.app.refresh_export()
        inline = (query.get("inline") or [""])[-1] in ("1", "true")
        headers = {"Content-Disposition": "inline" if inline else 'attachment; filename="SELECCION.md"'}
        ctype = "text/plain; charset=utf-8" if inline else "text/markdown; charset=utf-8"
        self._send(200, text.encode("utf-8"), ctype, headers)

    def _appearance(self, asset_id: str) -> None:
        describe = getattr(self.app.catalog, "appearance", None)
        if describe is None:
            raise HttpError(404, "Este catálogo no describe apariencias.")
        try:
            desc = describe(asset_id)
        except KeyError:
            raise HttpError(404, "Ese recurso no está en el catálogo.")
        except OSError:
            raise HttpError(503, "La biblioteca original no está accesible (¿disco desconectado?).")
        self._json(200, desc)

    def _media(self, read) -> None:
        try:
            result = read()
        except (FileNotFoundError, KeyError, LookupError, ValueError):
            raise HttpError(404, "No hay vista previa para este recurso.")
        except PermissionError:
            raise HttpError(403, "No se puede leer ese archivo.")
        except OSError:
            raise HttpError(503, "La biblioteca original no está accesible (¿disco desconectado?).")
        except Exception as exc:
            log("lectura de media falló: %r" % exc)
            raise HttpError(404, "No hay vista previa para este recurso.")
        if not isinstance(result, tuple) or len(result) != 2 or not isinstance(result[0], (bytes, bytearray)):
            raise HttpError(404, "No hay vista previa para este recurso.")
        data, mime = result
        mime = (mime or "application/octet-stream").split(";")[0].strip().lower()
        headers = {"Cache-Control": "private, max-age=300"}
        if mime not in MEDIA_TYPES:
            mime = "application/octet-stream"
            headers["Content-Disposition"] = "attachment"
        if mime == "text/plain":
            # Unity .mat/.prefab text, shown for inspection only.
            mime = "text/plain; charset=utf-8"
            headers["Content-Security-Policy"] = "default-src 'none'; sandbox"
        if mime == "image/svg+xml":
            headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; sandbox"
        self._send(200, bytes(data), mime, headers)

    # static files

    def _csp(self, inline_hashes: List[str]) -> str:
        script = " ".join(["'self'", "'wasm-unsafe-eval'"] + ["'sha256-%s'" % h for h in inline_hashes])
        return ("default-src 'none'; script-src %s; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; "
                "connect-src 'self' data: blob:; worker-src 'self' blob:; font-src 'self' data:; media-src 'self' blob:; "
                "manifest-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'" % script)

    def _serve_index(self) -> None:
        path = self.app.web_dir / "index.html"
        try:
            data = path.read_bytes()
        except OSError:
            data = PLACEHOLDER_HTML.encode("utf-8")
        hashes = [base64.b64encode(hashlib.sha256(m.group(1).encode("utf-8")).digest()).decode("ascii")
                  for m in SCRIPT_RE.finditer(data.decode("utf-8", "replace"))]
        self._send(200, data, "text/html; charset=utf-8", {"Content-Security-Policy": self._csp(hashes), "Cache-Control": "no-cache"})

    def _serve_web_file(self, name: str) -> None:
        base = self.app.web_dir.resolve()
        target = (base / name).resolve()
        if target.parent != base or not target.is_file():
            raise HttpError(404, "No encontrado.")
        ctype = WEB_TYPES.get(target.suffix.lower())
        if not ctype:
            raise HttpError(404, "No encontrado.")
        headers = {"Cache-Control": "no-cache"}
        if target.suffix.lower() in (".html", ".svg"):
            headers["Content-Security-Policy"] = self._csp([])
        self._send(200, target.read_bytes(), ctype, headers)

    def _serve_vendor(self, sub: str) -> None:
        for prefix, root in self.app.vendor_roots.items():
            if sub.startswith(prefix + "/"):
                rel = sub[len(prefix) + 1:]
                break
        else:
            raise HttpError(404, "No encontrado.")
        segs = rel.split("/")
        if not segs or any(not VENDOR_SEG_RE.match(s) or s in (".", "..") for s in segs):
            raise HttpError(404, "No encontrado.")
        ext = Path(segs[-1]).suffix.lower()
        if ext not in VENDOR_EXT:
            raise HttpError(404, "No encontrado.")
        try:
            base = root.resolve(strict=True)
            target = base.joinpath(*segs).resolve(strict=True)
        except (OSError, RuntimeError):
            raise HttpError(404, "No encontrado.")
        if base not in target.parents or not target.is_file():
            raise HttpError(404, "No encontrado.")
        self._send(200, target.read_bytes(), VENDOR_EXT[ext], {"Cache-Control": "public, max-age=3600"})

    # IO helpers

    def _read_body(self, optional: bool) -> bytes:
        if self.headers.get("Transfer-Encoding"):
            raise HttpError(411, "Se necesita Content-Length (sin envío por trozos).")
        raw = self.headers.get("Content-Length")
        if raw is None:
            if optional:
                return b""
            raise HttpError(411, "Se necesita Content-Length.")
        if not raw.strip().isdigit():
            raise HttpError(400, "Content-Length no válido.")
        length = int(raw)
        if length > MAX_BODY:
            self.close_connection = True
            raise HttpError(413, "El cuerpo pasa del límite de 64 KB.")
        if not optional:
            ctype = (self.headers.get("Content-Type") or "").split(";")[0].strip().lower()
            if ctype != "application/json":
                self._discard(length)
                raise HttpError(415, "El cuerpo debe ser JSON (Content-Type: application/json).")
        data = self.rfile.read(length) if length else b""
        self._body_read = True
        return data

    def _discard(self, length: int) -> None:
        if length:
            self.rfile.read(length)
        self._body_read = True

    def _json(self, status: int, payload: Any) -> None:
        data = json.dumps(payload, ensure_ascii=False, default=str).encode("utf-8")
        self._send(status, data, "application/json; charset=utf-8")

    def _error(self, status: int, message: str, extra: Optional[dict] = None) -> None:
        if not self._body_read and (self.headers.get("Content-Length") or "0").strip() not in ("", "0"):
            # Unread request body: the connection can't be reused safely.
            self.close_connection = True
        payload = {"error": message}
        headers = {}
        for key, value in (extra or {}).items():
            if key == "allow":
                headers["Allow"] = value
            else:
                payload[key] = value
        try:
            data = json.dumps(payload, ensure_ascii=False, default=str).encode("utf-8")
            self._send(status, data, "application/json; charset=utf-8", headers)
        except (BrokenPipeError, ConnectionResetError):
            pass

    def _send(self, status: int, data: bytes, ctype: Optional[str], headers: Optional[dict] = None) -> None:
        headers = dict(headers or {})
        try:
            phrase = HTTPStatus(status).phrase
        except ValueError:
            phrase = ""
        self.send_response(status, phrase)
        if ctype:
            self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Cross-Origin-Resource-Policy", "same-origin")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Permissions-Policy", "camera=(), microphone=(), geolocation=()")
        headers.setdefault("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'; sandbox")
        headers.setdefault("Cache-Control", "no-store")
        for key, value in headers.items():
            self.send_header(key, value)
        if self.close_connection:
            self.send_header("Connection", "close")
        self.end_headers()
        if not getattr(self, "_head_only", False) and data:
            self.wfile.write(data)


class LibraryServer(ThreadingHTTPServer):
    daemon_threads = True
    allow_reuse_address = False

    def __init__(self, address: Tuple[str, int], app: App, quiet: bool = False, lan_host: Optional[str] = None):
        if ":" in address[0]:
            self.address_family = socket.AF_INET6
        self.app = app
        self.quiet = quiet
        self.lan_host = lan_host
        super().__init__(address, Handler)

    def verify_request(self, request: Any, client_address: Any) -> bool:
        # Defense in depth: only loopback and RFC 1918 peers, even on a LAN bind.
        if peer_allowed(client_address[0] if client_address else ""):
            return True
        if not self.quiet:
            log("Conexión rechazada de %s: no es loopback ni red local privada." % (client_address,))
        return False


def is_lan_ipv4(value: str) -> bool:
    """True only for a literal RFC 1918 IPv4 address (no names, no wildcards)."""
    try:
        addr = ipaddress.IPv4Address(value)
    except (ipaddress.AddressValueError, ValueError):
        return False
    return any(addr in net for net in LAN_NETWORKS)


def peer_allowed(value: str) -> bool:
    try:
        addr = ipaddress.ip_address(value)
    except ValueError:
        return False
    if addr.version == 6:
        mapped = addr.ipv4_mapped  # type: ignore[union-attr]
        if mapped is None:
            return addr == ipaddress.IPv6Address("::1")
        addr = mapped
    return addr in LOOPBACK_V4 or any(addr in net for net in LAN_NETWORKS)


def validate_host(host: str) -> str:
    """Returns the bind address or raises ValueError. Loopback or explicit RFC 1918 IPv4."""
    host = (host or "").strip()
    if host in LOOPBACK_HOSTS:
        return host
    if is_lan_ipv4(host):
        return str(ipaddress.IPv4Address(host))
    raise ValueError("Solo se permite 127.0.0.1, localhost, ::1 o una IPv4 privada concreta "
                     "(10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16); nunca 0.0.0.0, :: ni una IP pública.")


def make_server(app: App, host: str = "127.0.0.1", port: int = DEFAULT_PORT, quiet: bool = False) -> LibraryServer:
    host = validate_host(host)
    if port in FORBIDDEN_PORTS:
        raise ValueError("Los puertos 3000, 3001 y 5173 son de los jugadores; usa otro.")
    if host == "localhost":
        host = "127.0.0.1"
    lan_host = host if host not in LOOPBACK_HOSTS else None
    return LibraryServer((host, port), app, quiet=quiet, lan_host=lan_host)


def _host_arg(value: str) -> str:
    try:
        return validate_host(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(str(exc))


def main(argv: Optional[List[str]] = None) -> int:
    repo_default = HERE.parent.parent
    parser = argparse.ArgumentParser(description="Biblioteca local de recursos Synty de Bastión.")
    parser.add_argument("--host", default="127.0.0.1", type=_host_arg,
                        help="127.0.0.1 (por defecto), localhost, ::1 o una IPv4 privada concreta de este equipo "
                             "(RFC 1918, p. ej. 192.168.1.128) para abrirla desde la red local de confianza; "
                             "sin contraseña ni TLS, nunca 0.0.0.0 ni una IP pública")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT, help="puerto (por defecto %d)" % DEFAULT_PORT)
    parser.add_argument("--repo-root", type=Path, default=repo_default)
    parser.add_argument("--library-root", type=Path, default=DEFAULT_LIBRARY, help="originales Synty (solo lectura)")
    parser.add_argument("--data-dir", type=Path, default=None, help="caché del índice (por defecto data/asset-library)")
    parser.add_argument("--selections-dir", type=Path, default=None, help="por defecto docs/assets")
    parser.add_argument("--reindex", action="store_true", help="indexar al arrancar")
    parser.add_argument("--quiet", action="store_true", help="no registrar cada petición")
    args = parser.parse_args(argv)

    repo = args.repo_root.resolve()
    app = App(repo, args.library_root, args.data_dir or repo / "data" / "asset-library",
              args.selections_dir or repo / "docs" / "assets")
    try:
        server = make_server(app, args.host, args.port, quiet=args.quiet)
    except (ValueError, OSError) as exc:
        log("No se pudo abrir %s:%d: %s" % (args.host, args.port, exc))
        return 2
    if not app.library_available():
        log("Aviso: no se encuentra %s; se usa el índice anterior si existe." % args.library_root)
    if args.reindex:
        started, message = app.start_reindex()
        log(message)
    host, port = str(server.server_address[0]), int(server.server_address[1])
    shown = "[%s]" % host if ":" in host else host
    log("Biblioteca Synty en http://%s:%d/ (Ctrl+C para parar)" % (shown, port))
    if server.lan_host:
        log("Aviso: abierta a la red local sin contraseña ni TLS. Úsala solo en una red de confianza "
            "y no la expongas a Internet.")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
    return 0


if __name__ == "__main__":
    sys.exit(main())
