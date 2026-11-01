# Screenshot — Signing certificates

How to create the two free, self-signed certificates that CI uses to sign the apps (ARCHITECTURE §8). You do this **once**.

- **Never paste a private key, `.p12`, `.pfx` or password into a chat, an issue or a commit.** It goes only into GitHub's secret settings and your password manager.
- In a public repo, secrets are never shown in logs and aren't given to pull requests from forks.

The recommended way is **§1, a Codespace**: one script creates both certificates, checks them and saves the five secrets. **§2** is the manual fallback on your own Mac and PC. Either way, finish with §3 and §4.

---

## 1. Recommended: one script in a Codespace (about 10 minutes)

1. On GitHub, open the repo → **Code → Codespaces → Create codespace on main**. Wait for the editor and terminal to open.
2. In the terminal, get the latest files and sign in to GitHub **as yourself**. The Codespace's built-in login can't save secrets.
   ```sh
   git pull
   unset GITHUB_TOKEN
   gh auth login
   ```
   Choose **GitHub.com → HTTPS → Login with a web browser**, copy the code, and approve it in the browser tab.
3. Run the script:
   ```sh
   bash tools/make-certs.sh
   ```
   It:
   - creates the macOS certificate `Screenshot Signing (Ashwin Mathias)` and the Windows certificate `CN=Ashwin Mathias`, both valid 10 years;
   - exports them in the classic 3DES format every macOS and Windows version can read, protected by long random passwords;
   - **checks its own files**: format, certificate inside, and that each key matches its certificate;
   - saves the five secrets: `MACOS_CERT_P12_BASE64`, `MACOS_CERT_PASSWORD`, `WINDOWS_CERT_PFX_BASE64`, `WINDOWS_CERT_PASSWORD`, `WINDOWS_CERT_THUMBPRINT`;
   - puts a backup in `.signing-backup/`, a folder git ignores so it can't be committed.

   If the secrets already exist, it stops and asks before replacing them.
4. **Save the two passwords it prints** in your password manager. They're shown only once.
5. In the Explorer on the left, right-click each file in `.signing-backup/` → **Download…**. Add them to your password manager next to the passwords:
   - `screenshot-mac-signing.p12`
   - `screenshot-win-signing.pfx`
   - `Screenshot-signing.cer`
   - `README.txt`
6. Clean up:
   ```sh
   rm -rf .signing-backup
   ```
   Then delete the Codespace: github.com/codespaces → **…** → **Delete**.

Then go to §3 and §4.

---

## 2. Fallback: by hand on your Mac and PC

Use this only if the Codespace route isn't possible.

### 2a. macOS (on the Mac)
1. Open **Keychain Access**. Go to **Certificate Assistant → Create a Certificate…** and fill in:
   - Name `Screenshot Signing (Ashwin Mathias)`, exactly;
   - Identity Type **Self-Signed Root**;
   - Certificate Type **Code Signing**;
   - tick **Let me override defaults**;
   - validity `3650` days;
   - keep the other defaults; keychain **login**.
2. Go to **login → My Certificates**, right-click the certificate → **Export…** → **.p12**. Choose a strong password.
3. Copy it as text: `base64 -i ~/Desktop/screenshot-mac-signing.p12 | pbcopy`. Add the secrets `MACOS_CERT_P12_BASE64` (paste) and `MACOS_CERT_PASSWORD`.

### 2b. Windows (on the PC, PowerShell)
```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Ashwin Mathias" `
  -FriendlyName "Screenshot Signing (Ashwin Mathias)" -KeyUsage DigitalSignature `
  -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
  -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(10) `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$pw = Read-Host -AsSecureString "Choose a password for the .pfx"
Export-PfxCertificate -Cert $cert -FilePath "$HOME\Desktop\screenshot-win-signing.pfx" -Password $pw
Export-Certificate -Cert $cert -FilePath "$HOME\Desktop\Screenshot-signing.cer"
$cert.Thumbprint
[Convert]::ToBase64String([IO.File]::ReadAllBytes("$HOME\Desktop\screenshot-win-signing.pfx")) | Set-Clipboard
```
- Add the secrets:
  - `WINDOWS_CERT_PFX_BASE64` (paste);
  - `WINDOWS_CERT_PASSWORD`;
  - `WINDOWS_CERT_THUMBPRINT`, the 40-character value printed above.
- Secrets live in GitHub → **Settings → Secrets and variables → Actions → New repository secret**.
- Back up the `.p12`, `.pfx` and their passwords in your password manager, then delete the Desktop copies.

---

## 3. Trust the Windows certificate on your PC (once per PC)

Windows installs the app only from a publisher it trusts. Use the `Screenshot-signing.cer` you downloaded. Later PCs can get it from any release page.

- **Click-through:** double-click `Screenshot-signing.cer` → **Install Certificate…** → **Local Machine** → **Place all certificates in the following store** → **Browse** → **Trusted People** → **Finish**.
- **Or PowerShell as administrator:**
  ```powershell
  Import-Certificate -FilePath "$env:USERPROFILE\Downloads\Screenshot-signing.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
  ```

Macs need nothing now. The first time you open the app, allow it once under **System Settings → Privacy & Security → Open Anyway** (README).

## 4. Check that signing works

1. On GitHub: **Actions → Check signing → Run workflow** (branch `main`).
2. Two jobs run on fresh machines:
   - **macos:** imports the `.p12` into a temporary keychain, signs a test program as `Screenshot Signing (Ashwin Mathias)` and verifies it.
   - **windows:** opens the `.pfx`, checks the subject is `CN=Ashwin Mathias`, the thumbprint matches the secret and it's a code-signing certificate, then signs and verifies a test program.
3. **Both green = done.** If one fails, its error says which secret or step is wrong. Nothing has been released yet, so re-running `tools/make-certs.sh` (answer `REPLACE`) costs nothing.

## 5. If a certificate is lost, leaked or expires

| | What happens | What to do |
|---|---|---|
| **macOS** | Updates fail the certificate check (UPD-004); macOS asks for Screen Recording permission again | Make a new certificate with the same name, update the secrets, release, and install that release by hand once on each Mac |
| **Windows** | A new certificate is a new publisher, so Windows treats the app as a different one | Make a new certificate with the same subject, update the secrets, release; on each PC uninstall Screenshot, trust the new `.cer` and install the new `.msix` |
| **Leaked** (either) | Someone could sign a fake update | Replace it at once as above |
| **Restore from backup** | — | Add the backed-up `.p12` / `.pfx` (as base64) and their passwords as secrets again; nothing changes for installed apps |

- **Expiry:** both certificates last 10 years, until late October 2036. Put a reminder in your calendar for mid-2036.
