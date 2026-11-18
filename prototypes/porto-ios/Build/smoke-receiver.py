"""Loopback peer for the iOS Simulator. Accepts two synthetic PNGs in memory."""
import base64
import hashlib
import http.server
import json
import pathlib
import ssl
import sys
import urllib.parse


class Smoke(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"
    saved = {}

    def do_POST(self):
        parsed = urllib.parse.urlsplit(self.path)
        size = int(self.headers.get("Content-Length", 0))
        if not 0 <= size <= 1024 * 1024:
            return self.send_error(413)
        data = self.rfile.read(size)
        body = b""
        if parsed.path == "/porto/v1/session":
            offer = json.loads(data)
            assert offer["count"] == 2 and offer["selection"] == "both"
            body = b'{"token":"simulator-only"}'
            status = 200
        elif self.headers.get("Authorization") != "Bearer simulator-only":
            status = 403
        else:
            query = urllib.parse.parse_qs(parsed.query)
            key = query["asset"][0], query["version"][0]
            expected = self.headers["X-Porto-SHA256"]
            assert base64.b64decode(self.headers["X-Porto-Name"]).decode().endswith(".png")
            if parsed.path == "/porto/v1/check":
                status = 204 if self.saved.get(key) == expected else 404
            elif parsed.path == "/porto/v1/file" and data.startswith(b"\x89PNG") and hashlib.sha256(data).hexdigest() == expected:
                assert key not in self.saved
                self.saved[key] = expected
                status = 201
            else:
                status = 422
        self.send_response(status)
        if status != 204:
            self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)


if __name__ == "__main__":
    root = pathlib.Path(sys.argv[1])
    tls = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
    tls.load_cert_chain(root / "cert.pem", root / "key.pem")
    with http.server.ThreadingHTTPServer(("127.0.0.1", 53317), Smoke) as server:
        server.socket = tls.wrap_socket(server.socket, server_side=True)
        (root / "peer-ready").touch()
        server.serve_forever()
