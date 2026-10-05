# D200X hardware validation

## First physical-test report — 2026-10-05

Evidence: user-supplied app log, with local times 11:06:58–11:08:20 (Europe/Riga), and the user reporting that the app seems to work. This records observed controls, not a claim of long-term stability. Personal logs are not bundled.

| Check | Evidence | Status |
| --- | --- | --- |
| HID detection | Main interface usage 12/1, reports 1025/1025, report ID 0; separate keyboard interface 1/6, reports 9/2 | Confirmed |
| LCD key input | Press/release on indices 0–6 and 10–13 | Confirmed for these 11 keys |
| Remaining LCD keys | Indices 7, 8 and 9 are absent from the supplied log | Pending |
| Dial rotations | Left/right events for indices 17, 18 and 19 | Confirmed |
| Dial presses | No dial press/release events in the supplied log | Pending |
| Side buttons | Press/release on indices 15 and 16 | Confirmed |
| Display transport | App reported Starter transfer completed, 21,203 bytes | Completed from the app's perspective |
| Physical display | User's general positive report does not specifically confirm numbered labels | Awaiting explicit confirmation |
| Action execution | Installed Starter profile has only none actions; no action execution entries supplied | Pending |
| Session errors | No error/disconnection entries in the supplied excerpt | No error shown in this short test |

Input continued after the display transfer. This supports successful communication following that transfer, but does not establish unplug/replug, sleep/wake, Studio restoration or crash-free operation during extended use.

Next: check missing controls and actual images, then one simple action, followed by lifecycle and normal-workload sessions. Changes to key placement and daily-use mappings require the user's desired layout.
