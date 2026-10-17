#!/usr/bin/env python3
"""Writes Crownroad.storekit, the StoreKit configuration for local testing, from data/shop_products.json.

Xcode serves these products instead of the App Store when a run uses the file (see README.md), so
purchases work in a simulator without App Store Connect. Run it again after changing the catalog.
Every product is a consumable: the game finishes each transaction once the server has recorded it,
and the server enforces the starter kit's once-per-account limit.
"""
import json
import pathlib
import zlib

here = pathlib.Path(__file__).resolve().parent
catalog = json.loads((here / "../../data/shop_products.json").read_text(encoding="utf-8"))


def stable_id(text, digits):
    # Xcode only needs the IDs to be unique within the file; deriving them keeps reruns diff-free.
    return str(zlib.crc32(text.encode()) % 10**digits).zfill(digits)


products = []
for product in sorted(catalog, key=lambda p: p["sortOrder"]):
    products.append({
        "displayPrice": f"{product['priceUsd']:.2f}",
        "familyShareable": False,
        "internalID": "6" + stable_id(product["appleProductId"], 9),
        "localizations": [{
            "description": product["description"],
            "displayName": product["displayName"],
            "locale": "en_US",
        }],
        "productID": product["appleProductId"],
        "referenceName": product["displayName"],
        "type": "Consumable",
    })

config = {
    "identifier": "C0A1B2D3",
    "nonRenewingSubscriptions": [],
    "products": products,
    "settings": {
        "_failTransactionsEnabled": False,
        "_locale": "en_US",
        "_storefront": "USA",
    },
    "subscriptionGroups": [],
    "version": {"major": 4, "minor": 0},
}
out = here / "Crownroad.storekit"
out.write_text(json.dumps(config, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"Wrote {len(products)} products to {out.relative_to(here.parent.parent)}")
