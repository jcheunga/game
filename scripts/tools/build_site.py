#!/usr/bin/env python3
"""Build and check the public website in site/.

Reads site/site.json, renders site/src into site/dist, then checks that every
internal link, image, anchor, sitemap entry and page head resolves.

    python3 scripts/tools/build_site.py                  # preview build; unset values render as visible placeholders
    python3 scripts/tools/build_site.py --strict         # release build; fails until every required value is set
    python3 scripts/tools/build_site.py --strict --domain crownroad.game
    python3 scripts/tools/build_site.py --serve          # build, then preview at http://localhost:8000

Templates use {{key}} for values, <!--#include name.html --> for files in
site/src/_partials, and {{#link key}}text{{/link}} for store links that render
as plain text until their URL is configured.
"""

from __future__ import annotations

import argparse
import datetime as dt
import html.parser
import http.server
import io
import json
import re
import shutil
import sys
from pathlib import Path
from urllib.parse import urlsplit

ROOT = Path(__file__).resolve().parents[2]
SITE = ROOT / "site"
SRC = SITE / "src"
PARTIALS = SRC / "_partials"
CONFIG = SITE / "site.json"

REQUIRED = {
    "domain": "the public hostname, for example crownroad.game",
    "legal_name": "the person or company that publishes the game",
    "support_email": "a monitored inbox for players, privacy and security requests",
    "governing_law": "the jurisdiction whose law governs the terms, for example New South Wales, Australia",
    "effective_date": "the date the privacy policy and terms take effect (YYYY-MM-DD)",
}
LINKS = ("app_store", "google_play", "web_play")
TEXT_SUFFIXES = {".html", ".txt", ".xml", ".webmanifest"}
EMAIL_RE = re.compile(r"^[^@\s]+@[^@\s]+\.[^@\s]+$")
DOMAIN_RE = re.compile(r"^(?!-)[a-z0-9-]+(\.[a-z0-9-]+)+$")
INCLUDE_RE = re.compile(r"<!--#include\s+([\w.-]+)\s*-->")
LINK_RE = re.compile(r"\{\{#link\s+(\w+)\}\}(.*?)\{\{/link\}\}", re.S)
TOKEN_RE = re.compile(r"\{\{\s*(\w+)\s*\}\}")


class Problems:
    def __init__(self) -> None:
        self.errors: list[str] = []
        self.warnings: list[str] = []

    def error(self, msg: str) -> None:
        self.errors.append(msg)

    def warn(self, msg: str) -> None:
        self.warnings.append(msg)


def load_values(config_path: Path, strict: bool, expect_domain: str | None, problems: Problems) -> dict[str, str]:
    try:
        config = json.loads(config_path.read_text(encoding="utf-8"))
    except FileNotFoundError:
        problems.error(f"{config_path} is missing")
        config = {}
    except json.JSONDecodeError as exc:
        problems.error(f"{config_path} is not valid JSON: {exc}")
        config = {}

    report = problems.error if strict else problems.warn
    values: dict[str, str] = {}

    for key, meaning in REQUIRED.items():
        raw = str(config.get(key) or "").strip()
        if not raw:
            report(f"site.json: set \"{key}\" to {meaning}")
            values[key] = f"[set {key} in site/site.json]"
        else:
            values[key] = raw

    domain = str(config.get("domain") or "").strip().lower()
    if domain and not DOMAIN_RE.match(domain):
        problems.error(f"site.json: \"domain\" should be a bare hostname such as crownroad.game, not {domain!r}")
    if domain:
        values["domain"] = domain
    if expect_domain and domain != expect_domain.strip().lower():
        problems.error(
            f"site.json domain {domain!r} does not match the deployment domain {expect_domain!r} "
            "(CROWNROAD_SITE_DOMAIN in server/.env.production)"
        )

    email = str(config.get("support_email") or "").strip()
    if email and not EMAIL_RE.match(email):
        problems.error(f"site.json: \"support_email\" does not look like an email address: {email!r}")

    effective = str(config.get("effective_date") or "").strip()
    if effective:
        try:
            date = dt.date.fromisoformat(effective)
            values["effective_date_long"] = f"{date.day} {date:%B %Y}"
        except ValueError:
            problems.error(f"site.json: \"effective_date\" must be YYYY-MM-DD, not {effective!r}")
            values["effective_date_long"] = effective
    else:
        values["effective_date_long"] = values["effective_date"]

    links = config.get("links") or {}
    any_link = False
    for key in LINKS:
        url = str(links.get(key) or "").strip()
        if url and urlsplit(url).scheme != "https":
            problems.error(f"site.json: links.{key} must be an https:// URL")
        values[f"link:{key}"] = url
        any_link = any_link or bool(url)
    values["availability_label"] = "Available on" if any_link else "Coming to"
    values["year"] = str(dt.date.today().year)
    # RFC 9116 requires an expiry under a year away; each deploy refreshes it.
    expires = dt.datetime.now(dt.timezone.utc).replace(microsecond=0) + dt.timedelta(days=364)
    values["security_expires"] = expires.isoformat().replace("+00:00", "Z")
    return values


def render(text: str, values: dict[str, str], source: Path, problems: Problems) -> str:
    def include(match: re.Match[str]) -> str:
        partial = PARTIALS / match.group(1)
        if not partial.is_file():
            problems.error(f"{source}: include {match.group(1)} not found in site/src/_partials")
            return ""
        return partial.read_text(encoding="utf-8").rstrip("\n")

    text = INCLUDE_RE.sub(include, text)

    def link(match: re.Match[str]) -> str:
        key, label = match.group(1), match.group(2)
        if key not in LINKS:
            problems.error(f"{source}: unknown link key {key!r}")
            return label
        url = values.get(f"link:{key}", "")
        if url:
            return f'<a href="{url}" rel="noopener">{label}</a>'
        # Once any store is live the heading reads "Available on", so mark the rest.
        suffix = " · soon" if values["availability_label"] == "Available on" else ""
        return f"<span>{label}{suffix}</span>"

    text = LINK_RE.sub(link, text)

    def token(match: re.Match[str]) -> str:
        key = match.group(1)
        if key not in values:
            problems.error(f"{source}: unknown template value {{{{{key}}}}}")
            return match.group(0)
        return values[key]

    return TOKEN_RE.sub(token, text)


class PageScan(html.parser.HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.ids: set[str] = set()
        self.refs: list[tuple[str, str]] = []
        self.title = False
        self.meta: dict[str, str] = {}
        self.canonical: str | None = None
        self.lang: str | None = None

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        a = {k: (v or "") for k, v in attrs}
        if "id" in a:
            self.ids.add(a["id"])
        if tag == "html":
            self.lang = a.get("lang")
        if tag == "title":
            self.title = True
        if tag == "meta":
            name = a.get("name") or a.get("property")
            if name:
                self.meta[name] = a.get("content", "")
        if tag == "link" and a.get("rel") == "canonical":
            self.canonical = a.get("href")
        for attr in ("href", "src"):
            if attr in a:
                self.refs.append((tag, a[attr]))
        if "srcset" in a:
            for part in a["srcset"].split(","):
                self.refs.append((tag, part.strip().split(" ")[0]))


def resolve(out: Path, page: Path, ref: str) -> tuple[Path | None, str]:
    parts = urlsplit(ref)
    path = parts.path
    if not path:
        return page, parts.fragment
    target = (out / path.lstrip("/")) if path.startswith("/") else (page.parent / path)
    if path.endswith("/"):
        target = target / "index.html"
    elif not target.suffix and target.with_suffix(".html").is_file():
        target = target.with_suffix(".html")
    return target, parts.fragment


def check(out: Path, values: dict[str, str], problems: Problems) -> int:
    pages = sorted(out.rglob("*.html"))
    scans: dict[Path, PageScan] = {}
    for page in pages:
        scan = PageScan()
        scan.feed(page.read_text(encoding="utf-8"))
        scans[page] = scan

    domain = values["domain"]
    for page, scan in scans.items():
        rel = page.relative_to(out)
        text = page.read_text(encoding="utf-8")
        if "{{" in text or "<!--#include" in text:
            problems.error(f"{rel}: unrendered template syntax")
        if not scan.title:
            problems.error(f"{rel}: missing <title>")
        if not scan.lang:
            problems.error(f"{rel}: missing <html lang>")
        if not scan.meta.get("description"):
            problems.error(f"{rel}: missing meta description")
        noindex = "noindex" in scan.meta.get("robots", "")
        if not noindex and not scan.canonical:
            problems.error(f"{rel}: missing canonical link")
        if scan.canonical and not scan.canonical.startswith(f"https://{domain}/"):
            problems.error(f"{rel}: canonical {scan.canonical} is not on https://{domain}/")
        for tag, ref in scan.refs:
            parts = urlsplit(ref)
            if parts.scheme in ("mailto", "tel", "data"):
                continue
            if parts.scheme or parts.netloc:
                host = parts.netloc.lower()
                if host == domain or host.endswith("." + domain):
                    local = out / parts.path.lstrip("/")
                    if parts.path not in ("", "/") and not local.exists() and not local.with_suffix(".html").exists():
                        problems.error(f"{rel}: absolute link {ref} has no matching file in the build")
                continue
            target, fragment = resolve(out, page, ref)
            if target is None or not target.exists():
                problems.error(f"{rel}: <{tag}> {ref} does not resolve to a file")
                continue
            if fragment and target.suffix == ".html":
                ids = scans.get(target.resolve()) or scans.get(target)
                if ids is not None and fragment not in ids.ids:
                    problems.error(f"{rel}: {ref} points at a missing #{fragment}")

    sitemap = out / "sitemap.xml"
    if sitemap.is_file():
        locs = re.findall(r"<loc>([^<]+)</loc>", sitemap.read_text(encoding="utf-8"))
        listed = set()
        for loc in locs:
            parts = urlsplit(loc)
            if parts.netloc != domain:
                problems.error(f"sitemap.xml: {loc} is not on {domain}")
                continue
            target, _ = resolve(out, out / "index.html", parts.path or "/")
            if target is None or not target.exists():
                problems.error(f"sitemap.xml: {loc} has no page")
            else:
                listed.add(target.resolve())
        for page, scan in scans.items():
            if "noindex" not in scan.meta.get("robots", "") and page.resolve() not in listed:
                problems.warn(f"sitemap.xml does not list {page.relative_to(out)}")
    else:
        problems.error("sitemap.xml is missing")

    return len(pages)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--strict", action="store_true", help="fail on any unset launch value (use for deploys)")
    parser.add_argument("--domain", help="fail unless site.json uses this domain")
    parser.add_argument("--config", type=Path, default=CONFIG, help="launch values (default: site/site.json)")
    parser.add_argument("--out", type=Path, default=SITE / "dist", help="output directory (default: site/dist)")
    parser.add_argument("--serve", nargs="?", type=int, const=8000, metavar="PORT",
                        help="after building, preview the site locally with the same clean URLs Caddy uses")
    args = parser.parse_args()

    problems = Problems()
    values = load_values(args.config, args.strict, args.domain, problems)

    out: Path = args.out.resolve()
    # Empty the directory rather than replacing it: Caddy bind-mounts it, and a
    # recreated directory would leave the running container serving nothing.
    out.mkdir(parents=True, exist_ok=True)
    for child in out.iterdir():
        if child.is_dir() and not child.is_symlink():
            shutil.rmtree(child)
        else:
            child.unlink()

    files = 0
    for source in sorted(SRC.rglob("*")):
        rel = source.relative_to(SRC)
        if source.is_dir() or rel.parts[0] == "_partials" or source.name == ".DS_Store":
            continue
        target = out / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if source.suffix in TEXT_SUFFIXES:
            target.write_text(render(source.read_text(encoding="utf-8"), values, rel, problems), encoding="utf-8")
        else:
            shutil.copy2(source, target)
        files += 1

    pages = check(out, values, problems)

    for msg in problems.warnings:
        print(f"warning: {msg}")
    for msg in problems.errors:
        print(f"error: {msg}", file=sys.stderr)
    if problems.errors:
        print(f"Website build failed with {len(problems.errors)} error(s).", file=sys.stderr)
        return 1
    mode = "release" if args.strict else "preview"
    print(f"Website {mode} build OK: {pages} pages, {files} files -> {out.relative_to(ROOT) if out.is_relative_to(ROOT) else out}")
    if not args.strict and problems.warnings:
        print("Run with --strict before deploying; unset values are shown as placeholders.")
    if args.serve:
        serve(out, args.serve)
    return 0


def serve(out: Path, port: int) -> None:
    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *a, **kw):
            super().__init__(*a, directory=str(out), **kw)

        def send_head(self):
            # Mirror Caddy's try_files {path} {path}.html and its 404 page.
            path = urlsplit(self.path).path
            local = out / path.lstrip("/")
            if not local.exists() and local.with_suffix(".html").is_file():
                self.path = path + ".html"
            elif not local.exists():
                self.path = "/404.html"
                body = (out / "404.html").read_bytes()
                self.send_response(404)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.end_headers()
                return io.BytesIO(body)
            return super().send_head()

    with http.server.ThreadingHTTPServer(("127.0.0.1", port), Handler) as httpd:
        print(f"Previewing at http://localhost:{port}/ (Ctrl+C to stop)")
        try:
            httpd.serve_forever()
        except KeyboardInterrupt:
            pass


if __name__ == "__main__":
    sys.exit(main())
