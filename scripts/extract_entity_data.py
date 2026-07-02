#!/usr/bin/env python3
"""Extract hero stat growth + item stats + role icons from HoN Reborn files.

The juvio gamedata API doesn't expose several fields that the game client reads
from local .entity files inside resources0.jz (a Zstd-compressed zip, the .s2z
successor). This script extracts those missing fields so they can be shipped as
static reference data. It also extracts the matchmaking role-pick icons, which
juvio serves no CDN path for (gamestorage is heroes/items only).

Usage:
    python3 scripts/extract_entity_data.py <path-to-resources0.jz>

Requires 7zz on PATH (available via: nix shell nixpkgs#python3 nixpkgs#_7zz).

Output:
    src/HonStats.Infra/ReferenceData/entity-overrides.json
    src/HonStats.Web/wwwroot/img/roles/role-*.png
"""

import json
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
OUTPUT = REPO_ROOT / "src" / "HonStats.Infra" / "ReferenceData" / "entity-overrides.json"

HERO_GLOB = "heroes/*/base/hero.entity"
ITEM_GLOBS = ["items/recipes/*/item.entity", "items/basic/*/item.entity"]
STATE_GLOBS = ["items/recipes/*/state.entity", "items/basic/*/state.entity"]
PHOENIX_GLOB = "items/phoenix_rewards/*/item.entity"

ROLE_ICON_GLOB = "preact/dist/assets/roles/*.png"
ROLE_ICON_OUTPUT = REPO_ROOT / "src" / "HonStats.Web" / "wwwroot" / "img" / "roles"

ITEM_STAT_MAP = {
    "attackspeed": "attackSpeed",
    "evasion": "evasion",
    "movespeed": "moveSpeed",
    "movespeedmultiplier": "moveSpeedMultiplier",
    "attackrange": "attackRange",
    "armor": "armor",
    "magicarmor": "magicArmor",
    "damage": "damage",
    "strength": "strength",
    "agility": "agility",
    "intelligence": "intelligence",
    "healthregen": "healthRegen",
    "healthregenpercent": "healthRegenPercent",
    "maxhealth": "maxHealth",
    "manaregenmultiplier": "manaRegenMultiplier",
    "lifesteal": "lifesteal",
    "deflection": "deflection",
    "reducedabilitycooldowns": "reducedAbilityCooldowns",
    "stunneddurationmultiplier": "stunnedDurationMultiplier",
    "debuffdurationmultiplier": "debuffDurationMultiplier",
    "criticalchance": "criticalChance",
    "criticalmultiplier": "criticalMultiplier",
}


def extract_entities(jz_path: str, dest: Path) -> None:
    result = subprocess.run(
        ["7zz", "x", jz_path, HERO_GLOB, *ITEM_GLOBS, *STATE_GLOBS, PHOENIX_GLOB, f"-o{dest}", "-y"],
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        sys.exit(1)


def extract_role_icons(jz_path: str) -> int:
    with tempfile.TemporaryDirectory(prefix="hon-roles-") as tmp:
        tmp_path = Path(tmp)
        result = subprocess.run(
            ["7zz", "x", jz_path, ROLE_ICON_GLOB, f"-o{tmp_path}", "-y"],
            capture_output=True,
            text=True,
        )
        if result.returncode != 0:
            print(result.stderr, file=sys.stderr)
            sys.exit(1)
        src_dir = tmp_path / "preact" / "dist" / "assets" / "roles"
        icons = sorted(src_dir.glob("role-*.png"))
        if not icons:
            print("Warning: no role icons found in archive", file=sys.stderr)
            return 0
        ROLE_ICON_OUTPUT.mkdir(parents=True, exist_ok=True)
        for icon in icons:
            shutil.copy2(icon, ROLE_ICON_OUTPUT / icon.name)
        return len(icons)


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


def parse_item_stats(attr: dict[str, str]) -> dict[str, float]:
    stats = {}
    for src_key, out_key in ITEM_STAT_MAP.items():
        val = num(attr.get(src_key))
        if val is not None and val != 0:
            if out_key == "attackSpeed":
                val *= 100
            stats[out_key] = val
    return stats


def parse_modifiers(parent: ET.Element, flat: dict[str, float]) -> dict[str, dict[str, float]]:
    modifiers = {}
    for mod in parent.findall("modifier"):
        mod_stats = parse_item_stats(mod.attrib)
        if not mod_stats:
            continue
        cond = mod.attrib.get("condition")
        if not cond:
            flat.update(mod_stats)
            continue
        for key in list(mod_stats):
            if key in flat and flat[key] == mod_stats[key]:
                del mod_stats[key]
        if mod_stats:
            modifiers[cond] = mod_stats
    return modifiers


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

    modifiers = parse_modifiers(root, flat)

    state_path = path.parent / "state.entity"
    if state_path.exists():
        try:
            state_root = ET.parse(state_path).getroot()
            for cond, mod_stats in parse_modifiers(state_root, flat).items():
                if cond in modifiers:
                    modifiers[cond].update(mod_stats)
                else:
                    modifiers[cond] = mod_stats
        except ET.ParseError:
            pass

    if flat:
        data["stats"] = flat

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
        for glob in ITEM_GLOBS:
            for path in sorted(tmp_path.glob(glob)):
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

    role_icons = extract_role_icons(jz_path)

    print(
        f"Wrote {len(heroes)} heroes, {len(items)} items, "
        f"{len(phoenix_rewards)} phoenix rewards to {OUTPUT}"
        + (f", {role_icons} role icons to {ROLE_ICON_OUTPUT}" if role_icons else "")
    )


if __name__ == "__main__":
    main()
