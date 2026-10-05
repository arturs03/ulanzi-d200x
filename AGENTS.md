# D200X Direct development

This is an independent Windows app, CLI diagnostic tool and per-user installer for the Ulanzi D200X. Keep profiles editable by any LLM through the documented JSON format; changing a profile must not require recompilation or an LLM API account.

- Read README.md, docs/customization.md and profiles/profile.schema.json before changing public configuration.
- Build with ./build.ps1. Generate distributables with ./package.ps1. Use the existing .NET 8 SDK; do not add drivers or system dependencies.
- Root C# files implement HID/protocol/config validation and CLI checks. Desktop contains the WinForms app; Installer contains the setup app. out and releases are generated and ignored by Git.
- Keep the default app stopped, with configured actions disabled. Loading a profile must not launch apps, send keys or write to a device. Display transfers require an explicit UI action.
- Preserve strict profile validation, native HID capability selection, concurrent-controller guards and the Studio-running check. Do not silently add shell/script actions, firmware writes, services or automatic startup.
- Installer operations are per user. Preserve profiles on update/uninstall. Validate fixed installation paths and reject directory links before removal. Never remove shared .NET caches or unrelated applications.
- Distinguish automated tests and app rendering from physical device verification. Physical button/dial input, display updates, mapped action delivery and long-term stability remain unverified in this preview.
- Public source and release files must contain no user-specific profiles, device identifiers, personal logs, crash dumps or credentials.
