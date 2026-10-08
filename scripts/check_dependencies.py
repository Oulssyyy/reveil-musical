#!/usr/bin/env python3
"""Contrôle légal et fraîcheur des dépendances NuGet (directes et transitives).

Échoue (code 1) si un paquet :
  - a une licence hors de la liste blanche (allowed-licenses.json) ou non déclarée en SPDX ;
  - est plus ancien que maxAgeYears ou marqué déprécié sur nuget.org,
sauf s'il figure dans la section "reviewed" avec une justification.

Usage : python3 scripts/check_dependencies.py [--markdown]
"""
import datetime
import json
import os
import re
import subprocess
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NUGET_HOME = os.environ.get("NUGET_PACKAGES", os.path.expanduser("~/.nuget/packages"))
REGISTRATION = "https://api.nuget.org/v3/registration5-semver1/{}/index.json"


def installed_packages():
    out = subprocess.run(
        ["dotnet", "list", os.path.join(ROOT, "ReveilMusical.slnx"), "package", "--include-transitive", "--format", "json"],
        check=True, capture_output=True, text=True).stdout
    packages = {}
    for project in json.loads(out)["projects"]:
        for framework in project.get("frameworks", []):
            for kind in ("topLevelPackages", "transitivePackages"):
                for p in framework.get(kind, []):
                    entry = packages.setdefault(p["id"], {"version": p["resolvedVersion"], "direct": False})
                    entry["direct"] |= kind == "topLevelPackages"
    return packages


def license_of(package_id, version):
    nuspec = os.path.join(NUGET_HOME, package_id.lower(), version, f"{package_id.lower()}.nuspec")
    with open(nuspec, encoding="utf-8-sig") as f:
        match = re.search(r'<license type="expression">([^<]+)</license>', f.read())
    return match.group(1) if match else None


def registry_info(package_id, version):
    with urllib.request.urlopen(REGISTRATION.format(package_id.lower()), timeout=20) as r:
        index = json.load(r)
    entries = []
    for page in index["items"]:
        if "items" not in page:
            with urllib.request.urlopen(page["@id"], timeout=20) as r:
                page = json.load(r)
        entries += [i["catalogEntry"] for i in page["items"]]
    stable = [e for e in entries if "-" not in e["version"] and e.get("listed", True)]
    current = next(e for e in entries if e["version"] == version)
    return current["published"][:10], stable[-1]["version"], stable[-1]["published"][:10], bool(current.get("deprecation"))


def main():
    with open(os.path.join(ROOT, "allowed-licenses.json"), encoding="utf-8") as f:
        policy = json.load(f)
    allowed, reviewed = set(policy["allowed"]), policy.get("reviewed", {})
    oldest = datetime.date.today() - datetime.timedelta(days=365 * policy["maxAgeYears"])

    rows, problems = [], []
    for package_id, info in sorted(installed_packages().items(), key=lambda kv: kv[0].lower()):
        version = info["version"]
        lic = license_of(package_id, version)
        published, latest, latest_date, deprecated = registry_info(package_id, version)

        issues = []
        if lic is None or any(part not in allowed for part in re.split(r"\s+(?:OR|AND)\s+", lic)):
            issues.append(f"licence {lic or 'non SPDX'}")
        if datetime.date.fromisoformat(published) < oldest:
            issues.append(f"publiée le {published}")
        if deprecated:
            issues.append("dépréciée")
        status = "OK" if not issues else ("revue" if package_id in reviewed else "À REVOIR")
        if issues and package_id not in reviewed:
            problems.append(f"{package_id} {version} : {', '.join(issues)}")
        rows.append((package_id, version, published, latest, latest_date, lic or "?",
                     "directe" if info["direct"] else "transitive", status))

    if "--markdown" in sys.argv:
        print("| Paquet | Installée | Publiée | Dernière stable | Licence | Type | Statut |")
        print("|---|---|---|---|---|---|---|")
        for p, v, pub, last, last_date, lic, kind, status in rows:
            print(f"| {p} | {v} | {pub} | {last} ({last_date}) | {lic} | {kind} | {status} |")
    else:
        for row in rows:
            print("{:<55} {:<9} {:<11} {:<9} {:<14} {:<10} {}".format(row[0], row[1], row[2], row[3], row[5], row[6], row[7]))

    if problems:
        print("\nDépendances non conformes :", *problems, sep="\n  - ", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
