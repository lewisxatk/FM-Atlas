# FM Atlas — Safe R7.2.0 GitHub Import Guide

This guide imports the supplied source checkpoints into a separate recovery branch. It does not replace the repository's `main` branch.

## Before starting

1. Download the previously supplied `FM-Atlas-GitHub-Recovery-Import-R7.2.0.zip` recovery bundle from the earlier ChatGPT message.
2. Keep the original R7.0.2, R7.1.0 and R7.2.0 ZIP archives unchanged.
3. Make a second copy of the recovery bundle to another folder or external drive if possible.
4. Extract the bundle fully to a simple folder, for example `C:\\FMAtlas-Recovery\\`. Do not run the script from inside the ZIP.
5. Open the extracted folder and confirm `IMPORT-TO-GITHUB.ps1` is present.

## Install/check Git

1. Open Windows PowerShell.
2. Run:

   ```powershell
   git --version
   ```

3. If PowerShell says Git is not recognised, install Git for Windows from https://git-scm.com/download/win, finish setup, close PowerShell and open it again.
4. Sign in to GitHub in the browser. When Git asks for authentication during clone/push, use the Git Credential Manager/browser sign-in flow. Do not paste a password or token into this chat.

## Run the import script

1. In File Explorer, open the extracted recovery folder.
2. Click the address bar, type `powershell`, and press Enter. This opens PowerShell in that folder.
3. Run:

   ```powershell
   .\\IMPORT-TO-GITHUB.ps1
   ```

4. If Windows blocks the script because of the execution policy, run this one-time process-scoped command in that same PowerShell window:

   ```powershell
   Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
   .\\IMPORT-TO-GITHUB.ps1
   ```

   This applies only to that PowerShell process; it does not permanently change the machine's execution policy.

5. Read the output. The script should clone `https://github.com/lewisxatk/FM-Atlas`, create a branch named similar to `recovery/r7.2.0-source-import-YYYYMMDD-HHMMSS`, copy the source/checkpoint material, commit and push that branch.
6. If GitHub asks you to authenticate, complete the browser sign-in. Do not close PowerShell while it is working.
7. When it finishes, copy the printed branch name and URL.

## Verify on GitHub

1. Open https://github.com/lewisxatk/FM-Atlas.
2. Use the branch selector (it normally displays `main`) and select the new `recovery/r7.2.0-source-import-...` branch.
3. Check that the R7.2.0 application source is present, including the solution/project files, source folders, assets and scripts.
4. Check that R7.1.0 and R7.0.2 historical backups and original archives are retained in the recovery area as described by the script.
5. Do not merge the branch into `main` yet. First send the branch name/URL and any script output back for a file-count and hash audit.

## If anything goes wrong

- Stop if the script reports an error, an unexpected branch, missing archive, authentication failure or push rejection.
- Copy the full PowerShell output and share it for diagnosis.
- Do not delete the cloned repository, original ZIPs or recovery bundle.
- Do not rerun the script repeatedly until the failure is understood.
- Never use `git push --force`, delete `main`, or overwrite a newer source folder to resolve an error.

## Keep these backups

Keep at least:
- Original R7.0.2 ZIP.
- Original R7.1.0 ZIP.
- Original R7.2.0 ZIP.
- Extracted recovery bundle.
- GitHub recovery branch after successful push.

GitHub is an additional versioned backup, not a reason to delete the original archives. Keep FM career saves, personal settings, passwords/tokens, licensed graphics and local build output out of the repository unless specifically required and safe.
