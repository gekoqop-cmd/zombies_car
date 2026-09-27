"""Packs the WebGL build (unity/Builds/WebGL) into the archive the Yandex Games console accepts.

Checks the Yandex requirements that can be checked offline:
  - index.html in the archive root (not in a sub-folder);
  - archive <= 100 MB;
  - file names ASCII only, no spaces (Cyrillic / spaces are rejected);
  - index.html uses relative paths only (no absolute URLs, no leading "/" except /sdk.js).
Writes unity/Builds/Rustwave_yandex.zip and prints the file list and sizes.
Usage: python tools/yandex/pack.py
"""
import os
import re
import sys
import zipfile

ROOT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "Builds")
BUILD = os.path.join(ROOT, "WebGL")
OUT = os.path.join(ROOT, "Rustwave_yandex.zip")
MAX_MB = 100


def fail(message):
    print("ERROR:", message)
    sys.exit(1)


def main():
    index = os.path.join(BUILD, "index.html")
    if not os.path.isfile(index):
        fail(f"no index.html in {BUILD} (build first: Tools > Build WebGL Player)")
    html = open(index, encoding="utf-8").read()
    for url in re.findall(r'(?:src|href)\s*=\s*"([^"]+)"', html):
        if re.match(r"^(https?:)?//", url) or (url.startswith("/") and url != "/sdk.js"):
            fail(f"index.html references a non-relative path: {url}")

    files = []
    for folder, _, names in os.walk(BUILD):
        for name in names:
            path = os.path.join(folder, name)
            rel = os.path.relpath(path, BUILD).replace(os.sep, "/")
            if not re.fullmatch(r"[A-Za-z0-9._/\-]+", rel):
                fail(f"file name not allowed by Yandex (spaces / non-ASCII): {rel}")
            files.append((rel, path))

    if os.path.exists(OUT):
        os.remove(OUT)
    # Build files are already Brotli-compressed: store them; compress the rest.
    with zipfile.ZipFile(OUT, "w") as z:
        for rel, path in sorted(files):
            method = zipfile.ZIP_STORED if rel.endswith((".unityweb", ".br", ".gz", ".png", ".jpg")) else zipfile.ZIP_DEFLATED
            z.write(path, rel, compress_type=method)

    with zipfile.ZipFile(OUT) as z:
        names = z.namelist()
        if "index.html" not in names:
            fail("index.html is not in the archive root")
        for info in z.infolist():
            print(f"  {info.file_size / 1048576:8.2f} MB  {info.filename}")
    size = os.path.getsize(OUT) / 1048576
    print(f"{OUT}: {size:.1f} MB, {len(names)} files, index.html in the root")
    if size > MAX_MB:
        fail(f"archive is {size:.1f} MB, Yandex allows {MAX_MB} MB")


if __name__ == "__main__":
    main()
