# Screenshot — Signing certificates

How to create the two free, self-signed certificates that CI uses to sign the apps (ARCHITECTURE §8). You do this **once**, on your own computers.

- **Never paste a private key, `.p12`, `.pfx` or password into a chat, an issue or a commit.** It goes only into GitHub's secret settings and your password manager.
- Secrets live in GitHub → **Settings → Secrets and variables → Actions → New repository secret**.
- In a public repo, secrets are never shown in logs and aren't given to pull requests from forks.

---

## 1. macOS certificate (on the Mac, about 10 minutes)

### Create it
1. Open **Keychain Access**. From the menu bar choose **Keychain Access → Certificate Assistant → Create a Certificate…**
2. Fill in:
   - **Name:** `Screenshot Signing (Ashwin Mathias)` (exactly; CI looks for this name)
   - **Identity Type:** Self-Signed Root
   - **Certificate Type:** Code Signing
   - Tick **Let me override defaults**, then click **Create** / **Continue**.
3. **Validity Period (days):** `3650` (10 years). Continue.
4. Email and name screens: fill in your name, leave the rest as is. Continue through the key screens and keep the defaults (RSA, 2048 bits; Code Signing extensions are already ticked).
5. **Keychain:** login. Click **Create**, then **Done**.

### Export it
6. In Keychain Access, click **login** → **My Certificates**. Find `Screenshot Signing (Ashwin Mathias)` (it has a ▸ with a private key inside).
7. Right-click it → **Export…** → format **Personal Information Exchange (.p12)** → save as `screenshot-mac-signing.p12` on the Desktop.
8. Choose a strong password when asked. Your Mac password may also be asked, so Keychain can release the key.

### Add it to GitHub
9. In Terminal:
   ```sh
   base64 -i ~/Desktop/screenshot-mac-signing.p12 | pbcopy
   ```
   The certificate is now on your clipboard as text.
10. On GitHub, add two repository secrets:

    | Name | Value |
    |---|---|
    | `MACOS_CERT_P12_BASE64` | paste (⌘V) |
    | `MACOS_CERT_PASSWORD` | the .p12 password from step 8 |

### Back it up, then tidy up
11. Save `screenshot-mac-signing.p12` and its password in your password manager (for example as a 1Password document) or another safe offline place.
12. Delete the copy on the Desktop and empty the Bin. The certificate stays in your keychain; that's fine.

---

## 2. Windows certificate (on the Windows PC, about 10 minutes)

### Create and export it
1. Open **PowerShell** (normal, not admin): Start → type `PowerShell` → Enter.
2. Paste these lines one block at a time:

   ```powershell
   # Create the certificate (10 years). The subject must be exactly CN=Ashwin Mathias.
   $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Ashwin Mathias" `
     -FriendlyName "Screenshot Signing (Ashwin Mathias)" -KeyUsage DigitalSignature `
     -KeyAlgorithm RSA -KeyLength 3072 -HashAlgorithm SHA256 `
     -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(10) `
     -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
   ```

   ```powershell
   # Export the private copy (.pfx) — you'll be asked to choose a password.
   $pw = Read-Host -AsSecureString "Choose a password for the .pfx"
   Export-PfxCertificate -Cert $cert -FilePath "$HOME\Desktop\screenshot-win-signing.pfx" -Password $pw
   # Export the public copy (.cer) — safe to share; it's what PCs trust.
   Export-Certificate -Cert $cert -FilePath "$HOME\Desktop\Screenshot-signing.cer"
   # Show the thumbprint (the certificate's fingerprint).
   $cert.Thumbprint
   ```

3. Copy the thumbprint that's printed (40 letters and digits).

### Add it to GitHub
4. Put the .pfx on the clipboard as text:
   ```powershell
   [Convert]::ToBase64String([IO.File]::ReadAllBytes("$HOME\Desktop\screenshot-win-signing.pfx")) | Set-Clipboard
   ```
5. On GitHub, add three repository secrets:

   | Name | Value |
   |---|---|
   | `WINDOWS_CERT_PFX_BASE64` | paste (Ctrl+V) |
   | `WINDOWS_CERT_PASSWORD` | the .pfx password from step 2 |
   | `WINDOWS_CERT_THUMBPRINT` | the thumbprint from step 3 |

### Trust it on this PC (once per PC)
6. Open **PowerShell as administrator** (Start → type `PowerShell` → **Run as administrator**) and run:
   ```powershell
   Import-Certificate -FilePath "$env:USERPROFILE\Desktop\Screenshot-signing.cer" -CertStoreLocation Cert:\LocalMachine\TrustedPeople
   ```
   Other PCs do the same with `Screenshot-signing.cer` from the release page (README has the click-through version).

### Back it up, then tidy up
7. Save `screenshot-win-signing.pfx` and its password in your password manager.
8. Delete the `.pfx` from the Desktop and empty the Recycle Bin. You can keep the `.cer`; it's public.

---

## 3. Check

- GitHub → Settings → Secrets and variables → Actions shows **five** secrets:
  - `MACOS_CERT_P12_BASE64`
  - `MACOS_CERT_PASSWORD`
  - `WINDOWS_CERT_PFX_BASE64`
  - `WINDOWS_CERT_PASSWORD`
  - `WINDOWS_CERT_THUMBPRINT`
- The first CI run on `main` signs both apps. If a secret is missing, the run stops with an error naming it.

## 4. If a certificate is lost, leaked or expires

| | What happens | What to do |
|---|---|---|
| **macOS** | Updates fail the certificate check (UPD-004); macOS asks for Screen Recording permission again | Make a new certificate (same name), update the two secrets, release, and install that release by hand once on each Mac |
| **Windows** | A new certificate is a new publisher: Windows treats the app as a different one | Make a new certificate (same subject), update the three secrets, release; on each PC uninstall Screenshot, trust the new `.cer` and install the new `.msix` |
| **Leaked** (either) | Someone could sign a fake update | Replace it at once as above, and delete the old certificate from your keychain or certificate store |

- **Expiry:** both certificates last 10 years, until November 2036. Put a reminder in your calendar for mid-2036.
