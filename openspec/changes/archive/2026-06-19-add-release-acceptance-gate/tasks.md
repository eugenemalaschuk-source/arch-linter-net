## 1. Edit release workflow

- [x] 1.1 Fetch current `.github/workflows/release-nuget.yml` from GitHub's `main` branch
- [x] 1.2 Add `Setup uv` step after `Setup .NET` (between line 54 and Restore)
- [x] 1.3 Add `Acceptance` step after `Build` and before `Pack Core`

## 2. Verify

- [x] 2.1 Run `make acceptance` locally to confirm gate still passes
- [x] 2.2 Trigger manual release workflow with `publish=false` and confirm acceptance runs before pack
