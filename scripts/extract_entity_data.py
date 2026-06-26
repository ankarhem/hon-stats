#!/usr/bin/env python3
"""Extract hero stat growth + item stats from HoN Reborn entity files.

The juvio gamedata API doesn't expose several fields that the game client reads
from local .entity files inside resources0.jz (a Zstd-compressed zip, the .s2z
successor). This script extracts those missing fields so they can be shipped as
static reference data.

Usage:
    python3 scripts/extract_entity_data.py <path-to-resources0.jz>

Requires 7zz on PATH (available via: nix shell nixpkgs#python3 nixpkgs#_7zz).

Output: src/HonStats.Infra/ReferenceData/entity-overrides.json
"""

import json
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUTPUT = REPO_ROOT / "src" / "HonStats.Infra" / "ReferenceData" / "entity-overrides.json"

HERO_GLOB = "heroes/*/base/hero.entity"
ITEM_GLOB = "items/recipes/*/item.entity"
PHOENIX_GLOB = "items/phoenix_rewards/*/item.entity"

ITEM_STAT_KEYS = {
    "attackspeed", "evasion", "movespeed", "attackrange",
    "armor", "magicarmor", "damage", "strength", "agility",
    "intelligence", "healthregen", "maxhealth",
}


def extract_entities(jz_path: str, dest: Path) -> None:
    result = subprocess.run(
        ["7zz", "x", jz_path, HERO_GLOB, ITEM_GLOB, PHOENIX_GLOB, f"-o{dest}", "-y"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        sys.exit(1)


def num(text: str | None) -> float | None:
    if text is None:
        return None
    try:
        return float(text.strip())
    except (ValueError, AttributeError):
        return None


def parse_hero(path: Path) -> dict | None:
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError:
        return None
    if root.tag != "hero":
        return None
    attr = root.attrib
    name = attr.get("name", "")
    if not name.startswith("Hero_"):
        return None

    data = {}
    for src_key, out_key in [
        ("strengthperlevel", "strengthPerLevel"),
        ("agilityperlevel", "agilityPerLevel"),
        ("intelligenceperlevel", "intelligencePerLevel"),
        ("armor", "armor"),
        ("magicarmor", "magicArmor"),
        ("healthregen", "healthRegen"),
        ("manaregen", "manaRegen"),
        ("sightrangeday", "sightRangeDay"),
        ("sightrangenight", "sightRangeNight"),
    ]:
        val = num(attr.get(src_key))
        if val is not None:
            data[out_key] = val

    return {name: data} if data else None


def parse_phoenix(path: Path) -> str | None:
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError:
        return None
    if root.tag != "item":
        return None
    name = root.attrib.get("name", "")
    return name if name.startswith("Item_") else None


def to_camel(snake: str) -> str:
    parts = snake.split("_") if "_" in snake else [snake]
    return parts[0] + "".join(p.capitalize() for p in parts[1:])


def parse_item_stats(attr: dict[str, str]) -> dict[str, float]:
    stats = {}
    for key in ITEM_STAT_KEYS:
        val = num(attr.get(key))
        if val is not None and val != 0:
            stats[to_camel(key)] = val
    return stats


def parse_item(path: Path) -> dict | None:
    try:
        root = ET.parse(path).getroot()
    except ET.ParseError:
        return None
    if root.tag != "item":
        return None
    attr = root.attrib
    name = attr.get("name", "")
    if not name.startswith("Item_"):
        return None

    data = {}
    flat = parse_item_stats(attr)
    if flat:
        data["stats"] = flat

    modifiers = {}
    for mod in root.findall("modifier"):
        cond = mod.attrib.get("condition")
        if not cond:
            continue
        mod_stats = parse_item_stats(mod.attrib)
        if mod_stats:
            modifiers[cond] = mod_stats

    if modifiers:
        data["modifiers"] = modifiers

    return {name: data} if data else None


def main() -> None:
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <path-to-resources0.jz>", file=sys.stderr)
        sys.exit(1)

    jz_path = sys.argv[1]
    if not Path(jz_path).is_file():
        print(f"Error: {jz_path} not found", file=sys.stderr)
        sys.exit(1)

    with tempfile.TemporaryDirectory(prefix="hon-entities-") as tmp:
        tmp_path = Path(tmp)
        print(f"Extracting entity files from {jz_path}...", file=sys.stderr)
        extract_entities(jz_path, tmp_path)

        heroes = {}
        for path in sorted(tmp_path.glob(HERO_GLOB)):
            result = parse_hero(path)
            if result:
                heroes.update(result)

        items = {}
        for path in sorted(tmp_path.glob(ITEM_GLOB)):
            result = parse_item(path)
            if result:
                items.update(result)

        phoenix_rewards = sorted(
            name
            for path in sorted(tmp_path.glob(PHOENIX_GLOB))
            if (name := parse_phoenix(path))
        )

    output = {"heroes": heroes, "items": items, "phoenixRewards": phoenix_rewards}
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(output, indent=2, sort_keys=True) + "\n")
    print(
        f"Wrote {len(heroes)} heroes, {len(items)} items, "
        f"{len(phoenix_rewards)} phoenix rewards to {OUTPUT}"
    )


if __name__ == "__main__":
    main()
