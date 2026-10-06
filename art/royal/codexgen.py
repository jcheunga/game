#!/usr/bin/env python3
"""Generate royal-UI art through the Codex CLI's built-in image tool.

Jobs live in art/royal/jobs/*.json as a list of objects:
  {"id": "plate-warband", "out": "art/royal/gen/plates/warband.png",
   "inputs": ["output/.../07-warband-armory.png"], "prompt": "..."}

Each job runs `codex exec` once; finished outputs are skipped, so the runner can be
re-run after a usage-limit stop. Usage-limit errors stop the whole run (exit 3).

  python3 art/royal/codexgen.py [--jobs plates,backdrops] [--only id1,id2] [--parallel 2]
"""
import argparse
import concurrent.futures
import json
import os
import subprocess
import sys
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
JOBS = ROOT / "art/royal/jobs"
LOGS = ROOT / "art/royal/gen/logs"
MODEL = "gpt-5.6-sol"
stop = threading.Event()

WRAPPER = """You are producing one image asset for the game project in this workspace.
Use your built-in image_gen tool (imagegen skill, built-in mode; never the CLI fallback) exactly once{edit_hint}.
Then copy the generated PNG (from $CODEX_HOME/generated_images/...) to:
  {out}
creating parent folders if needed, and reply with exactly one line: DONE <width>x<height>.
Do not modify any other file and do not run anything else.

{prompt}
"""


def load_jobs(groups):
    jobs = []
    for path in sorted(JOBS.glob("*.json")):
        if groups and path.stem not in groups:
            continue
        for job in json.loads(path.read_text()):
            job.setdefault("group", path.stem)
            jobs.append(job)
    return jobs


def run(job):
    if stop.is_set():
        return job["id"], "skipped"
    out = ROOT / job["out"]
    if out.exists():
        return job["id"], "exists"
    out.parent.mkdir(parents=True, exist_ok=True)
    LOGS.mkdir(parents=True, exist_ok=True)
    inputs = [str(ROOT / i) for i in job.get("inputs", [])]
    edit_hint = ""
    if inputs:
        edit_hint = (" in edit mode: the FIRST attached image is the edit target" if job.get("edit", True)
                     else "; the attached images are style/composition references only")
    prompt = WRAPPER.format(out=out, prompt=job["prompt"].strip(), edit_hint=edit_hint)
    command = ["codex", "exec", "-m", MODEL, "-c", 'model_reasoning_effort="low"', "--skip-git-repo-check",
               "-C", str(ROOT), "--sandbox", "workspace-write"]
    for image in inputs:
        command += ["-i", image]
    command.append("-")
    started = time.time()
    log = LOGS / f"{job['id']}.log"
    with log.open("w") as handle:
        result = subprocess.run(command, input=prompt, text=True, stdout=handle, stderr=subprocess.STDOUT, cwd=ROOT)
    text = log.read_text()
    if "usage limit" in text.lower():
        stop.set()
        return job["id"], "usage-limit"
    status = "ok" if out.exists() else f"failed (exit {result.returncode})"
    return job["id"], f"{status} in {time.time() - started:.0f}s"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--jobs", default="")
    parser.add_argument("--only", default="")
    parser.add_argument("--parallel", type=int, default=2)
    args = parser.parse_args()
    jobs = load_jobs([g for g in args.jobs.split(",") if g])
    if args.only:
        wanted = set(args.only.split(","))
        jobs = [j for j in jobs if j["id"] in wanted]
    pending = [j for j in jobs if not (ROOT / j["out"]).exists()]
    print(f"{len(pending)} pending of {len(jobs)}", flush=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=args.parallel) as pool:
        for job_id, status in pool.map(run, pending):
            print(f"{job_id}: {status}", flush=True)
    sys.exit(3 if stop.is_set() else 0)


if __name__ == "__main__":
    main()
