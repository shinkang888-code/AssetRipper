# PMKE Decryptor (static web tool)

A single-file, client-side tool that converts a `PMKE`-wrapped AssetBundle back into a standard
`UnityFS` bundle. It runs entirely in the browser via the Web Crypto API — the file never leaves
your device and there is no backend.

## Use

Open `index.html` in a browser (or host it on any static site — GitHub Pages, Netlify, etc.), drop
a `.bundle` file, paste the 32-hex-character AES-128 key for your title, and download the decrypted
bundle. Then open it in AssetRipper.

No key is bundled here — it is game/build specific and must be supplied by the user.

## Format

`PMKE` (4-byte magic) + version byte + 16-byte IV, followed by an AES-128-CTR encrypted UnityFS
bundle. The IV is the initial 128-bit big-endian counter, incremented once per 16-byte block; the
payload starts at offset 21.

This mirrors the built-in `PmkeBundleScheme` (see `AssetRipper.IO.Files`), which does the same
decryption in-process when `AR_PMKE_KEY` is set.
