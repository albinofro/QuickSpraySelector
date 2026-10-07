# Release checklist

Prepared for Stooo: QuickSpraySelector 1.0.0.

## GitHub

The source folder is a standalone repository snapshot. It does not include an upstream remote or inherited Git history.

1. Create a new GitHub repository named `QuickSpraySelector` in your account. Upload the prepared source folder or push the standalone local repository.
2. Create tag `v1.0.0` and a GitHub release named `QuickSpraySelector 1.0.0`.
3. Use `RELEASE_NOTES.md` as the release body and attach the Thunderstore ZIP. Optionally attach the source ZIP too.
4. Once your repository exists, set `website_url` in `Thunderstore/manifest.json` to its URL and rebuild the package if you want a homepage link on Thunderstore. The field is currently empty, which is valid.

## Thunderstore

1. Sign in and select the Bomb Rush Cyberfunk community.
2. Select or create your author team, preferably `Stooo` if available. This is separate from your GitHub username.
3. Upload `QuickSpraySelector-1.0.0.zip` and choose appropriate categories.
4. Preview the listing and publish. The README includes the exact description and first-use hang note.

Use the Thunderstore ZIP as-is: its manifest, README and 256x256 PNG icon are at the archive root. Do not place them inside an extra enclosing folder.

Do not publish game DLLs, BepInEx DLLs, local compiler files, extracted sound folders or development backups. None are included in these prepared archives.

Official package requirements: https://wiki.thunderstore.io/mods/creating-a-package
