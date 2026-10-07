"""Loopback-only MP4/Relay fixture for the opt-in native diagnostic. No real Relay access."""
import argparse
import datetime
import http.server
import json
import pathlib
import re
import threading
import urllib.parse

parser = argparse.ArgumentParser()
parser.add_argument("--video", required=True, type=pathlib.Path)
parser.add_argument("--port", type=int, default=49183)
args = parser.parse_args()
sessions = {}
guard = threading.Lock()


class Fixture(http.server.BaseHTTPRequestHandler):
    def log_message(self, format, *values):
        print(format % values, flush=True)

    def json(self, data, status=200):
        body = json.dumps(data).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        if self.command != "HEAD":
            self.wfile.write(body)

    def media(self, path):
        if path != "/video.mp4" and not path.startswith("/play/"):
            return False
        if path.startswith("/play/") and path.split("/")[-1] not in sessions:
            self.send_error(410)
            return True
        size = args.video.stat().st_size
        start, end = 0, size - 1
        requested = self.headers.get("Range")
        if requested:
            match = re.fullmatch(r"bytes=(\d*)-(\d*)", requested)
            if not match:
                self.send_error(416)
                return True
            first, last = match.groups()
            if first:
                start = int(first)
                end = min(int(last), size - 1) if last else size - 1
            elif last:
                start = max(0, size - int(last))
            if start > end or start >= size:
                self.send_response(416)
                self.send_header("Content-Range", f"bytes */{size}")
                self.end_headers()
                return True
        self.send_response(206 if requested else 200)
        self.send_header("Content-Type", "video/mp4")
        self.send_header("Accept-Ranges", "bytes")
        self.send_header("Content-Length", str(end - start + 1))
        if requested:
            self.send_header("Content-Range", f"bytes {start}-{end}/{size}")
        self.end_headers()
        if self.command != "HEAD":
            try:
                with args.video.open("rb") as source:
                    source.seek(start)
                    remaining = end - start + 1
                    while remaining:
                        block = source.read(min(65536, remaining))
                        if not block:
                            break
                        self.wfile.write(block)
                        remaining -= len(block)
            except (BrokenPipeError, ConnectionResetError):
                pass
        return True

    def do_HEAD(self):
        self.do_GET()

    def do_GET(self):
        path = urllib.parse.urlparse(self.path).path
        if self.media(path):
            return
        if self.headers.get("Authorization") != "Bearer tv-loopback-fixture":
            self.json({"code": "unauthorized", "message": "Fixture token required"}, 401)
            return
        route = path.removeprefix("/integrations/game/v1/")
        if route == "capabilities":
            self.json({"protocolVersion": 1, "profiles": ["mp4-h264-aac"], "subtitleModes": ["none", "burned"]})
        elif route == "search":
            self.json({"items": [{"id": "title-1", "title": "Loopback TV fixture", "year": 2026, "mediaType": "anime"}]})
        elif route == "titles/title-1":
            self.json({"id": "title-1", "title": "Loopback TV fixture", "seasons": [{"number": 1, "title": "Season 1"}]})
        elif route == "titles/title-1/episodes":
            self.json({"items": [{"id": "episode-1", "title": "Moon Landing", "seasonNumber": 1, "number": 1}]})
        elif route == "episodes/episode-1/sources":
            self.json({"items": [{"id": "fixture", "name": "Loopback MP4", "playable": True, "versions": [{"id": "original", "label": "Original 1080p", "playable": True}]}]})
        elif route.startswith("playbacks/") and route.split("/")[-1] in sessions:
            session = sessions[route.split("/")[-1]]
            session["status"] = "ready"
            self.json(session)
        else:
            self.json({"code": "not_found", "message": "Unknown fixture route"}, 404)

    def do_POST(self):
        if self.headers.get("Authorization") != "Bearer tv-loopback-fixture":
            self.json({"code": "unauthorized", "message": "Fixture token required"}, 401)
            return
        body = json.loads(self.rfile.read(int(self.headers.get("Content-Length", "0"))) or "{}")
        path = urllib.parse.urlparse(self.path).path
        route = path.removeprefix("/integrations/game/v1/")
        if route == "playbacks":
            assert body["profile"] == "mp4-h264-aac" and body["episodeId"] == "episode-1"
            with guard:
                identity = "session-" + str(len(sessions) + 1)
                sessions[identity] = {"id": identity, "status": "preparing", "title": "Moon Landing", "episodeId": body["episodeId"], "sourceId": body.get("sourceId") or "fixture", "versionId": body.get("versionId") or "original", "profile": body["profile"], "subtitleMode": body["subtitleMode"], "mediaUrl": f"http://127.0.0.1:{args.port}/play/{identity}", "mimeType": "video/mp4", "durationSeconds": 134.7167, "width": 1920, "height": 1080, "seekable": True, "expiresAt": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(hours=1)).isoformat(), "message": "Loopback fixture preparing"}
            self.json(sessions[identity], 202)
        elif route.endswith("/refresh"):
            identity = route.split("/")[-2]
            session = sessions[identity]
            session["status"] = "ready"
            session["mediaUrl"] = f"http://127.0.0.1:{args.port}/play/{identity}?renewed={body['positionSeconds']}"
            self.json(session)
        elif route.endswith("/stop"):
            identity = route.split("/")[-2]
            sessions.pop(identity, None)
            self.send_response(204)
            self.end_headers()
        else:
            self.json({"code": "not_found", "message": "Unknown fixture route"}, 404)


http.server.ThreadingHTTPServer(("127.0.0.1", args.port), Fixture).serve_forever()
