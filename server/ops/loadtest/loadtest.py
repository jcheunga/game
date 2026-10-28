#!/usr/bin/env python3
"""Crownroad API load test: a ramped, realistic player mix against a running server.

Standard library only (k6 was not installed where this was written, and the
deployment VM already needs Python 3 for deploy.sh). Each virtual player (VU)
is a thread with its own keep-alive connection, like a phone's HttpClient, and
plays the same calls the game makes, at the game's own intervals:

  launch   GET /health, POST /player-profile (new guest), POST /player-profile
           (profile sync with the session), GET /cloud-save/info (session check),
           GET /wallet/adjustments, GET /wallet, GET /cloud-save/download,
           GET /challenge-feed
  battle   think for the battle's length, then POST /cloud-save/upload after a
           win, analytics batches of 10 events (consenting players only),
           challenge sync + board, daily score + leaderboard, achievements,
           shop visits (wallet), room-directory polling every 10 s, a few
           hosted rooms polling session + scoreboard, and GET /health every 45 s
  purchase a light rate of POST /purchase/validate with platform "loadtest",
           which the server rejects before contacting Google or Apple (400)

Usage (see server/ops/README.md for the full guide):

  python3 loadtest.py                                    # local server, default ramp
  python3 loadtest.py --stages 10s:5,15s:5,5s:0 --time-scale 0.1
  python3 loadtest.py --target https://staging-api.example.com --stages 2m:50,5m:200,5m:200,1m:0

It defaults to http://localhost:8080; any other server needs an explicit
--target. Exit code: 0 when every threshold passes, 1 when one fails, 2 on a
usage error.
"""

from __future__ import annotations

import argparse
import datetime as dt
import http.client
import json
import os
import random
import re
import socket
import ssl
import sys
import threading
import time
import urllib.parse
import uuid

DEFAULT_TARGET = "http://localhost:8080"
DEFAULT_STAGES = "30s:10,60s:25,60s:25,30s:0"
USER_AGENT = "crownroad-loadtest/1"

# Board codes the server seeds (server/Database.cs SeedDefaultFeed).
BOARD_CODES = [
    "CH-01-DAILY-1001",
    "CH-02-DAILY-2002",
    "CH-03-LOCK-3003",
    "CH-04-DAILY-4004",
    "CH-05-LOCK-5005",
]
ACHIEVEMENTS = [
    "first_victory", "stage_10", "stage_25", "stage_50", "boss_slayer", "full_deck",
    "spell_master", "gold_hoarder", "perfect_defense", "endless_wave_10",
]
UNIT_IDS = [
    "player_brawler", "player_shooter", "player_defender", "player_breacher",
    "player_marksman", "player_grenadier", "player_lancer", "player_cleric",
]

# How often a player does each thing. Times are the game's own; --time-scale
# shortens player think time (battles, menus) but not the client's polling
# timers (health every 45 s, rooms every 10 s), which run on the wall clock.
MIX = {
    "battle_seconds": (60.0, 180.0),       # one stage, start to result screen
    "win_probability": 0.75,               # a won battle uploads the cloud save
    "analytics_consent": 0.35,             # analytics is opt-in, off by default
    "challenge_per_battle": 0.25,          # challenge sync + its leaderboard
    "daily_per_battle": 0.10,              # daily score + daily leaderboard
    "achievements_per_battle": 0.20,
    "shop_per_battle": 0.15,               # refunds check + wallet
    "multiplayer_menu_per_battle": 0.15,   # room directory, polled every 10 s
    "menu_seconds": (20.0, 60.0),
    "host_room_per_battle": 0.03,          # host polls session + scoreboard
    "room_seconds": (30.0, 90.0),
    "restore_per_battle": 0.03,            # cloud-save download (restore)
    "profile_sync_on_menu": 0.30,
    "session_minutes": 15.0,               # app session length in game time
    "health_interval": 45.0,               # NetworkStatus.CheckIntervalSeconds
    "room_poll_interval": 10.0,            # MultiplayerMenu auto refresh
}

# Per-route latency budgets in ms (p95, p99). Others use the defaults.
ROUTE_BUDGETS = {
    "POST /cloud-save/upload": (800.0, 2000.0),
    "GET /cloud-save/download": (800.0, 2000.0),
}


# ── Statistics ──────────────────────────────────────────────────────────────


class Stats:
    def __init__(self) -> None:
        self.lock = threading.Lock()
        self.routes: dict[str, dict] = {}
        self.stages: dict[int, dict] = {}
        self.statuses: dict[tuple[str, str], int] = {}
        self.stage_index = 0

    @staticmethod
    def _blank() -> dict:
        return {"n": 0, "ok": 0, "throttled": 0, "errors": 0, "lat": []}

    def record(self, route: str, outcome: str, status: str, latency_ms: float | None) -> None:
        with self.lock:
            for bucket in (self.routes.setdefault(route, self._blank()),
                           self.stages.setdefault(self.stage_index, self._blank())):
                bucket["n"] += 1
                if outcome == "ok":
                    bucket["ok"] += 1
                elif outcome == "throttled":
                    bucket["throttled"] += 1
                else:
                    bucket["errors"] += 1
                # A 429 is answered before any work; it would flatter latency.
                if latency_ms is not None and outcome != "throttled":
                    bucket["lat"].append(latency_ms)
            if outcome != "ok":
                key = (route, status)
                self.statuses[key] = self.statuses.get(key, 0) + 1


def percentile(values: list[float], pct: float) -> float | None:
    if not values:
        return None
    ordered = sorted(values)
    rank = max(1, int(round(pct / 100.0 * len(ordered) + 0.4999)))
    return ordered[min(rank, len(ordered)) - 1]


# ── HTTP ────────────────────────────────────────────────────────────────────


class Target:
    def __init__(self, url: str, timeout: float, source_ips: list[str]) -> None:
        parsed = urllib.parse.urlsplit(url)
        if parsed.scheme not in ("http", "https") or not parsed.hostname:
            raise ValueError(f"--target must be an http(s) URL, got {url!r}")
        self.url = url.rstrip("/")
        self.scheme = parsed.scheme
        self.host = parsed.hostname
        self.port = parsed.port or (443 if parsed.scheme == "https" else 80)
        self.base_path = parsed.path.rstrip("/")
        self.timeout = timeout
        self.source_ips = source_ips
        self.ssl_context = ssl.create_default_context() if parsed.scheme == "https" else None
        self._next_ip = 0
        self._ip_lock = threading.Lock()

    def is_local(self) -> bool:
        if self.host in ("localhost", "127.0.0.1", "::1"):
            return True
        try:
            return all(info[4][0].startswith(("127.", "::1"))
                       for info in socket.getaddrinfo(self.host, self.port))
        except OSError:
            return False

    def connect(self) -> http.client.HTTPConnection:
        source = None
        if self.source_ips:
            with self._ip_lock:
                source = (self.source_ips[self._next_ip % len(self.source_ips)], 0)
                self._next_ip += 1
        if self.scheme == "https":
            return http.client.HTTPSConnection(self.host, self.port, timeout=self.timeout,
                                               context=self.ssl_context, source_address=source)
        return http.client.HTTPConnection(self.host, self.port, timeout=self.timeout,
                                          source_address=source)


class StopVU(Exception):
    pass


class VirtualPlayer(threading.Thread):
    def __init__(self, run: "LoadRun", vu_number: int) -> None:
        super().__init__(name=f"vu-{vu_number}", daemon=True)
        self.run_ = run
        self.number = vu_number
        self.rng = random.Random()
        self.stop_event = threading.Event()
        self.conn: http.client.HTTPConnection | None = None
        self.profile = ""
        self.token = ""
        self.callsign = f"Loadtest {vu_number}"
        self.consents = self.rng.random() < MIX["analytics_consent"]
        self.analytics_queue: list[dict] = []
        self.next_health = 0.0
        self.save_template = run.save_templates[vu_number % len(run.save_templates)]

    # ── plumbing ──

    def stop(self) -> None:
        self.stop_event.set()

    def check_stop(self) -> None:
        if self.stop_event.is_set() or self.run_.hard_stop.is_set():
            raise StopVU()

    def scaled(self, bounds: tuple[float, float]) -> float:
        return self.rng.uniform(*bounds) * self.run_.time_scale

    def wait(self, seconds: float) -> None:
        """Think time. The game's 45 s health check keeps running meanwhile."""
        end = time.monotonic() + max(0.0, seconds)
        while True:
            self.check_stop()
            now = time.monotonic()
            if now >= self.next_health:
                self.health()
                continue
            remaining = end - now
            if remaining <= 0:
                return
            self.stop_event.wait(min(remaining, self.next_health - now))

    def request(self, method: str, route: str, path: str, body=None, expect=(200,),
                auth: bool = True, check=None, while_stopping: bool = False) -> dict | None:
        """Sends one request and records it. Returns the parsed JSON body or None."""
        if while_stopping:
            if self.run_.hard_stop.is_set():
                raise StopVU()
        else:
            self.check_stop()
        headers = {"User-Agent": USER_AGENT, "Accept": "application/json"}
        payload = None
        if body is not None:
            payload = json.dumps(body, separators=(",", ":")).encode()
            headers["Content-Type"] = "application/json"
        if auth and self.profile:
            headers["X-Convoy-Profile"] = self.profile
        if auth and self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        full_path = self.run_.target.base_path + path

        for attempt in (1, 2):
            if self.conn is None:
                self.conn = self.run_.target.connect()
            reused = getattr(self.conn, "sock", None) is not None
            started = time.perf_counter()
            try:
                self.conn.request(method, full_path, body=payload, headers=headers)
                response = self.conn.getresponse()
                data = response.read()
                latency = (time.perf_counter() - started) * 1000.0
                if response.getheader("Connection", "").lower() == "close":
                    self._drop_connection()
            except (http.client.RemoteDisconnected, BrokenPipeError, ConnectionResetError) as exc:
                self._drop_connection()
                # A keep-alive connection the server closed while the player
                # was idle; HttpClient retries these too. Not a server error.
                if reused and attempt == 1:
                    continue
                self.run_.stats.record(route, "error", f"EXC:{type(exc).__name__}", None)
                return None
            except (socket.timeout, TimeoutError):
                self._drop_connection()
                self.run_.stats.record(route, "error", "EXC:timeout", None)
                return None
            except (OSError, http.client.HTTPException) as exc:
                self._drop_connection()
                self.run_.stats.record(route, "error", f"EXC:{type(exc).__name__}", None)
                return None
            break

        status = response.status
        parsed = None
        if data:
            try:
                parsed = json.loads(data)
            except ValueError:
                parsed = None
        if status == 429:
            self.run_.stats.record(route, "throttled", "429", latency)
            return None
        if status not in expect:
            self.run_.stats.record(route, "error", str(status), latency)
            return None
        if check is not None and not check(parsed):
            self.run_.stats.record(route, "error", f"{status}:unexpected_body", latency)
            return None
        self.run_.stats.record(route, "ok", str(status), latency)
        return parsed if parsed is not None else {}

    def _drop_connection(self) -> None:
        if self.conn is not None:
            try:
                self.conn.close()
            except OSError:
                pass
        self.conn = None

    def read_headers(self) -> bool:
        # The current game sends no session on public reads (leaderboards,
        # feed, rooms, health), so they count against the per-IP bucket.
        return self.run_.bearer_on_reads

    # ── game calls ──

    def health(self) -> None:
        self.next_health = time.monotonic() + MIX["health_interval"]
        self.request("GET", "GET /health", "/health", auth=self.read_headers(),
                     check=lambda b: isinstance(b, dict) and b.get("status") == "healthy")

    def register_guest(self) -> bool:
        self.profile = "CVY-LT" + uuid.uuid4().hex[:8].upper()
        self.token = ""
        body = {"profile": {"playerProfileId": self.profile, "playerCallsign": self.callsign,
                            "syncProviderId": "http_api", "requestedAtUnixSeconds": int(time.time())}}
        result = self.request("POST", "POST /player-profile (new guest)", "/player-profile", body,
                              auth=False, check=lambda b: isinstance(b, dict) and b.get("sessionToken"))
        if not result:
            self.profile = ""
            return False
        self.token = result["sessionToken"]
        self.run_.created_identity(self.profile)
        return True

    def sync_profile(self) -> None:
        # The server rotates the session on every profile sync.
        body = {"profile": {"playerProfileId": self.profile, "playerCallsign": self.callsign,
                            "syncProviderId": "http_api", "requestedAtUnixSeconds": int(time.time())}}
        result = self.request("POST", "POST /player-profile (sync)", "/player-profile", body,
                              check=lambda b: isinstance(b, dict) and b.get("sessionToken"))
        if result:
            self.token = result["sessionToken"]

    def query(self) -> str:
        return "?profileId=" + urllib.parse.quote(self.profile)

    def session_check(self) -> bool:
        return self.request("GET", "GET /cloud-save/info", "/cloud-save/info" + self.query()) is not None

    def refunds_and_wallet(self) -> None:
        self.request("GET", "GET /wallet/adjustments", "/wallet/adjustments" + self.query())
        self.request("GET", "GET /wallet", "/wallet" + self.query())

    def download_save(self) -> None:
        self.request("GET", "GET /cloud-save/download", "/cloud-save/download" + self.query())

    def upload_save(self, battles: int) -> None:
        save = dict(self.save_template)
        save["PlayerProfileId"] = self.profile
        save["Gold"] = self.rng.randint(0, 250000)
        save["HighestUnlockedStage"] = min(100, 1 + battles)
        save["SavedAtUnixSeconds"] = int(time.time())
        body = {"profileId": self.profile, "saveData": json.dumps(save, separators=(",", ":")),
                "saveVersion": save["Version"]}
        self.request("POST", "POST /cloud-save/upload", "/cloud-save/upload", body)

    def feed(self) -> None:
        self.request("GET", "GET /challenge-feed", "/challenge-feed?stageCap=12&maxStage=100&limit=8",
                     auth=self.read_headers())

    def challenge(self) -> None:
        code = self.rng.choice(BOARD_CODES)
        submission = {
            "submissionId": uuid.uuid4().hex, "playerProfileId": self.profile,
            "playerCallsign": self.callsign, "code": code, "stage": self.rng.randint(1, 100),
            "score": self.rng.randint(1000, 90000), "won": self.rng.random() < 0.7,
            "elapsedSeconds": round(self.rng.uniform(60, 400), 1),
            "enemyDefeats": self.rng.randint(10, 300), "starsEarned": self.rng.randint(0, 3),
            "hullPercent": self.rng.randint(0, 100), "deckUnitIds": self.rng.sample(UNIT_IDS, 4),
        }
        body = {"batch": {"batchId": uuid.uuid4().hex, "providerId": "http_api",
                          "playerProfileId": self.profile, "playerCallsign": self.callsign,
                          "submittedAtUnixSeconds": int(time.time()), "submissions": [submission]}}
        self.request("POST", "POST /challenge-sync", "/challenge-sync", body)
        self.request("GET", "GET /challenge-boards", f"/challenge-boards?code={code}&limit=10",
                     auth=self.read_headers())

    def daily(self) -> None:
        date = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d")
        self.request("POST", "POST /daily/complete", "/daily/complete",
                     {"profileId": self.profile, "date": date, "score": self.rng.randint(100, 50000)})
        self.request("GET", "GET /daily/leaderboard/{date}", f"/daily/leaderboard/{date}",
                     auth=self.read_headers())

    def achievements(self) -> None:
        ids = self.rng.sample(ACHIEVEMENTS, self.rng.randint(1, 3))
        self.request("POST", "POST /achievements/sync", "/achievements/sync",
                     {"profileId": self.profile, "achievementIds": ids})

    def track(self, event_type: str, data: str) -> None:
        if not self.consents:
            return
        self.analytics_queue.append({"type": event_type, "data": data})
        if len(self.analytics_queue) >= 10:   # AnalyticsService.FlushThreshold
            batch, self.analytics_queue = self.analytics_queue[:10], self.analytics_queue[10:]
            self.request("POST", "POST /analytics/ingest", "/analytics/ingest",
                         {"profileId": self.profile, "clientVersion": 31, "platform": "android",
                          "events": batch})

    def failed_purchase(self) -> None:
        # Rejected as an unsupported store before any call to Google or Apple,
        # and nothing is credited. Production refuses test claims anyway.
        body = {"profileId": self.profile, "productId": "gold_pouch", "platform": "loadtest",
                "receiptToken": "loadtest-" + uuid.uuid4().hex, "transactionId": ""}
        self.request("POST", "POST /purchase/validate (rejected)", "/purchase/validate", body,
                     expect=(400,),
                     check=lambda b: isinstance(b, dict) and b.get("error") == "native_purchase_verification_failed")

    def browse_rooms(self) -> None:
        if self.rng.random() < MIX["profile_sync_on_menu"]:
            self.sync_profile()
        end = time.monotonic() + self.scaled(MIX["menu_seconds"])
        while True:
            self.request("GET", "GET /challenge-rooms", "/challenge-rooms?stageCap=12&maxStage=100&limit=20",
                         auth=self.read_headers())
            if time.monotonic() + MIX["room_poll_interval"] > end:
                return
            self.wait(MIX["room_poll_interval"])

    def host_room(self) -> None:
        code = self.rng.choice(BOARD_CODES)
        body = {"room": {"playerProfileId": self.profile, "playerCallsign": self.callsign,
                         "boardCode": code, "boardTitle": "Load test board", "region": "global",
                         "usesLockedDeck": False, "lockedDeckUnitIds": []}}
        created = self.request("POST", "POST /challenge-room-create", "/challenge-room-create", body,
                               check=lambda b: isinstance(b, dict) and b.get("roomId"))
        if not created:
            return
        room = created["roomId"]
        end = time.monotonic() + self.scaled(MIX["room_seconds"])
        try:
            while True:
                q = "?roomId=" + urllib.parse.quote(room)
                self.request("GET", "GET /challenge-room-session", "/challenge-room-session" + q)
                self.request("GET", "GET /challenge-room-scoreboard", "/challenge-room-scoreboard" + q)
                if time.monotonic() + MIX["room_poll_interval"] > end:
                    break
                self.wait(MIX["room_poll_interval"])
        finally:
            # A host leaves its room even when the ramp-down closes the app.
            try:
                self.request("POST", "POST /challenge-room-leave", "/challenge-room-leave", {"roomId": room},
                             while_stopping=True)
            except StopVU:
                pass

    # ── lifecycle ──

    def launch(self) -> bool:
        """App start. Returns False only if the player never got a session."""
        self.health()
        returning = self.run_.take_returning(self.rng)
        if returning:
            self.profile, self.token = returning
            if not self.session_check():
                returning = None
        if not returning:
            # Without a session the game keeps playing offline and tries again
            # after its next health check, so a refused registration waits.
            while not self.register_guest():
                self.wait(MIX["health_interval"])
            self.sync_profile()
            self.session_check()
        elif self.rng.random() < 0.3:
            self.sync_profile()
        self.track("session_start", "stage=1,gold=0")
        self.refunds_and_wallet()
        self.download_save()
        self.feed()
        return True

    def play(self) -> None:
        battles = 0
        session_end = time.monotonic() + MIX["session_minutes"] * 60.0 * self.run_.time_scale
        while time.monotonic() < session_end:
            stage = min(100, battles + 1)
            self.track("stage_start", f"stage={stage},diff=normal,deck=player_brawler+player_shooter")
            self.wait(self.scaled(MIX["battle_seconds"]))
            battles += 1
            won = self.rng.random() < MIX["win_probability"]
            self.track("stage_end", f"stage={stage},won={won},stars={3 if won else 0},time=95.0,hull=0.62")
            if won:
                self.upload_save(battles)
            r = self.rng.random
            if r() < MIX["challenge_per_battle"]:
                self.challenge()
            if r() < MIX["daily_per_battle"]:
                self.daily()
            if r() < MIX["achievements_per_battle"]:
                self.achievements()
            if r() < MIX["shop_per_battle"]:
                self.refunds_and_wallet()
            if r() < self.run_.purchase_fail_rate:
                self.failed_purchase()
            if r() < MIX["restore_per_battle"]:
                self.download_save()
            if r() < MIX["multiplayer_menu_per_battle"]:
                self.browse_rooms()
            if r() < MIX["host_room_per_battle"]:
                self.host_room()

    def run(self) -> None:  # noqa: D401 - thread entry point
        try:
            if self.launch():
                self.play()
        except StopVU:
            pass
        except Exception as exc:  # a bug in this script must not hide as a server error
            self.run_.script_errors.append(f"{self.name}: {type(exc).__name__}: {exc}")
        finally:
            self._drop_connection()
            if self.profile and self.token:
                self.run_.return_identity(self.profile, self.token)
            self.run_.vu_finished(self)


# ── Run control ─────────────────────────────────────────────────────────────


def parse_duration(text: str) -> float:
    match = re.fullmatch(r"(\d+(?:\.\d+)?)(ms|s|m|h)?", text.strip())
    if not match:
        raise ValueError(f"bad duration {text!r} (use 30s, 2m, 1h)")
    value, unit = float(match.group(1)), match.group(2) or "s"
    return value * {"ms": 0.001, "s": 1, "m": 60, "h": 3600}[unit]


def parse_stages(text: str) -> list[tuple[float, int]]:
    stages = []
    for part in text.split(","):
        if not part.strip():
            continue
        duration, _, target = part.partition(":")
        if not target.strip().isdigit():
            raise ValueError(f"bad stage {part!r} (use duration:VUs, e.g. 30s:10)")
        stages.append((parse_duration(duration), int(target)))
    if not stages:
        raise ValueError("no stages given")
    return stages


def build_save_templates(rng: random.Random) -> list[dict]:
    """Synthetic saves of 20, 50 and 100 KB (a late-game save is about 100 KB)."""
    templates = []
    for size in (20_000, 50_000, 100_000):
        save = {"Version": 46, "PlayerProfileId": "", "PlayerCallsign": "Loadtest", "Gold": 0,
                "Food": 40, "HighestUnlockedStage": 1, "SavedAtUnixSeconds": 0,
                "UnitLevels": {u: rng.randint(1, 30) for u in UNIT_IDS},
                "StageStars": {}, "Journal": []}
        stage = 1
        while len(json.dumps(save)) < size:
            save["StageStars"][str(stage)] = rng.randint(0, 3)
            save["Journal"].append({"stage": stage, "won": True, "elapsed": round(rng.uniform(60, 300), 1),
                                    "deck": rng.sample(UNIT_IDS, 4), "seed": rng.randint(1, 10**9)})
            stage += 1
        templates.append(save)
    return templates


class LoadRun:
    def __init__(self, args: argparse.Namespace) -> None:
        self.target = Target(args.target, args.timeout, args.source_ip or [])
        self.stages = parse_stages(args.stages)
        self.time_scale = args.time_scale
        self.purchase_fail_rate = args.purchase_fail_rate
        self.bearer_on_reads = args.bearer_on_reads
        self.returning_ratio = args.returning_ratio
        self.stats = Stats()
        self.hard_stop = threading.Event()
        self.script_errors: list[str] = []
        self.save_templates = build_save_templates(random.Random(7))
        self.lock = threading.Lock()
        self.active: list[VirtualPlayer] = []
        self.started_vus = 0
        self.max_vus = 0
        self.identities: dict[str, str] = {}        # every guest created: profile -> latest token
        self.idle_identities: list[tuple[str, str]] = []
        self.stage_rows: list[dict] = []
        self.vu_seconds = 0.0

    # identities
    def created_identity(self, profile: str) -> None:
        with self.lock:
            self.identities[profile] = ""

    def return_identity(self, profile: str, token: str) -> None:
        with self.lock:
            self.identities[profile] = token
            self.idle_identities.append((profile, token))

    def take_returning(self, rng: random.Random):
        with self.lock:
            if self.idle_identities and rng.random() < self.returning_ratio:
                return self.idle_identities.pop(rng.randrange(len(self.idle_identities)))
        return None

    def vu_finished(self, vu: VirtualPlayer) -> None:
        with self.lock:
            if vu in self.active:
                self.active.remove(vu)

    def spawn(self) -> None:
        self.started_vus += 1
        vu = VirtualPlayer(self, self.started_vus)
        with self.lock:
            self.active.append(vu)
        vu.start()

    def running(self) -> list[VirtualPlayer]:
        with self.lock:
            return [vu for vu in self.active if not vu.stop_event.is_set()]

    def drive(self) -> None:
        current = 0.0
        tick = 0.25
        for index, (duration, target_vus) in enumerate(self.stages):
            with self.stats.lock:
                self.stats.stage_index = index
            start_vus = current
            stage_start = time.monotonic()
            stage_max = 0
            while True:
                elapsed = time.monotonic() - stage_start
                if elapsed >= duration:
                    break
                desired = round(start_vus + (target_vus - start_vus) * (elapsed / duration)) if duration else target_vus
                live = self.running()
                # Players whose session ended are replaced by new or returning ones.
                for _ in range(desired - len(live)):
                    self.spawn()
                if len(live) > desired:
                    for vu in sorted(live, key=lambda v: v.number, reverse=True)[: len(live) - desired]:
                        vu.stop()
                stage_max = max(stage_max, desired)
                self.max_vus = max(self.max_vus, stage_max)
                self.vu_seconds += len(live) * tick
                time.sleep(tick)
            current = float(target_vus)
            self.stage_rows.append({"index": index, "seconds": duration, "target": target_vus,
                                    "max_vus": stage_max})

    def finish(self, grace: float) -> None:
        for vu in self.running():
            vu.stop()
        deadline = time.monotonic() + grace
        while time.monotonic() < deadline:
            with self.lock:
                if not self.active:
                    return
            time.sleep(0.1)
        self.hard_stop.set()
        # Requests already on the wire end within the request timeout.
        with self.lock:
            stragglers = list(self.active)
        for vu in stragglers:
            vu.join(self.target.timeout + 1)

    def delete_accounts(self, workers: int = 8) -> tuple[int, int, int]:
        """Deletes every guest through POST /account/delete, the game's own path."""
        with self.lock:
            pending = [(p, t) for p, t in self.identities.items() if t]
            untracked = sum(1 for t in self.identities.values() if not t)
        deleted = [0]
        failed = [0]
        lock = threading.Lock()

        def worker() -> None:
            conn = None
            while True:
                with lock:
                    if not pending:
                        break
                    profile, token = pending.pop()
                for _ in range(3):
                    try:
                        if conn is None:
                            conn = self.target.connect()
                        conn.request("POST", self.target.base_path + "/account/delete", body=b"{}",
                                     headers={"Content-Type": "application/json", "User-Agent": USER_AGENT,
                                              "X-Convoy-Profile": profile, "Authorization": f"Bearer {token}"})
                        response = conn.getresponse()
                        response.read()
                        if response.status == 429:
                            time.sleep(float(response.getheader("Retry-After", "5")))
                            continue
                        with lock:
                            if response.status == 200:
                                deleted[0] += 1
                            else:
                                failed[0] += 1
                        break
                    except (OSError, http.client.HTTPException):
                        conn = None
                else:
                    with lock:
                        failed[0] += 1

        threads = [threading.Thread(target=worker, daemon=True) for _ in range(workers)]
        for t in threads:
            t.start()
        for t in threads:
            t.join()
        return deleted[0], failed[0], untracked


# ── Report ──────────────────────────────────────────────────────────────────


def fmt_ms(value: float | None) -> str:
    return "-" if value is None else (f"{value:.0f}" if value >= 10 else f"{value:.1f}")


def build_report(run: LoadRun, args, wall_seconds: float, cpu_seconds: float) -> tuple[str, dict, bool]:
    stats = run.stats
    lines = []
    failures: list[str] = []
    now = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    stage_text = ", ".join(f"{int(d) if d == int(d) else d}s→{t}" for d, t in run.stages)
    lines.append(f"Crownroad load test · {run.target.url} · finished {now}")
    lines.append(f"Stages: {stage_text} VUs · time scale {run.time_scale:g} · peak {run.max_vus} VUs · "
                 f"{run.started_vus} player sessions · wall {wall_seconds:.0f}s")
    if run.target.source_ips:
        lines.append(f"Source IPs: {', '.join(run.target.source_ips)}")
    lines.append(f"Public reads carry the session: {'yes (--bearer-on-reads)' if run.bearer_on_reads else 'no (as the game does today)'}")
    lines.append("")

    lines.append(f"{'Stage':<6}{'Length':>8}{'VUs':>6}{'Reqs':>8}{'Req/s':>8}{'429':>6}{'Err':>6}"
                 f"{'Err%':>7}{'p50':>7}{'p95':>7}{'p99':>7}")
    offset = 0.0
    stage_json = []
    for row in run.stage_rows:
        bucket = stats.stages.get(row["index"], Stats._blank())
        n = bucket["n"]
        rate = n / row["seconds"] if row["seconds"] else 0.0
        err_pct = 100.0 * bucket["errors"] / n if n else 0.0
        p50, p95, p99 = (percentile(bucket["lat"], p) for p in (50, 95, 99))
        lines.append(f"{row['index'] + 1:<6}{row['seconds']:>7.0f}s{row['max_vus']:>6}{n:>8}{rate:>8.1f}"
                     f"{bucket['throttled']:>6}{bucket['errors']:>6}{err_pct:>6.1f}%"
                     f"{fmt_ms(p50):>7}{fmt_ms(p95):>7}{fmt_ms(p99):>7}")
        stage_json.append({"stage": row["index"] + 1, "start_s": offset, "seconds": row["seconds"],
                           "vus": row["max_vus"], "requests": n, "rps": round(rate, 2),
                           "throttled": bucket["throttled"], "errors": bucket["errors"],
                           "p50_ms": p50, "p95_ms": p95, "p99_ms": p99})
        offset += row["seconds"]
    lines.append("(latency in ms; 429 answers are counted but left out of latency)")
    lines.append("")

    header = (f"{'Route':<38}{'n':>6}{'ok':>6}{'429':>6}{'err':>5}{'err%':>7}"
              f"{'p50':>7}{'p95':>7}{'p99':>7}  result")
    lines.append(header)
    lines.append("-" * len(header))
    total = Stats._blank()
    route_json = {}
    for route in sorted(stats.routes):
        bucket = stats.routes[route]
        for key in ("n", "ok", "throttled", "errors"):
            total[key] += bucket[key]
        total["lat"].extend(bucket["lat"])
        n = bucket["n"]
        err_pct = 100.0 * bucket["errors"] / n if n else 0.0
        p50, p95, p99 = (percentile(bucket["lat"], p) for p in (50, 95, 99))
        budget95, budget99 = ROUTE_BUDGETS.get(route, (args.p95_ms, args.p99_ms))
        problems = []
        if n >= args.min_samples:
            if p95 is not None and p95 > budget95:
                problems.append(f"p95>{budget95:.0f}")
            if p99 is not None and p99 > budget99:
                problems.append(f"p99>{budget99:.0f}")
            if err_pct > args.max_route_error_pct:
                problems.append(f"err>{args.max_route_error_pct:g}%")
            verdict = "FAIL " + ",".join(problems) if problems else "pass"
        else:
            # Too few samples for a percentile to mean much; errors still count.
            if bucket["errors"]:
                problems.append("errors")
            verdict = ("FAIL errors" if problems else "pass") + f" (n<{args.min_samples})"
        if problems:
            failures.append(f"{route}: {', '.join(problems)}")
        lines.append(f"{route:<38}{n:>6}{bucket['ok']:>6}{bucket['throttled']:>6}{bucket['errors']:>5}"
                     f"{err_pct:>6.1f}%{fmt_ms(p50):>7}{fmt_ms(p95):>7}{fmt_ms(p99):>7}  {verdict}")
        route_json[route] = {"requests": n, "ok": bucket["ok"], "throttled": bucket["throttled"],
                             "errors": bucket["errors"], "error_pct": round(err_pct, 3),
                             "p50_ms": p50, "p95_ms": p95, "p99_ms": p99,
                             "budget_p95_ms": budget95, "budget_p99_ms": budget99, "verdict": verdict}
    lines.append("-" * len(header))
    n = total["n"]
    err_pct = 100.0 * total["errors"] / n if n else 0.0
    thr_pct = 100.0 * total["throttled"] / n if n else 0.0
    p50, p95, p99 = (percentile(total["lat"], p) for p in (50, 95, 99))
    lines.append(f"{'ALL':<38}{n:>6}{total['ok']:>6}{total['throttled']:>6}{total['errors']:>5}"
                 f"{err_pct:>6.1f}%{fmt_ms(p50):>7}{fmt_ms(p95):>7}{fmt_ms(p99):>7}")
    lines.append("")

    if stats.statuses:
        lines.append("Non-OK answers by route and status:")
        for (route, status), count in sorted(stats.statuses.items(), key=lambda kv: -kv[1]):
            lines.append(f"  {count:>6} × {status:<26} {route}")
        lines.append("")

    vu_minutes = run.vu_seconds / 60.0
    if n == 0:
        failures.append("no requests were made")
    if err_pct > args.max_error_pct:
        failures.append(f"overall error rate {err_pct:.2f}% > {args.max_error_pct:g}%")
    if thr_pct > args.max_throttled_pct:
        failures.append(f"{thr_pct:.2f}% of requests were rate-limited (429) > {args.max_throttled_pct:g}%; "
                        "from one machine the per-IP limits apply to every VU at once (see README)")
    if run.script_errors:
        failures.append(f"{len(run.script_errors)} load-generator error(s): {run.script_errors[0]}")

    cpu_pct = 100.0 * cpu_seconds / wall_seconds if wall_seconds else 0.0
    lines.append(f"Thresholds: per route p95 ≤ {args.p95_ms:g} ms and p99 ≤ {args.p99_ms:g} ms "
                 f"(cloud saves {ROUTE_BUDGETS['POST /cloud-save/upload'][0]:g}/{ROUTE_BUDGETS['POST /cloud-save/upload'][1]:g} ms), "
                 f"route errors ≤ {args.max_route_error_pct:g}% (percentiles need n ≥ {args.min_samples}); "
                 f"overall errors ≤ {args.max_error_pct:g}%, 429s ≤ {args.max_throttled_pct:g}%")
    lines.append(f"Load generator CPU: {cpu_pct:.0f}% of one core"
                 + (" — above 70%, client-side delays may inflate latency; use more machines" if cpu_pct > 70 else ""))
    if vu_minutes:
        lines.append(f"Request rate per player: {n / max(vu_minutes, 1e-9):.2f} requests per VU-minute at time scale {run.time_scale:g}")
    passed = not failures
    lines.append("Result: " + ("PASS" if passed else "FAIL"))
    for failure in failures:
        lines.append(f"  - {failure}")

    summary = {
        "target": run.target.url, "finished": now, "stages": stage_json, "routes": route_json,
        "total": {"requests": n, "ok": total["ok"], "throttled": total["throttled"], "errors": total["errors"],
                  "error_pct": round(err_pct, 3), "throttled_pct": round(thr_pct, 3),
                  "p50_ms": p50, "p95_ms": p95, "p99_ms": p99},
        "peak_vus": run.max_vus, "player_sessions": run.started_vus, "time_scale": run.time_scale,
        "bearer_on_reads": run.bearer_on_reads, "source_ips": run.target.source_ips,
        "statuses": [{"route": r, "status": s, "count": c} for (r, s), c in stats.statuses.items()],
        "client_cpu_pct": round(cpu_pct, 1), "passed": passed, "failures": failures,
        "script_errors": run.script_errors[:20],
    }
    return "\n".join(lines), summary, passed


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Ramped load test of the Crownroad API with a realistic per-player mix.",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter)
    parser.add_argument("--target", default=DEFAULT_TARGET,
                        help="API origin. Anything but the local default must be given explicitly.")
    parser.add_argument("--stages", default=DEFAULT_STAGES,
                        help="Comma-separated duration:VUs ramps, e.g. 1m:50,5m:200,1m:0 (linear within a stage).")
    parser.add_argument("--time-scale", type=float, default=1.0,
                        help="Multiplies player think time (battles, menus). 1 = real players; 0.1 = ten times busier.")
    parser.add_argument("--purchase-fail-rate", type=float, default=0.01,
                        help="Chance per battle of a rejected purchase validation (platform 'loadtest'). 0 disables.")
    parser.add_argument("--session-minutes", type=float, default=MIX["session_minutes"],
                        help="Length of one app session in game minutes (scaled by --time-scale); the slot then "
                             "starts a new or returning player.")
    parser.add_argument("--returning-ratio", type=float, default=0.7,
                        help="Chance that a new player session reuses an earlier guest instead of registering.")
    parser.add_argument("--bearer-on-reads", action="store_true",
                        help="Send the session on public reads (health, feed, boards, rooms). Not what the game does "
                             "today; models a client change so reads use the per-session rate-limit bucket.")
    parser.add_argument("--source-ip", action="append", metavar="ADDR",
                        help="Local address to send from; repeat to spread VUs over several addresses.")
    parser.add_argument("--timeout", type=float, default=15.0, help="Per-request timeout in seconds (the game uses 6-15).")
    parser.add_argument("--grace", type=float, default=15.0, help="Seconds to let in-flight requests finish at the end.")
    parser.add_argument("--p95-ms", type=float, default=500.0)
    parser.add_argument("--p99-ms", type=float, default=1500.0)
    parser.add_argument("--max-error-pct", type=float, default=1.0)
    parser.add_argument("--max-route-error-pct", type=float, default=2.0)
    parser.add_argument("--max-throttled-pct", type=float, default=1.0)
    parser.add_argument("--min-samples", type=int, default=20,
                        help="Routes with fewer requests are judged on errors only.")
    parser.add_argument("--keep-accounts", action="store_true",
                        help="Leave the guest accounts in the database instead of deleting them at the end.")
    parser.add_argument("--json-out", metavar="FILE", help="Also write the summary as JSON (evidence).")
    parser.add_argument("--yes", action="store_true", help="Skip the 5-second pause before loading a remote target.")
    args = parser.parse_args()

    MIX["session_minutes"] = args.session_minutes
    try:
        run = LoadRun(args)
    except ValueError as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 2
    if not 0 < args.time_scale <= 10:
        print("error: --time-scale must be between 0 and 10", file=sys.stderr)
        return 2

    print(f"Target {run.target.url} · stages {args.stages} · time scale {args.time_scale:g}", flush=True)
    if not run.target.is_local():
        print("This is not a local server. Load-test only an environment you operate (staging, or production "
              "in an agreed window). Ctrl-C now to abort.", flush=True)
        if not args.yes:
            time.sleep(5)

    started = time.monotonic()
    cpu_start = time.process_time()
    try:
        run.drive()
    except KeyboardInterrupt:
        print("Interrupted; finishing up.", file=sys.stderr)
    run.finish(args.grace)
    wall = time.monotonic() - started
    cpu = time.process_time() - cpu_start

    report, summary, passed = build_report(run, args, wall, cpu)
    if not args.keep_accounts and run.identities:
        deleted, failed, untracked = run.delete_accounts()
        note = f"Cleanup: deleted {deleted} load-test guest account(s) through POST /account/delete"
        if failed:
            note += f", {failed} failed"
        if untracked:
            note += f"; {untracked} guest(s) whose session was lost mid-run remain (profile IDs start CVY-LT)"
        report += "\n" + note
        summary["cleanup"] = {"deleted": deleted, "failed": failed, "untracked": untracked}
    print(report, flush=True)
    if args.json_out:
        with open(args.json_out, "w", encoding="utf-8") as handle:
            json.dump(summary, handle, indent=2)
        print(f"Summary written to {args.json_out}")
    return 0 if passed else 1


if __name__ == "__main__":
    sys.exit(main())
