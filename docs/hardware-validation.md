# D200X hardware validation

## First physical-test report — 2026-10-05

Evidence: user-supplied app log, with local times 11:06:58–11:08:20 (Europe/Riga), and the user reporting that the app seems to work. This records observed controls, not a claim of long-term stability. Personal logs are not bundled.

| Check | Evidence | Status |
| --- | --- | --- |
| HID detection | Main interface usage 12/1, reports 1025/1025, report ID 0; separate keyboard interface 1/6, reports 9/2 | Confirmed |
| LCD key input | Initial log: indices 0–6 and 10–13; follow-up log at 11:13:06–11:13:08: indices 7, 8 and 9, each with press/release | Confirmed for all 14 keys |
| Dial rotations | Left/right events for indices 17, 18 and 19 | Confirmed |
| Dial presses | Follow-up preview 3 log shows press/release on 18 and 19 | Confirmed for 18 and 19; 17 pending |
| Side buttons | Press/release on indices 15 and 16 | Confirmed |
| Display transport | App reported Starter transfer completed, 21,203 bytes | Completed from the app's perspective |
| Physical display | User confirmed numbered labels and subsequently confirmed only the Key 13 label remains on preview 3 | Labels and Key 13 overlay correction confirmed |
| Action execution | Installed Starter profile has only none actions; no action execution entries supplied | Pending |
| Session errors | No error/disconnection entries in the supplied excerpt | No error shown in this short test |

Input continued after the display transfer. This supports successful communication following that transfer, but does not establish unplug/replug, sleep/wake, Studio restoration or crash-free operation during extended use.

Next: verify the one-minute fallback with keep-awake enabled, check dial 17 press, then one simple action, followed by lifecycle and normal-workload sessions. Changes to key placement and daily-use mappings require the user's desired layout.

## Key 13 overlay correction — preview 2

The initial app sent the icon bundle without selecting the separate wide-screen renderer's mode. The user observed the retained CPU/RAM/GPU gauges underneath the Key 13 label. The [D200X protocol reference](https://github.com/edubox/opendeck-ulanzi-d200x/blob/b4c75804ec40ee64e03e3504641a47ea24dcc23a/com.ulanzi.d200x.sdPlugin/d200x_plugin.py) identifies ordinary display command 0x0006 and image mode 2, distinct from stats mode 0 and clock mode 1.

Preview 2 selects image mode immediately before the icon bundle and restates it once afterward. Unused gauge/time fields are protocol placeholders in image mode, not displayed monitoring readings. This occurs only on an explicit page transfer, with the existing cancellation/timeout and no automatic retry. The wide icon is composed at 392×196 and encoded at 196×196 for the device's horizontal stretch; the app preview also spans both columns.

Tests verify mode selection, framing, bounded command count, ZIP preservation and rejecting invalid bundles before mode changes. The user physically confirmed removal of the overlay on preview 3: only the Key 13 label remains.

## Return to default graphics after one minute — preview 3

The user supplied a photo of stock key graphics and the Ulanzi Studio logo, then confirmed the transition occurs about one minute after sending a page. This does not prove that device RAM was cleared. At inspection, the installed app remained preview 1, Windows saw the HID device with OK status, and the recent System event query had no relevant USB entries. That snapshot does not exclude a transient disconnect. Whether input continues during the fallback is still unknown.

An independent [D200 SDK's heartbeat documentation](https://github.com/mindlss/open-ulanzi-d200/blob/main/docs/en.md#live-widget-and-heartbeat) describes idle sleep when widget/time updates cease and uses command 0x0006 as a keep-awake update. It targets D200; applying the same mechanism to D200X is an inference requiring validation. Another D200X implementation avoids repeatedly restating image mode because it can blink, so this is opt-in and experimental.

Preview 3 adds a checkbox, off by default, that sends one image-mode/time packet every 5 seconds while the user actively listens after sending a page. Separate read and write handles avoid sharing an outstanding read with a write on one stream; both remain inside the same guarded app session. A 3-second write timeout or any read/write failure ends both operations. Stop/exit cancels them. No page is automatically reuploaded and there is no reconnect/retry loop.

Test: send the profile, enable Keep device awake, start listening with actions disabled, and wait at least five minutes. Confirm the page remains, inputs continue and the wide screen does not blink. Stop afterward and compare whether the original approximately one-minute fallback returns. A short successful run is not proof of overall stability.

The next preview 3 log at 11:31:31–11:32:18 records a 23,007-byte Starter transfer with image mode, input on all 14 keys, all dial rotation directions, both side buttons, and presses on dials 18 and 19. The user still reports the one-minute fallback. No Keep-awake active or sent messages appear in this excerpt; a test with keep-awake enabled remains pending. These entries do not prove a firmware reset, RAM clearing or Windows crash.

The next log confirms keep-awake enabled at 11:39:17 and the first command sent at 11:39:22. The user subsequently reported that the device no longer sleeps. The workaround is physically confirmed for that session; exact elapsed duration, possible flicker and extended workload stability were not explicitly reported. Listening alone does not send keep-awake updates: the checkbox must also be enabled.

## Dark UI and module API — preview 4

Automated checks cover module dispatch with a fake platform, validation/cancellation before host access, duplicate-module rejection, schema-compatible serialization and edits that preserve other controls/gestures. The desktop check uses an isolated fixture profile, saves a dial mapping and Key 13 label, and verifies invalid input leaves the saved file unchanged. Rendered layouts are inspected separately. These checks send no USB packets, hotkeys or application launches. Physical action delivery and the updated UI's device lifecycle still need testing.

The desktop check also saves a side-button mapping, verifies running/stopped control states and rejects compact layouts that collapse control cards. Standard and compact renders were inspected at the host's 150% scale. Smaller windows scroll the deck/editor while keeping Save mapping visible. Installer payload verification passes. Full uninstall and physical action delivery remain separate checks.

## Key icons and capture presets — preview 5

All 72 hardware-free core checks pass. An isolated desktop fixture also checks PNG import/deduplication/transparency, every built-in icon, normal/wide display-image dimensions, malformed/missing/remote image rejection, preset editing, profile import with icon copying/backup/actions disabled, and invalid imports leaving the active profile unchanged. Standard and compact editor renders were inspected at 150% scaling. Tests do not modify the user's active profile, send USB packets or execute shortcuts.

Physical verification is pending: choose an icon for one key, save, stop listening and explicitly send the page. Confirm its appearance on the D200X, enable Keep awake and listen, then enable actions and test one screenshot mapping. Region recording opens Snipping Tool selection/start controls; Game Bar recording is a separate preset. No recording was started during automated checks. Existing sensor/price/FPS widgets and Discord horn playback remain unimplemented.

## Old page after completed draft writes and resize artifacts — preview 6

The app screenshot shows the Studio draft while the device photo shows Starter labels. Subsequent logs report completed 59,370-byte draft writes at 12:47:59, 12:49:16 and 12:49:22, and keep-awake active at 12:49:24. This establishes that sending was attempted; it does not establish that the firmware applied the images. The photo does not show the factory rings/logo.

Our device ZIPs reused `icons/key-N.png` across profiles. The [D200X reference implementation](https://github.com/edubox/opendeck-ulanzi-d200x/blob/main/com.ulanzi.d200x.sdPlugin/d200x_plugin.py) uses image-content names specifically to avoid firmware filename caching. Preview 6 similarly derives each PNG filename from its bytes and updates manifest references. Filename caching is a likely explanation, not a physically proven diagnosis. Packet framing, explicit upload and no-retry behavior remain unchanged. Automated checks verify changed pixels change filenames, unchanged pixels retain names, and every manifest image exists. Confirmation on the device remains pending.

The same app screenshot shows stale rounded-panel borders after resizing. Preview 6 invalidates/clears the full surface on resize and refreshes exposed areas at resize end. A hardware-free WinForms check verifies full-client invalidation for width/height increases and decreases; fresh bitmap renders alone would miss this defect.
