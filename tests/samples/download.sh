#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

BASE="https://images.unsplash.com"
W=800

declare -A IMAGES=(
  # Whiskey
  ["whiskey-01-jack-daniels.jpg"]="photo-1681040900989-645cecfd8ea4"
  ["whiskey-02-bourbon.jpg"]="photo-1602166242292-93a00e63e8e8"
  ["whiskey-03-japanese-collection.jpg"]="photo-1772442034167-462f7553016b"
  ["whiskey-04-dead-rabbit-irish.jpg"]="photo-1638518220185-2d1b461f4f7c"
  # Wine
  ["wine-01-bottle-label.jpg"]="photo-1733248113944-c4f7dc132dac"
  ["wine-02-red-label.jpg"]="photo-1695634739311-d0e49339a089"
  ["wine-03-table-setting.jpg"]="photo-1695634739401-d2a54e36a6a9"
  # Cocktails
  ["cocktail-01-pouring.jpg"]="photo-1566417713940-fe7c737a9ef2"
  ["cocktail-02-old-fashioned.jpg"]="photo-1598994671512-395d7a6147e0"
  ["cocktail-03-pink-drink.jpg"]="photo-1605270012917-bf157c5a9541"
  # Cigars
  ["cigar-01-lighting.jpg"]="photo-1773080521729-18239e46f50a"
  ["cigar-02-pile.jpg"]="photo-1694716479704-459f025d0793"
  ["cigar-03-box.jpg"]="photo-1694716438178-c6f34bddd64d"
)

echo "Downloading ${#IMAGES[@]} test images from Unsplash..."
for name in $(echo "${!IMAGES[@]}" | tr ' ' '\n' | sort); do
  if [[ -f "$name" ]]; then
    echo "  ✓ $name (exists)"
    continue
  fi
  id="${IMAGES[$name]}"
  url="${BASE}/${id}?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=${W}"
  echo -n "  ↓ $name... "
  curl -sL -o "$name" "$url"
  echo "done ($(du -h "$name" | cut -f1))"
done

echo "All images downloaded to $SCRIPT_DIR"
