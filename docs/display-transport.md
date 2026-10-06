# Screen snapshots and the bounded update probe

Status: preview 14, prepared 2026-10-05. Uploaded images now include hardware values. Synthetic rendering/framing checks pass; physical snapshot appearance and the small-image command's D200X behavior remain unverified. Continuous physical refresh is disabled.

## Current values in an explicit upload

Update screens resumes assigned providers, waits up to four seconds for output/the first CPU counter interval, then captures samples on the UI thread. Rendering rechecks original observation times: older than five seconds, more than two seconds in the future, missing, invalid or unavailable values show `--`. Each binding is independent; unavailable GPU data does not erase CPU/RAM. Labels, colors, icons, precision and press actions stay in the profile; readings never enter its JSON. Key 13 composes at 392x196 and encodes at 196x196 for the firmware stretch.

The existing command 0x0001 sends a whole page only on an explicit Update screens click. It is a snapshot: numbers stay static and can become outdated until another upload. Source failure after upload is visible in the app, not automatically on the physical static page.

## Candidate for changed-key updates

The independent [D200 SDK packet documentation](https://github.com/mindlss/open-ulanzi-d200/blob/main/docs/en.md#protocol-packets) names KEY_IMAGE_ZIP as command 0x000d. Its [image/bundle implementation](https://github.com/mindlss/open-ulanzi-d200/blob/main/src/D200.js) builds a ZIP with one col_row manifest entry, State 0, ViewParam with empty Font/Text and an image asset under Images/, then uses a header plus raw continuation packets. This is D200 evidence; applicability to the D200X is an inference requiring a physical test.

Direct's one-shot probe uses that command, exactly one saved LCD key, one PNG with a content-derived name and a temporary TEST label. It does not write the profile, alter actions, change global fonts/brightness or start periodic transfers. The ZIP is bounded to 64 KiB, padding attempts to 256, and the transfer to the existing 30-second cancellable session. At the size limit it takes at most 65 reports. Continuation boundaries are validated before opening the writable handle; report ID/capability, exclusive-controller and Studio guards remain. Failure ends the attempt without retry. No mode packet is inserted in the probe; Update screens establishes image mode first.

## Physical procedure

1. Exit the previous app through its tray menu and install/update preview 14. Fully exit Studio. Do not run another controller simultaneously.
2. Stop device control and choose Update screens. Confirm CPU/RAM numbers appear where assigned. Temperatures require a configured, fresh supported log; a missing source correctly shows `--`.
3. Select an ordinary LCD key, for example key 10, then choose Test selected screen. Confirm only that key gains the TEST label and snapshot values, while other tiles stay intact. Record byte/report counts in Activity and any flicker, reset or disconnect.
4. Select key 13 and run the same one-shot test. Confirm wide aspect ratio, independent values and unchanged other tiles. Do not enable automatic repeating tests.
5. Choose Update screens to remove the temporary marker. Start with actions off for an input-only check; confirm button/dial input and keep-screen-on, then Stop. If the candidate fails, use only the existing explicit page upload and report the result.

An upload-complete log proves only that Windows accepted the writes, not that a key rendered or omitted cells were preserved. Before continuous refresh, record successful ordinary/wide tests, integrate one shared write scheduler for changed-key/coalesced updates and keep-awake, and verify input/flicker, stale transitions, stop/exit/disconnect and resource behavior. Repeated full-page uploads are not a substitute.
