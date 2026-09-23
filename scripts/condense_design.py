"""Condense .dc.html design files: drop injected preview runtime, inline styles,
and helmet boilerplate; keep structure, semantic attributes, text, and DC logic."""
import sys, re, os
from html.parser import HTMLParser

KEEP_ATTRS = {"href", "role", "alt", "id", "type", "placeholder", "value", "name",
              "for", "lang", "dir", "title", "src", "label", "open", "checked",
              "disabled", "selected", "colspan", "scope", "hint-size"}
VOID = {"br", "img", "input", "meta", "link", "hr", "source", "col"}

class C(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.out = []
        self.depth = 0
        self.skip = 0  # inside injected / style
        self.in_script = False
        self.script_keep = False

    def attrs_str(self, attrs):
        parts = []
        for k, v in attrs:
            if k == "style" or k == "class":
                # keep hints that carry layout meaning: grid templates, widths
                if k == "style" and v:
                    hints = re.findall(r"(grid-template-columns:[^;]+|width:\s*\d+px|max-width:[^;]+|position:sticky)", v)
                    if hints:
                        parts.append('~"' + ";".join(h.strip() for h in hints) + '"')
                continue
            if k.startswith("aria-") or k.startswith("data-") or k.startswith("sc-") or k.startswith(":") or k in KEEP_ATTRS or k.startswith("@"):
                if k.startswith("data-omelette"):
                    continue
                parts.append(f'{k}="{v}"' if v is not None else k)
        return (" " + " ".join(parts)) if parts else ""

    def handle_starttag(self, tag, attrs):
        d = dict(attrs)
        if self.skip:
            if tag not in VOID:
                self.skip += 1
            return
        if "data-omelette-injected" in d or tag == "style":
            self.skip = 1
            return
        if tag == "script":
            if d.get("src"):
                return
            self.in_script = True
            self.out.append("  " * self.depth + f"<script{self.attrs_str(attrs)}>")
            return
        if tag in ("link", "meta", "svg", "path", "g", "rect", "circle", "line", "polyline", "defs"):
            if tag == "svg":
                self.out.append("  " * self.depth + "<svg/>")
                self.skip = 1
            return
        self.out.append("  " * self.depth + f"<{tag}{self.attrs_str(attrs)}>")
        if tag not in VOID:
            self.depth += 1

    def handle_endtag(self, tag):
        if self.skip:
            self.skip -= 1
            return
        if tag == "script":
            if self.in_script:
                self.out.append("  " * self.depth + "</script>")
            self.in_script = False
            return
        if tag in VOID or tag in ("link", "meta"):
            return
        self.depth = max(0, self.depth - 1)
        # collapse "<tag>text" + "</tag>"
        self.out.append("  " * self.depth + f"</{tag}>")

    def handle_data(self, data):
        if self.skip:
            return
        if self.in_script:
            self.out.append(data.strip("\n"))
            return
        t = re.sub(r"\s+", " ", data).strip()
        if t:
            self.out.append("  " * self.depth + t)

def condense(src):
    p = C()
    p.feed(src)
    lines = p.out
    # merge <tag>, text, </tag> triples onto one line
    merged = []
    i = 0
    while i < len(lines):
        a = lines[i]
        if i + 2 < len(lines):
            b, c = lines[i + 1].strip(), lines[i + 2].strip()
            m = re.match(r"\s*<([a-z0-9-]+)", a)
            if m and not b.startswith("<") and c == f"</{m.group(1)}>":
                merged.append(a + b + c)
                i += 3
                continue
        if i + 1 < len(lines):
            m = re.match(r"\s*<([a-z0-9-]+)[^>]*>$", a)
            if m and lines[i + 1].strip() == f"</{m.group(1)}>":
                merged.append(a[:-1] + "/>")
                i += 2
                continue
        merged.append(a)
        i += 1
    return "\n".join(merged)

if __name__ == "__main__":
    src_dir, out_dir = sys.argv[1], sys.argv[2]
    os.makedirs(out_dir, exist_ok=True)
    for f in sorted(os.listdir(src_dir)):
        if f.endswith(".dc.html"):
            with open(os.path.join(src_dir, f), encoding="utf-8") as fh:
                s = fh.read()
            out = condense(s)
            name = f.replace(".dc.html", ".txt")
            with open(os.path.join(out_dir, name), "w", encoding="utf-8") as fh:
                fh.write(out)
            print(f"{len(s):>7} -> {len(out):>7}  {f}")
