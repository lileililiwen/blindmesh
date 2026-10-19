#!/usr/bin/env python3
"""Verify planning artifacts only; this does not run product tests or certify readiness."""
from pathlib import Path
import hashlib
import json
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
errors = []


def require(condition, message):
    if not condition:
        errors.append(message)


def read(relative):
    path = ROOT / relative
    require(path.is_file(), f"missing {relative}")
    return path.read_text() if path.is_file() else ""


def luminance(color):
    channels = [int(color[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    linear = [v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in channels]
    return sum(v * weight for v, weight in zip(linear, (0.2126, 0.7152, 0.0722)))


def main():
    required = [
        "requirements.md", "README.md", "ROADMAP.md", "HANDOFF.md", "AGENTS.md", "LICENSE",
        ".project.json", ".gitignore", ".agentignore", "forge.yaml", ".ai-gate/gate.yaml",
        ".ai-rules/workflow.md", ".ai-rules/completion.md", "docs/architecture.md",
        "docs/branding.md", "docs/requirements-coverage.md", "docs/assets/capture-plan.md",
        "openspec/config.yaml", "openspec/package-map.md", "openspec/change-map.json",
    ]
    for path in required:
        read(path)
    require(hashlib.sha256((ROOT / "requirements.md").read_bytes()).hexdigest() ==
            "718d1f7bb8884c803fb7aa6b8b8fc88c5afba6b9688884dad187f6eb3d7ec91b", "original PRD changed")
    read("docs/bootstrap-evidence.md")
    declaration = json.loads(read(".project.json"))
    require(declaration["profile"] == "dotnet-product" and declaration["stack"] == "dotnet",
            "C#/.NET declaration differs from user-selected stack")
    require(declaration["id"] == "blindmesh", "project identity differs")
    require(declaration["lifecycle"] == "planning", "update verifier when runtime lifecycle changes")
    require(declaration["deployment"]["deployable"] is False, "planning project cannot claim deployable")
    require(declaration["verification"]["evidence_status"] == "planned", "do not promote planning evidence")
    plan = json.loads(read("openspec/change-map.json"))
    changes = plan["changes"]
    ids = [change["id"] for change in changes]
    require(len(ids) == len(set(ids)), "duplicate package")
    seen = set()
    requirements = scenarios = tasks = 0
    for change in changes:
        name, prefix = change["id"], change["requirement_prefix"]
        require(re.fullmatch(r"[a-z][a-z0-9]*(?:-[a-z0-9]+)*", name), f"invalid change name {name}")
        require(set(change["depends_on"]) <= seen, f"dependency order violated: {name}")
        seen.add(name)
        base = f"openspec/changes/{name}/"
        proposal = read(base + "proposal.md")
        design = read(base + "design.md")
        task_text = read(base + "tasks.md")
        trace = read(base + "traceability.md")
        verification = read(base + "verification.md")
        spec = read(base + f"specs/{name}/spec.md")
        for heading in ["Package Boundary and Split Assessment", "Sibling and Shared Architecture Reconnaissance",
                        "User Experience and Interface Impact", "BFS Impact Map"]:
            require(f"## {heading}" in proposal, f"{name}: missing proposal {heading}")
        headings = [
            "## 1. BFS — Baseline and impact coverage",
            "## 2. DFS — Requirement-by-requirement implementation",
            "## 3. BFS — Cross-surface regression and completeness",
            "## 4. Verification",
        ]
        offsets = [task_text.find(heading) for heading in headings]
        require(all(i >= 0 for i in offsets) and offsets == sorted(offsets), f"{name}: task phases")
        require("- [x]" not in task_text.lower(), f"{name}: proposed tasks marked complete")
        tasks += task_text.count("- [ ]")
        for marker in ["Context and implementation boundary", "Language, runtime and shared ownership",
                       "User experience and interface", "Behavioral model, contracts and failures",
                       "Verification oracle", "Decision ledger"]:
            require(marker in design, f"{name}: design {marker} missing")
        require("## ADDED Requirements" in spec, f"{name}: missing delta section")
        reqs = re.findall(r"^### Requirement: (" + prefix + r"-(?:\d+|UX)) ", spec, re.M)
        cases = re.findall(r"^#### Scenario: (.+)$", spec, re.M)
        require(len(reqs) == len(set(reqs)) and len(cases) == len(set(cases)), f"{name}: duplicate IDs")
        require(bool(reqs), f"{name}: no requirements")
        for req in reqs:
            require(req in trace and req in task_text and prefix in verification, f"{name}: untraced {req}")
            suffixes = ["complete journey", "recover from failure", "responsive keyboard operation"] if req.endswith("-UX") else ["success", "rejection", "boundary"]
            for suffix in suffixes:
                require(req + " " + suffix in cases and suffix in trace, f"{name}: unmapped {req} {suffix}")
        require(spec.count("**WHEN**") == len(cases) and spec.count("**THEN**") == len(cases),
                f"{name}: malformed WHEN/THEN scenarios")
        requirements += len(reqs)
        scenarios += len(cases)
        require(not re.search(r"\b(?:TBD|TODO|FIXME)\b", proposal + design + task_text + spec), f"{name}: placeholder")
        require("test:e2e" in verification and "test:a11y" in verification, f"{name}: browser oracle missing")
    handoff = read("HANDOFF.md")
    pointers = re.findall(r"^current_spec: (.+)$", handoff, re.M)
    require(pointers == [plan["selected"]] and pointers[0] in ids, "handoff selection mismatch")
    require(handoff.splitlines()[0].startswith("current_spec:"), "pointer must be first line")
    for path in ["AGENTS.md", "HANDOFF.md", ".ai-rules/workflow.md", ".ai-rules/completion.md"]:
        content = read(path)
        require("exactly two commits" in content, f"{path}: missing commit lifecycle")
        require("Do not ask for conversational authorization or confirmation before either commit" in content,
                f"{path}: missing commit authorization rule")
    token_path = declaration["ui"]["shared_layer"]["consume_paths"][0]
    tokens = (ROOT / token_path).read_bytes()
    digest = hashlib.sha256(tokens).hexdigest()
    require(digest == declaration["ui"]["shared_layer"]["pin_sha256"], "shared token bytes drifted")
    contrast = json.loads(read("docs/evidence/token-contrast.json"))
    require(contrast["source_sha256"] == digest, "contrast source digest differs")
    require({row["theme"] for row in contrast["measurements"]} == {"light", "dark"}, "both themes required")
    for row in contrast["measurements"]:
        low, high = sorted([luminance(row["foreground"]), luminance(row["background"])])
        actual = (high + 0.05) / (low + 0.05)
        require(abs(actual - row["ratio"]) < 0.001 and actual >= row["minimum"],
                f"contrast mismatch/failure {row['theme']} {row['pair']}")
    for path in ROOT.rglob("*.md"):
        if ".git" in path.parts or ".forge" in path.parts or path.name == "requirements.md":
            continue
        for target in re.findall(r"(?<!!)\[[^\]]*\]\(([^)]+)\)", path.read_text()):
            target = target.split("#", 1)[0]
            if not target or "://" in target or target.startswith("mailto:"):
                continue
            require((path.parent / target).exists(), f"broken link {path.relative_to(ROOT)}: {target}")
    if errors:
        print("\n".join("FAIL " + error for error in errors))
        return 1
    print(f"PASS planning artifacts: {len(changes)} changes, {requirements} requirements, "
          f"{scenarios} scenarios, {tasks} unchecked tasks; token pin and 12 contrast pairs verified")
    print("Product/runtime/browser/deployment verification: NOT PERFORMED")
    return 0


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (KeyError, ValueError, OSError) as exc:
        print(f"FAIL invalid bootstrap artifact: {exc}")
        sys.exit(1)
