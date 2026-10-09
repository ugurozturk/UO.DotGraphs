#!/usr/bin/env python3
"""Copy the reviewed, standalone template to an existing repository; never merge."""
import argparse
from pathlib import Path
import shutil

TEMPLATE = Path(__file__).resolve().parents[1] / "templates/copilot/uographs"


def install(repository: Path, template: Path = TEMPLATE) -> Path:
    repository = repository.resolve(strict=True)
    if not repository.is_dir():
        raise ValueError("Target must be an existing repository directory")
    destination = repository / ".github/skills/uographs"
    for part in (repository / ".github", repository / ".github/skills", destination):
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
    args = parser.parse_args()
    try:
        print(install(args.repository))
    except (OSError, ValueError) as error:
        parser.exit(1, f"Skill not installed: {error}\n")


if __name__ == "__main__":
    main()
