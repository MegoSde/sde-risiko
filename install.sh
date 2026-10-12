#!/usr/bin/env bash
set -Eeuo pipefail

# Ubuntu package instructions:
# https://learn.microsoft.com/dotnet/core/install/linux-ubuntu-install
if [[ ${1:-} == --help ]]; then
    echo 'Brug: ./install.sh'
    echo 'Installerer .NET 10 SDK på Ubuntu og henter Enterprise ATT&CK 19.2.'
    exit 0
fi
if (( $# )); then
    echo 'Ukendt argument. Brug ./install.sh --help.' >&2
    exit 1
fi

source /etc/os-release
if [[ ${ID:-} != ubuntu ]]; then
    echo 'Dette script kræver Ubuntu Linux.' >&2
    exit 1
fi
case ${VERSION_ID:-} in
    22.04|24.04|25.10|26.04) ;;
    *) echo "Ubuntu ${VERSION_ID:-ukendt} understøttes ikke af dette script." >&2; exit 1 ;;
esac

project_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
if [[ ! -w "$project_dir/Data/Seed" ]]; then
    echo "Du skal have skriverettigheder til $project_dir/Data/Seed." >&2
    exit 1
fi

as_root=()
if (( EUID != 0 )); then
    if ! command -v sudo >/dev/null; then
        echo 'Installer sudo eller kør scriptet som root.' >&2
        exit 1
    fi
    as_root=(sudo)
fi

"${as_root[@]}" apt-get update
"${as_root[@]}" apt-get install -y ca-certificates curl jq

if command -v dotnet >/dev/null && dotnet --list-sdks | grep -q '^10\.'; then
    echo '.NET 10 SDK er allerede installeret.'
else
    if [[ $VERSION_ID == 22.04 ]]; then
        "${as_root[@]}" apt-get install -y software-properties-common
        "${as_root[@]}" add-apt-repository -y ppa:dotnet/backports
        "${as_root[@]}" apt-get update
    fi
    "${as_root[@]}" apt-get install -y dotnet-sdk-10.0
fi

if ! dotnet --list-sdks | grep -q '^10\.'; then
    echo '.NET 10 SDK kunne ikke verificeres.' >&2
    exit 1
fi

catalog="$project_dir/Data/Seed/enterprise-attack.json"
download=$(mktemp "$project_dir/Data/Seed/.enterprise-attack.XXXXXX")
trap 'rm -f -- "$download"' EXIT
echo 'Henter MITRE Enterprise ATT&CK 19.2...'
curl --fail --location --retry 3 --connect-timeout 30 --max-time 600 \
    --proto '=https' --proto-redir '=https' \
    --output "$download" \
    'https://raw.githubusercontent.com/mitre-attack/attack-stix-data/master/enterprise-attack/enterprise-attack-19.2.json'
jq -e '.type == "bundle" and (.objects | type == "array") and any(.objects[]; .type == "attack-pattern" and (.revoked != true) and (.x_mitre_deprecated != true))' \
    "$download" >/dev/null
chmod 644 "$download"
mv -f -- "$download" "$catalog"
echo "ATT&CK-katalog gemt i $catalog"
echo 'Installation færdig. Start appen fra projektmappen med: dotnet run'
echo 'Kataloget importeres ved startup, hvis AttackTechniques er tom. Eksisterende SQLite-data bevares.'
