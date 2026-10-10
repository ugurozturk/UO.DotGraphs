#!/usr/bin/env python3
"""Copy the reviewed, standalone template to an existing repository; never merge."""
import argparse
from pathlib import Path
import shutil

ROOT = Path(__file__).resolve().parents[1]
AGENT_DIRECTORIES = {"copilot": ".github", "codex": ".agents"}
TEMPLATE = ROOT / "templates/copilot/uographs"


def install(repository: Path, template: Path | None = None, *, agent: str = "copilot") -> Path:
    if agent not in AGENT_DIRECTORIES:
        raise ValueError(f"Unsupported agent: {agent}")
    if template is None:
        template = ROOT / "templates" / agent / "uographs"
    repository = repository.resolve(strict=True)
    if not repository.is_dir():
        raise ValueError("Target must be an existing repository directory")
    agent_root = repository / AGENT_DIRECTORIES[agent]
    destination = agent_root / "skills/uographs"
    for part in (agent_root, destination.parent, destination):
        if part.is_symlink():
            raise ValueError(f"Refusing symlink destination: {part}")
    if destination.exists():
        raise FileExistsError(f"Skill already exists; review and merge manually: {destination}")
    files = sorted(template.rglob("*"))
    if not (template / "SKILL.md").is_file() or any(p.is_symlink() for p in files):
        raise ValueError("Template must contain SKILL.md and no symlinks")
    # mkdir is exclusive even when a second installer runs after the initial check.
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.mkdir()
    # A failed copy is intentionally left for inspection; never delete user files.
    for source in files:
        target = destination / source.relative_to(template)
        if source.is_dir():
            target.mkdir()
        else:
            with source.open("rb") as reader, target.open("xb") as writer:
                shutil.copyfileobj(reader, writer)
    return destination


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("repository", type=Path, help="Existing, authorized target repository")
    parser.add_argument("--agent", choices=AGENT_DIRECTORIES, default="copilot",
                        help="Skill host (default: copilot); codex uses .agents/skills")
    args = parser.parse_args()
    try:
        print(install(args.repository, agent=args.agent))
    except (OSError, ValueError) as error:
        parser.exit(1, f"Skill not installed: {error}\n")


if __name__ == "__main__":
    main()
