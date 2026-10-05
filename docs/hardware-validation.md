# D200X hardware validation

## First physical-test report — 2026-10-05

Evidence: user-supplied app log, with local times 11:06:58–11:08:20 (Europe/Riga), and the user reporting that the app seems to work. This records observed controls, not a claim of long-term stability. Personal logs are not bundled.

| Check | Evidence | Status |
| --- | --- | --- |
| HID detection | Main interface usage 12/1, reports 1025/1025, report ID 0; separate keyboard interface 1/6, reports 9/2 | Confirmed |
| LCD key input | Initial log: indices 0–6 and 10–13; follow-up log at 11:13:06–11:13:08: indices 7, 8 and 9, each with press/release | Confirmed for all 14 keys |
| Dial rotations | Left/right events for indices 17, 18 and 19 | Confirmed |
| Dial presses | No dial press/release events in the supplied log | Pending |
| Side buttons | Press/release on indices 15 and 16 | Confirmed |
| Display transport | App reported Starter transfer completed, 21,203 bytes | Completed from the app's perspective |
| Physical display | User subsequently answered yes to the numbered-label confirmation, then reported built-in CPU/RAM/GPU gauges behind Key 13 | Labels confirmed; Key 13 overlay correction pending |
| Action execution | Installed Starter profile has only none actions; no action execution entries supplied | Pending |
| Session errors | No error/disconnection entries in the supplied excerpt | No error shown in this short test |

Input continued after the display transfer. This supports successful communication following that transfer, but does not establish unplug/replug, sleep/wake, Studio restoration or crash-free operation during extended use.

Next: check dial presses and the preview 2 overlay correction, then one simple action, followed by lifecycle and normal-workload sessions. Changes to key placement and daily-use mappings require the user's desired layout.

## Key 13 overlay correction — preview 2

The initial app sent the icon bundle without selecting the separate wide-screen renderer's mode. The user observed the retained CPU/RAM/GPU gauges underneath the Key 13 label. The [D200X protocol reference](https://github.com/edubox/opendeck-ulanzi-d200x/blob/b4c75804ec40ee64e03e3504641a47ea24dcc23a/com.ulanzi.d200x.sdPlugin/d200x_plugin.py) identifies ordinary display command 0x0006 and image mode 2, distinct from stats mode 0 and clock mode 1.

Preview 2 selects image mode immediately before the icon bundle and restates it once afterward. Unused gauge/time fields are protocol placeholders in image mode, not displayed monitoring readings. This occurs only on an explicit page transfer, with the existing cancellation/timeout and no automatic retry. The wide icon is composed at 392×196 and encoded at 196×196 for the device's horizontal stretch; the app preview also spans both columns.

Tests verify mode selection, framing, bounded command count, ZIP preservation and rejecting invalid bundles before mode changes. Physical removal of the overlay must still be confirmed after updating and resending the page.
