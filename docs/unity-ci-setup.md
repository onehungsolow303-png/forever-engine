# Enabling the Unity job in GitHub Actions CI

The `forever-engine` repo's CI workflow has two jobs:

1. **`cross-module-http`** — always runs, no secrets needed. Boots both Python services and curls them.
2. **`unity`** — gated on `vars.UNITY_LICENSE_AVAILABLE == 'true'`. Skipped by default.

To enable the gated `unity` job you need to give the runner a Unity Personal license file plus your Unity ID credentials. This is a **one-time, ~30-minute setup** that requires a browser. game-ci has a polished walkthrough, but here's the abridged version:

## What you need

- A free Unity ID account (you almost certainly already have one if you've ever opened the Editor)
- The exact Unity version: **6000.4.1f1**
- About 30 minutes of browser interaction

## Steps

### 1. Generate your license file

The easiest path uses game-ci's `unity-request-activation-file` action, which produces a `.alf` file that Unity Cloud signs into a `.ulf` license file. Step-by-step:

a. Go to https://github.com/onehungsolow303-png/forever-engine/actions
b. Click **New workflow** → **set up a workflow yourself**
c. Paste this workflow temporarily (don't commit):

```yaml
name: request-license
on: workflow_dispatch
jobs:
  request:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: game-ci/unity-request-activation-file@v2
        id: getManualLicenseFile
        with:
          unityVersion: 6000.4.1f1
      - uses: actions/upload-artifact@v4
        with:
          name: Manual Activation File
          path: ${{ steps.getManualLicenseFile.outputs.filePath }}
```

d. Save and click **Run workflow** manually (the dispatch button)
e. When the run finishes, download the `Manual Activation File` artifact — it contains a `.alf` file
f. Open https://license.unity3d.com/manual in a browser
g. Upload the `.alf` file
h. Pick **Unity Personal**, fill out any business questions (Hobbyist / not company / etc)
i. Download the resulting `.ulf` file — this is your license

### 2. Add the secrets to your GitHub repo

Go to: https://github.com/onehungsolow303-png/forever-engine/settings/secrets/actions

Add three repository secrets:

| Name | Value |
|---|---|
| `UNITY_LICENSE` | The **entire contents** of the `.ulf` file from step 1i. Open it in a text editor, copy everything, paste into the secret value field. |
| `UNITY_EMAIL` | Your Unity ID email |
| `UNITY_PASSWORD` | Your Unity ID password |

### 3. Enable the Unity job in CI

Same settings page, switch to **Variables** tab (next to Secrets). Add a repository **variable**:

| Name | Value |
|---|---|
| `UNITY_LICENSE_AVAILABLE` | `true` |

The `forever-engine` workflow's `unity` job is gated on `${{ vars.UNITY_LICENSE_AVAILABLE == 'true' }}`. Setting that variable flips the gate open.

### 4. Delete the temporary `request-license` workflow file

You don't need it long-term and leaving it in lets anyone with workflow_dispatch rights regenerate license files. Either delete it from the GitHub UI or via:

```bash
gh workflow delete request-license --repo onehungsolow303-png/forever-engine
```

### 5. Trigger a CI run to verify

Push any small change to `forever-engine`. Watch:

```bash
gh run list --repo onehungsolow303-png/forever-engine
gh run watch --repo onehungsolow303-png/forever-engine
```

The `unity` job should now appear and execute. First run is slow (~5-15 min) because it downloads the Unity Editor docker image. Subsequent runs are faster thanks to the workflow's `Library/` cache.

## What you get when this is enabled

- **EditMode tests** run on every push (`DialoguePanelTests` and any future tests)
- **`SmokeTestRunner.Run`** runs against the live Python services in CI
- **`DialogueSmokeTest.Run`** runs against the same
- Compile errors in C# fail CI before they reach `master`

## Why this isn't automated

`gh secret set` can write the secret values, but step 1 (downloading the `.alf`, uploading to license.unity3d.com, pasting checkboxes about company size, downloading the `.ulf`) requires a browser. Unity does not expose a CLI for this flow. The first time setup is genuinely manual.

After it's set up once, the secrets persist and CI runs Unity automatically forever — no further interaction needed.

## Troubleshooting

- **`Activation failed: License is not yet valid`** — Wait 5 minutes and re-run. Unity Cloud sometimes lags after issuing a license.
- **`UNITY_LICENSE secret value is empty`** — You probably pasted just a snippet. The `.ulf` file is XML; the secret needs the full `<root>...</root>` blob.
- **CI runs the `unity` job but skips all steps** — `vars.UNITY_LICENSE_AVAILABLE` is set as a secret instead of a variable. They're in different tabs on the GitHub settings page.

## Alternative: skip Unity CI

The `cross-module-http` job already covers the most common breakage (Python service contract changes, deps drift). If you don't want to deal with Unity CI, the `unity` job can stay gated forever and you rely on running batchmode tests locally before pushing:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.4.1f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath "C:/Dev/Forever engine" \
  -executeMethod ForeverEngine.Tests.DialogueSmokeTest.Run -quit -logFile -
```

This is what I (the assistant) have been doing all session — works fine, just isn't automatic on push.
