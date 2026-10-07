"""English for the game: collects every Russian text and writes the table the game reads.

python tools/loc/extract.py          -> tools/loc/keys_ru.txt   every Russian text (keys), new ones marked in
                                                                missing.txt (no translation in en_table.py yet)
                                        unity/Assets/Resources/Localization/en.txt   "russian TAB english" per line

Where the texts are: string literals of the runtime scripts (not the editor builders: what they make is in the
assets; not the developer cheats), and the serialized strings of the prefabs, scenes and data assets. A key is the
text with its numbers and interpolated values as placeholders {0}, {1}, … in order (the game does the same with the
numbers of a text when it looks it up; any other value is matched by the placeholder: Loc.cs).
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", ".."))
ASSETS = os.path.join(ROOT, "unity", "Assets")
OUT = os.path.join(ASSETS, "Resources", "Localization", "en.txt")
BS = chr(92)
CYR = re.compile("[Ѐ-ӿ]")
NUMBER = re.compile(r"\d+(?:[.,]\d+)?")
PLACEHOLDER = re.compile(r"\{(\d+)\}")

SKIP_DIRS = ("Editor", "DevTools")
SKIP_LINE = re.compile(r"Debug\.Log|\[Tooltip|\[Header|\[MenuItem|throw new|LogWarning|LogError|^\s*//")
SKIP_FIELDS = {"m_Name", "m_EditorClassIdentifier", "m_TagString"}


# ------------------------------------------------------------------ keys
def key_of(text):
    """Numbers and placeholders -> {0}, {1}, … in order of appearance."""
    out, n, i = [], 0, 0
    pattern = re.compile(r"\{\d+\}|\d+(?:[.,]\d+)?")
    for m in pattern.finditer(text):
        out.append(text[i:m.start()])
        out.append("{%d}" % n)
        n += 1
        i = m.end()
    out.append(text[i:])
    return "".join(out)


# ------------------------------------------------------------------ C#
def cs_literals(line):
    """String literals of a line of C#: interpolated holes become {0}…, escapes decoded."""
    i = 0
    while i < len(line):
        c = line[i]
        if line.startswith("//", i):
            return
        if c == "'":                                      # a char literal: skip it
            j = line.find("'", i + 2 if line[i + 1:i + 2] == BS else i + 1)
            i = (j + 1) if j > 0 else i + 1
            continue
        interp = c == "$" and line[i + 1:i + 2] == '"'
        verbatim = c == "@" and line[i + 1:i + 2] == '"'
        if c == '"' or interp or verbatim:
            j = i + (2 if interp or verbatim else 1)
            buf, holes, depth = [], 0, 0
            while j < len(line):
                ch = line[j]
                if interp and ch == "{":
                    if line[j + 1:j + 2] == "{":
                        buf.append("{")
                        j += 2
                        continue
                    depth = 1
                    j += 1
                    while j < len(line) and depth:
                        if line[j] == "{":
                            depth += 1
                        elif line[j] == "}":
                            depth -= 1
                        elif line[j] == '"':                # a string inside the hole
                            k = line.find('"', j + 1)
                            j = k if k > 0 else j
                        j += 1
                    buf.append("{%d}" % holes)
                    holes += 1
                    continue
                if interp and ch == "}" and line[j + 1:j + 2] == "}":
                    buf.append("}")
                    j += 2
                    continue
                if ch == BS and not verbatim:
                    nxt = line[j + 1:j + 2]
                    buf.append({"n": "\n", "t": "\t", '"': '"', BS: BS, "r": ""}.get(nxt, nxt))
                    j += 2
                    continue
                if ch == '"':
                    if verbatim and line[j + 1:j + 2] == '"':
                        buf.append('"')
                        j += 2
                        continue
                    break
                buf.append(ch)
                j += 1
            yield "".join(buf)
            i = j + 1
            continue
        i += 1


def from_scripts():
    texts = []
    for dp, dn, fn in os.walk(os.path.join(ASSETS, "Scripts")):
        rel = os.path.relpath(dp, os.path.join(ASSETS, "Scripts")).replace("\\", "/")
        if rel.split("/")[0] in SKIP_DIRS:
            continue
        for f in fn:
            if not f.endswith(".cs") or f == "Loc.cs":
                continue
            in_doc = False
            for line in open(os.path.join(dp, f), encoding="utf-8"):
                if SKIP_LINE.search(line):
                    continue
                for lit in cs_literals(line):
                    if CYR.search(lit):
                        texts.append((lit, f"{rel}/{f}"))
    return texts


# ------------------------------------------------------------------ Unity YAML
def yaml_unquote(s):
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == BS and i + 1 < len(s):
            n = s[i + 1]
            if n == "u":
                out.append(chr(int(s[i + 2:i + 6], 16)))
                i += 6
                continue
            if n == "x":
                out.append(chr(int(s[i + 2:i + 4], 16)))
                i += 4
                continue
            out.append({"n": "\n", "t": "\t", '"': '"', BS: BS, "/": "/", "_": " ", "r": "", " ": " ",
                        "0": "\0", "L": " ", "P": " ", "N": "\u0085"}.get(n, n))
            i += 2
            continue
        out.append(c)
        i += 1
    return "".join(out)


FIELD = re.compile(r"^(\s*)(-\s+)?([A-Za-z_][\w ]*): ?(.*)$")


def yaml_strings(path):
    lines = open(path, encoding="utf-8", errors="replace").read().split("\n")
    i = 0
    while i < len(lines):
        m = FIELD.match(lines[i])
        i += 1
        if not m:
            continue
        indent, field, value = len(m.group(1)) + len(m.group(2) or ""), m.group(3), m.group(4)
        if field in SKIP_FIELDS or not value:
            continue
        if value.startswith('"'):
            body = value[1:]
            # Until the closing quote (an escaped one does not count); a line break folds into a space.
            while True:
                k, closed = 0, False
                while k < len(body):
                    if body[k] == BS:
                        k += 2
                        continue
                    if body[k] == '"':
                        closed = True
                        break
                    k += 1
                if closed or i >= len(lines):
                    body = body[:k]
                    break
                nxt = lines[i].strip()
                i += 1
                body = body[:-1] + nxt if body.endswith(BS) else body.rstrip() + " " + nxt
            text = yaml_unquote(body)
        elif value.startswith("'"):
            body = value[1:]
            while not re.search(r"(?<!')'(?:'')*$", body.rstrip()) or body.rstrip().endswith("''") and body.count("'") % 2 == 0:
                if i >= len(lines):
                    break
                body = body.rstrip() + " " + lines[i].strip()
                i += 1
            text = body.rstrip()[:-1].replace("''", "'")
        else:
            text = value
            # Plain scalar folded over more indented lines.
            while i < len(lines) and lines[i].strip() and (len(lines[i]) - len(lines[i].lstrip())) > indent \
                    and not FIELD.match(lines[i]) and not lines[i].lstrip().startswith("- "):
                text += " " + lines[i].strip()
                i += 1
        if CYR.search(text):
            yield field, text


def from_assets():
    texts = []
    for dp, dn, fn in os.walk(ASSETS):
        for f in fn:
            if f.endswith((".prefab", ".unity", ".asset")):
                p = os.path.join(dp, f)
                rel = os.path.relpath(p, ASSETS).replace("\\", "/")
                for field, text in yaml_strings(p):
                    texts.append((text, rel))
    return texts


def escape(s):
    return s.replace(BS, BS + BS).replace("\n", BS + "n").replace("\t", BS + "t")


def main():
    sys.path.insert(0, HERE)
    from en_table import EN
    found = {}
    for text, where in from_scripts() + from_assets():
        k = key_of(text)
        found.setdefault(k, where)
    keys = sorted(found)
    with open(os.path.join(HERE, "keys_ru.txt"), "w", encoding="utf-8") as f:
        for k in keys:
            f.write(f"{escape(k)}\t{found[k]}\n")
    table = {key_of(k): v for k, v in EN.items()}
    missing = [k for k in keys if k not in table]
    with open(os.path.join(HERE, "missing.txt"), "w", encoding="utf-8") as f:
        for k in missing:
            f.write(f"{escape(k)}\t{found[k]}\n")
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        for k in sorted(table):
            f.write(f"{escape(k)}\t{escape(table[k])}\n")
    print(f"{len(keys)} texts, {len(table)} translated, {len(missing)} missing (tools/loc/missing.txt)")


if __name__ == "__main__":
    main()
