# Code map: (project root)

Written through codemap_write. One entry per --- block; values are JSON. Freshness is computed when read, never stored here.

---
file: "SteamIdle.cs"
kind: "how"
text: "Checking Idle? logs in with SteamKit2 and sends EMsg.ClientGamesPlayed (CMsgClientGamesPlayed) listing each checked appid as game_id, the same message HourBoostr uses. An empty list clears the playing state. SteamIdle.MaxGames is 34. Unchecking or closing the form sends the updated list; Dispose sends an empty list and logs off."
hash: "content:2974f47f7ff67319"
written: "2026-10-13"
by: "core"
---

---
file: "SteamLibrary.cs"
kind: "how"
text: "SteamLibraryScan.Run resolves the most recently logged-in account from config\\loginusers.vdf (MostRecent, else newest Timestamp), decrypts userdata\\<steam32>\\config\\licensecache with Valve's RandomStream XOR seeded by the signed steam32 id, reads owned package ids from the protobuf license list, maps them to appids through appcache\\packageinfo.vdf, and keeps the type=game entries named from appcache\\appinfo.vdf. Installed is only a flag, from config\\libraryfolders.vdf."
hash: "content:a07aabbe21d1efb6"
written: "2026-10-13"
by: "core"
---

---
file: "SteamLibrary.cs"
symbol: "LicenseCacheCipher"
ordinal: 0
kind: "trap"
text: "licensecache is XOR-encrypted, not plaintext. Its protobuf length-delimited fields must be bounds-checked before skipping: an unchecked length walks into the next license and the parser then reports a nonsense wire type. The trailing 4 bytes are a checksum and are excluded before parsing."
evidence: "`output[index] = (byte)(data[index] ^ generator.Next(32, 126));`"
hash: "span:1b1b44187db9aa66"
quote: "quote:f3e9bf707e300256"
written: "2026-10-13"
by: "core"
---
