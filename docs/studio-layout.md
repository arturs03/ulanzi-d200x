# Recreating the previous Studio page

The source includes `profiles/studio-layout.json`, a validated layout draft compatible with preview 5. Load it with Import profile, then stop and explicitly Update screens. It does not replace the installed profile or write to the device automatically. A separate private copy can contain a locally verified application path; public templates contain no personal paths.

| Row | Column 1 | Column 2 | Column 3 | Column 4 | Column 5 |
| --- | --- | --- | --- | --- | --- |
| Top | ChatGPT | Steam | Mute | Screenshot (proposed) | Record region (proposed) |
| Middle | NVDA | SOL-USD | AAPL | Empty | FPS |
| Bottom | CPU temperature | GPU temperature | GPU hotspot | CPU/RAM/GPU usage, wide screen | Wide screen continued |

The user clarified that the earlier rocket icon was Discord mute, with Ctrl+Alt+Shift+M. The draft preserves that behavior on key 2. Discord must retain its matching shortcut. ChatGPT is currently a browser link; desktop-app activation would be a separate mapping. The private draft launches the locally verified Steam executable. In the portable public template Steam is unassigned until a path is configured.

NVDA/SOL-USD/AAPL currently open their corresponding Yahoo Finance pages; this is not live quote acquisition and those pages were rate-limited during online verification. Their labels show `--`, as do FPS, temperatures and usage. All dials/side buttons remain unassigned because their intended functions have not been specified. Generic built-in icons are included. Existing proprietary images/icons are not imported or redistributed.

## What is possible now

The positions, labels, colors, mute shortcut and app/website actions use existing profile features. Both public and private drafts passed profile validation. No shortcut was fired and no USB page was uploaded during preparation.

## Modules still required

The accepted direction is a C# host with Rust executable data providers and a language-independent protocol. See [architecture](architecture.md), [protocol draft](plugin-protocol.md) and [resource requirements](performance.md). The providers, loader and continuous widget refresh are not implemented; the source choices below remain integration work. Offline-source observations in this document are dated development context, not a diagnosis for every checkout.

- **Exact artwork:** preview 5 supports built-in icons and local PNG images. An exact visual match still needs the original assets or user-selected replacements.
- **Hardware display:** connect to the existing loopback sensor bridge and/or read the existing OHM CSV, preserving explicit sensor/hardware IDs, freshness checks and `--` for unavailable data. The bridge is currently offline and the newest CSV found is from yesterday. No new sensor driver has been installed.
- **Usage:** use read-only Windows CPU/RAM APIs and an explicit GPU data source. Keep the integrated and discrete GPUs separate.
- **Quotes:** select a documented provider, currency, refresh rate and stale/error display. Loading a quote website does not give the app a supported quote API.
- **FPS:** use a verified available provider. FPS is not generally obtainable from every game through a normal process counter. Existing bridge support for AMD fullscreen FPS is conditional; additional tooling has not been installed.
- **Live refresh:** add display-widget contracts separate from button actions, bounded acquisition and a single serialized device writer. Verify small per-key updates on the physical D200X before promising continuous refresh; do not repeatedly resend the full page as a shortcut.

The action registry already separates dispatch/validation from device I/O. Live display modules need their own provider and refresh contracts; attaching an action module alone cannot update a button's image.

## Horn/soundboard

The user requested a horn audible to the Discord voice channel. Keep mute on key 2. A spare button can be proposed for the horn after a trigger is verified.

Discord's [official soundboard guide](https://support.discord.com/hc/en-us/articles/12612888127767-Discord-Soundboard-Guide-Using-Adding-and-Managing-Sounds) documents channel playback and an overlay-opening shortcut. Opening the panel is different from selecting/playing a specific horn. The installed client's support for an individual sound hotkey has not been verified. A bot integration would need a separate bot, permissions and voice-channel connection; no bot, token handling or voice connection is implemented in this app.

Local speaker playback alone would not satisfy the requested channel-audible sound. No horn mapping has been claimed functional or automatically configured.
