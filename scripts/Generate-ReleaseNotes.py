"""Generate concise player-facing release notes using Git history and published releases.

No third-party packages or model API are required. Feature descriptions are deliberately
conservative: a source change means 'Updated', not an inferred bug fix or promise.
"""
import argparse
import json
import os
from pathlib import Path
import re
import subprocess
import urllib.parse
import urllib.request


FEATURES = [
    ("cash Texas Hold'em poker", ("src/VICE.Lifestyle/Poker/",)),
    ("darts and pool", ("src/VICE.Lifestyle/Amenities/",)),
    ("casino games", ("src/VICE.Lifestyle/Casino/", "src/Casino/")),
    ("TV and wall-screen playback", ("src/VICE.Lifestyle/Television/",)),
    ("trade negotiations and deliveries", ("src/VICE.Commerce/Trading/", "src/Algorithms/")),
    ("gas-mart shopping lists", ("src/VICE.Commerce/Purchasing/",)),
    ("warehouse access", ("src/VICE.Commerce/Warehouse/",)),
    ("weapons and reloads", ("src/VICE.Arsenal/",)),
    ("the minimap and HUD", ("src/VICE.Interface/",)),
    ("police encounters", ("src/VICE.Enforcement/", "src/BetterPolice/")),
    ("cartel activities", ("src/VICE.Syndicate/",)),
    ("the LSD workshop and trip effects", ("src/VICE.Substances/", "src/LSDGameplay/", "src/LSDPreview/")),
    ("YouTube playback", ("src/VICE.YouTube/",)),
    ("Lifestyle features", ("src/VICE.Lifestyle/",)),
    ("Commerce features", ("src/VICE.Commerce/",)),
    ("shared settings and module support", ("src/VICE.Core/",)),
]
TAG_PATTERN = re.compile(r"[A-Za-z0-9][A-Za-z0-9._-]{0,79}\Z")


def git(*args):
    return subprocess.check_output(["git", *args], text=True, encoding="utf-8").strip()


def tag_commit(tag):
    if not TAG_PATTERN.fullmatch(tag):
        raise ValueError("Use a filename-safe release tag.")
    return git("rev-parse", "--verify", f"refs/tags/{tag}^{{commit}}")


def is_ancestor(base, current):
    result = subprocess.run(["git", "merge-base", "--is-ancestor", base, current], check=False)
    if result.returncode not in (0, 1):
        raise RuntimeError("Could not compare release history.")
    return result.returncode == 0


def published_releases(repo):
    if not re.fullmatch(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+", repo):
        raise ValueError("Provide the GitHub owner/repository.")
    headers = {"Accept": "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28"}
    token = os.environ.get("GH_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    releases = []
    page = 1
    while True:
        request = urllib.request.Request(
            f"https://api.github.com/repos/{repo}/releases?per_page=100&page={page}", headers=headers)
        with urllib.request.urlopen(request, timeout=30) as response:
            batch = json.load(response)
        releases.extend(batch)
        if len(batch) < 100:
            return releases
        page += 1


def previous_release(releases, tag, current):
    # A stable release compares with a stable predecessor so features introduced
    # during prereleases still appear in the eventual stable announcement.
    candidates = sorted(releases, key=lambda item: item.get("published_at") or "", reverse=True)
    for release in candidates:
        previous = release.get("tag_name", "")
        if release.get("draft") or not release.get("published_at") or previous == tag:
            continue
        if "-" not in tag and release.get("prerelease"):
            continue
        if not TAG_PATTERN.fullmatch(previous):
            continue
        try:
            commit = tag_commit(previous)
        except subprocess.CalledProcessError:
            raise RuntimeError(f"Published release tag {previous} is missing; fetch all tags.") from None
        if is_ancestor(commit, current):
            return previous, commit
    return None, None


def tree(commit):
    return set(git("ls-tree", "-r", "--name-only", commit).splitlines()) if commit else set()


def feature_paths(files, prefixes):
    return {path for path in files if path.endswith((".cs", ".json", ".shader", ".bundle"))
            and path.startswith(prefixes)}


def summarize(changed, before, after, companion_changed=False):
    bullets = []
    remaining = set(changed)
    for label, prefixes in FEATURES:
        touched = feature_paths(remaining, prefixes)
        if not touched:
            continue
        old = feature_paths(before, prefixes)
        new = feature_paths(after, prefixes)
        verb = "Added" if not old and new else "Removed" if old and not new else "Updated"
        bullets.append(f"{verb} {label}.")
        # Root module glue shouldn't add a second vague bullet when a specific
        # feature already describes the module's changes.
        remaining -= touched
        if prefixes[0].startswith(("src/VICE.Lifestyle/", "src/VICE.Commerce/")):
            module = "/".join(prefixes[0].split("/")[:2]) + "/"
            remaining -= {path for path in remaining if path.startswith(module) and path.count("/") == 2}
    if any(path.startswith("src/") and path.endswith((".cs", ".json", ".shader", ".bundle")) for path in remaining):
        bullets.append("Updated other gameplay features.")
    if companion_changed:
        bullets.append("Updated the optional YouTube Companion.")
    if any(path.startswith("docs/") or path == "README.md" for path in changed):
        bullets.append("Updated player guides and documentation.")
    if not bullets:
        bullets.append("Updated suite maintenance and release support.")
    # Preserve every changed feature without producing a long commit-style list.
    if len(bullets) > 6:
        bullets = bullets[:5] + ["Also " + "; ".join(item[0].lower() + item[1:].rstrip(".")
                                                   for item in bullets[5:]) + "."]
    return bullets


def file_at(commit, path):
    if commit and path in tree(commit):
        return git("show", f"{commit}:{path}")
    return ""


def companion_identity(commit):
    source = file_at(commit, "scripts/Package-TVCompanion.ps1")
    return tuple(re.findall(r"\$(?:expectedArchive|expectedHelper)\s*=\s*'([A-Fa-f0-9]{64})'", source))


def generate(tag, current, previous_tag, base, repo):
    before, after = tree(base), tree(current)
    # Disable rename detection so both old and new feature scopes are accounted for.
    changed = set(git("diff", "--no-renames", "--name-only", base, current, "--").splitlines()) if base else after
    identity = companion_identity(current)
    previous_identity = companion_identity(base)
    companion_changed = bool(base and len(identity) == 2 and identity != previous_identity)
    lines = [f"- {bullet}" for bullet in summarize(changed, before, after, companion_changed)]
    poker_changed = any(path.startswith("src/VICE.Lifestyle/Poker/") for path in changed)
    poker_guide = file_at(current, "docs/vice-lifestyle-poker.md")
    if poker_changed and "Poker remains experimental" in poker_guide:
        lines += ["", "Poker remains experimental. Use matching VICE versions for multiplayer and test on a copied save."]
    if base and len(identity) == 2 and identity == previous_identity:
        lines += ["", "The optional YouTube Companion is unchanged; existing installations can be kept."]
    else:
        lines += ["", "The YouTube Companion is optional."]
    lines += ["", f"Downloads: **VICE-{tag}.zip** (main suite) and **VICE-YouTube-Companion-{tag}.zip** (optional YouTube support)."]
    if previous_tag and repo:
        old, new = urllib.parse.quote(previous_tag, safe=""), urllib.parse.quote(tag, safe="")
        lines += ["", f"[Changes since {previous_tag}](https://github.com/{repo}/compare/{old}...{new})."]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tag", required=True)
    parser.add_argument("--previous-tag", help="Explicit baseline for an offline preview")
    parser.add_argument("--repo", default=os.environ.get("GH_REPO", ""))
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    current = tag_commit(args.tag)
    if args.previous_tag:
        previous, base = args.previous_tag, tag_commit(args.previous_tag)
        if previous == args.tag or not is_ancestor(base, current):
            parser.error("The previous tag must be a different tag in this release's history.")
    else:
        previous, base = previous_release(published_releases(args.repo), args.tag, current)
    notes = generate(args.tag, current, previous, base, args.repo)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(notes, encoding="utf-8")
    print(f"Release notes: {previous or 'first release'} -> {args.tag}")
    print(notes)


if __name__ == "__main__":
    main()
