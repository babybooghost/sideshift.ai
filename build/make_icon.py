#!/usr/bin/env python3
"""Generate build/icon.png (1024x1024 RGBA) with no third-party deps.
Indigo->violet rounded-square with a white lightning bolt. electron-builder
derives the mac .icns and win .ico from this single PNG."""
import zlib, struct, os

W = H = 1024
PAD = 48
R = 220  # corner radius

# gradient endpoints
C0 = (99, 102, 241)    # #6366f1 indigo
C1 = (139, 92, 246)    # #8b5cf6 violet

# lightning bolt polygon (canvas coords)
BOLT = [(600, 225), (356, 560), (500, 560), (430, 800),
        (704, 452), (556, 452), (612, 225)]


def in_rounded_rect(x, y):
    x0, y0, x1, y1 = PAD, PAD, W - PAD, H - PAD
    if x < x0 or x > x1 or y < y0 or y > y1:
        return False
    # corner circles
    for cx, cy in ((x0 + R, y0 + R), (x1 - R, y0 + R),
                   (x0 + R, y1 - R), (x1 - R, y1 - R)):
        inx = (cx == x0 + R and x < x0 + R) or (cx == x1 - R and x > x1 - R)
        iny = (cy == y0 + R and y < y0 + R) or (cy == y1 - R and y > y1 - R)
        if inx and iny:
            return (x - cx) ** 2 + (y - cy) ** 2 <= R * R
    return True


def in_poly(x, y, poly):
    inside = False
    n = len(poly)
    j = n - 1
    for i in range(n):
        xi, yi = poly[i]
        xj, yj = poly[j]
        if ((yi > y) != (yj > y)) and (x < (xj - xi) * (y - yi) / (yj - yi) + xi):
            inside = not inside
        j = i
    return inside


raw = bytearray()
for y in range(H):
    raw.append(0)  # PNG filter byte (none) per scanline
    t_row = y / (H - 1)
    for x in range(W):
        if not in_rounded_rect(x, y):
            raw += bytes((0, 0, 0, 0))
            continue
        if in_poly(x, y, BOLT):
            raw += bytes((255, 255, 255, 255))
            continue
        t = (x / (W - 1) + t_row) / 2.0  # diagonal gradient
        r = int(C0[0] + (C1[0] - C0[0]) * t)
        g = int(C0[1] + (C1[1] - C0[1]) * t)
        b = int(C0[2] + (C1[2] - C0[2]) * t)
        raw += bytes((r, g, b, 255))


def chunk(tag, data):
    return (struct.pack(">I", len(data)) + tag + data
            + struct.pack(">I", zlib.crc32(tag + data) & 0xffffffff))


png = b"\x89PNG\r\n\x1a\n"
png += chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 6, 0, 0, 0))
png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
png += chunk(b"IEND", b"")

out = os.path.join(os.path.dirname(__file__), "icon.png")
with open(out, "wb") as f:
    f.write(png)
print("wrote", out, os.path.getsize(out), "bytes")
