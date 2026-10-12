# Local Geist font assets

These are the Latin variable-weight Geist and Geist Mono WOFF2 assets previously
served by this candidate's Next Google-font integration. They are now explicit
source assets so network-denied production builds do not fetch Google Fonts.
Families, normal style, 100–900 weights, swap display and CSS variables remain.

Geist: SHA-256 `9b6f5ff45b278c744b5f379a2c4ecbaf858a842b8eaf82ac8d21b699ca16c608`.
Geist Mono: SHA-256 `5f3d6ad60f29d6cb708414ec6887163d63bf197377ef5417d2483ff31ace6c3b`.

Provenance: existing local Next font output; fontconfig metadata and generated
CSS independently matched the families, variable weights and Latin subsets.
Upstream: https://github.com/vercel/geist-font. Both fonts are distributed under
the SIL Open Font License 1.1 retained in `OFL.txt`; no npm dependency is added.
