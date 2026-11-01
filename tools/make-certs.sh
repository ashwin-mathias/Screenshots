#!/usr/bin/env bash
# Creates Screenshot's two self-signed signing certificates (docs/SIGNING.md),
# stores them as GitHub Actions secrets, and leaves a backup folder for you to download.
#
# Run it once, in a GitHub Codespace on this repo:
#     unset GITHUB_TOKEN; gh auth login      # sign in as yourself (once)
#     bash tools/make-certs.sh
#
# --no-upload   create and check the files only; don't touch GitHub secrets (for testing)
set -euo pipefail

REPO="ashwin-mathias/Screenshots"
MAC_NAME="Screenshot Signing (Ashwin Mathias)"   # CI signs with this exact name
WIN_CN="Ashwin Mathias"                          # must match Publisher="CN=Ashwin Mathias" in AppxManifest
DAYS=3650                                        # 10 years
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/.signing-backup"
SECRETS=(MACOS_CERT_P12_BASE64 MACOS_CERT_PASSWORD WINDOWS_CERT_PFX_BASE64 WINDOWS_CERT_PASSWORD WINDOWS_CERT_THUMBPRINT)

UPLOAD=1
case "${1:-}" in
  "") ;;
  --no-upload) UPLOAD=0 ;;
  *) echo "Usage: bash tools/make-certs.sh [--no-upload]" >&2; exit 2 ;;
esac

say() { printf '\n\033[1m%s\033[0m\n' "$*"; }
die() { printf '\n\033[1mStopped:\033[0m %s\n' "$*" >&2; exit 1; }

# ---- 1. Checks before anything is created -------------------------------------------------
command -v openssl >/dev/null || die "openssl isn't installed."
command -v base64 >/dev/null || die "base64 isn't installed."
[ -e "$OUT" ] && die "$OUT already exists. Download what you need from it, delete it (rm -rf .signing-backup), then run this again."
(cd "$ROOT" && git check-ignore -q .signing-backup/x.p12) \
  || die ".signing-backup/ isn't ignored by git. Refusing to write private keys where they could be committed."

# The Codespace's built-in token can't write secrets, so gh must use your own login.
gh_user() { env -u GITHUB_TOKEN -u GH_TOKEN gh "$@"; }

if [ "$UPLOAD" = 1 ]; then
  command -v gh >/dev/null || die "The GitHub CLI (gh) isn't installed."
  gh_user auth status >/dev/null 2>&1 \
    || die "Sign in first:  unset GITHUB_TOKEN; gh auth login   (GitHub.com → HTTPS → browser). Then run this again."
  existing=$(gh_user secret list --repo "$REPO" --json name -q '.[].name' 2>/dev/null) \
    || die "Couldn't read the secrets of $REPO. Your gh login needs admin access to the repo."
  found=()
  for n in "${SECRETS[@]}"; do grep -qx "$n" <<<"$existing" && found+=("$n"); done
  if [ ${#found[@]} -gt 0 ]; then
    say "These secrets already exist: ${found[*]}"
    echo "Replacing them creates NEW certificates. Installed Windows apps must then be reinstalled,"
    echo "and Macs will ask for Screen Recording permission again (docs/SIGNING.md §5)."
    read -r -p "Type REPLACE to continue, anything else to stop: " answer
    [ "$answer" = "REPLACE" ] || die "Nothing was changed."
  fi
fi

umask 077
WORK=$(mktemp -d)
trap 'rm -rf "$WORK"' EXIT
mkdir -p "$OUT"

password() { openssl rand -base64 48 | tr -d '/+=\n' | cut -c1-32; }
MAC_PW=$(password)
WIN_PW=$(password)
export MAC_PW WIN_PW

# ---- 2. Create the certificates ------------------------------------------------------------
# A small config per certificate: an end-entity code-signing certificate, not a CA.
make_cert() { # $1 = file prefix, $2 = common name, $3 = RSA bits
  cat > "$WORK/$1.cnf" <<EOF
[req]
distinguished_name = dn
prompt = no
x509_extensions = ext
[dn]
CN = $2
[ext]
basicConstraints = critical, CA:FALSE
keyUsage = critical, digitalSignature
extendedKeyUsage = codeSigning
subjectKeyIdentifier = hash
EOF
  openssl req -x509 -new -newkey "rsa:$3" -sha256 -days "$DAYS" -nodes \
    -config "$WORK/$1.cnf" -keyout "$WORK/$1.key" -out "$WORK/$1.crt" 2>/dev/null
}

say "Creating the macOS certificate ($MAC_NAME)…"
make_cert mac "$MAC_NAME" 2048

say "Creating the Windows certificate (CN=$WIN_CN)…"
make_cert win "$WIN_CN" 3072

# ---- 3. Export in the format macOS and Windows both read ----------------------------------
# OpenSSL 3 encrypts .p12/.pfx with AES by default, which some macOS versions can't import.
# Use the classic 3DES / SHA-1 format instead; the strong random password protects the file.
LEGACY=(-keypbe PBE-SHA1-3DES -certpbe PBE-SHA1-3DES -macalg sha1)

openssl pkcs12 -export "${LEGACY[@]}" -name "$MAC_NAME" \
  -inkey "$WORK/mac.key" -in "$WORK/mac.crt" -passout env:MAC_PW \
  -out "$OUT/screenshot-mac-signing.p12"
openssl pkcs12 -export "${LEGACY[@]}" -name "Screenshot Signing (Ashwin Mathias, Windows)" \
  -inkey "$WORK/win.key" -in "$WORK/win.crt" -passout env:WIN_PW \
  -out "$OUT/screenshot-win-signing.pfx"
openssl x509 -in "$WORK/win.crt" -outform der -out "$OUT/Screenshot-signing.cer"

THUMB=$(openssl x509 -in "$WORK/win.crt" -noout -fingerprint -sha1 | cut -d= -f2 | tr -d ':' | tr 'a-f' 'A-F')

# ---- 4. Check our own output ----------------------------------------------------------------
say "Checking the files…"
check_bundle() { # $1 = file, $2 = password variable name, $3 = original cert
  local info
  info=$(openssl pkcs12 -in "$1" -info -noout -passin "env:$2" 2>&1) || die "Can't read back $1."
  grep -q "MAC: sha1" <<<"$info" || die "$1 doesn't use the SHA-1 MAC macOS expects."
  grep -q "Shrouded Keybag: pbeWithSHA1And3-KeyTripleDES-CBC" <<<"$info" || die "$1: key isn't in the 3DES format."
  grep -q "PKCS7 Encrypted data: pbeWithSHA1And3-KeyTripleDES-CBC" <<<"$info" || die "$1: certificate isn't in the 3DES format."
  local a b
  a=$(openssl pkcs12 -in "$1" -nokeys -clcerts -passin "env:$2" 2>/dev/null | openssl x509 -noout -fingerprint -sha256)
  b=$(openssl x509 -in "$3" -noout -fingerprint -sha256)
  [ "$a" = "$b" ] || die "$1 doesn't contain the certificate that was just made."
  # the private key inside must belong to the certificate
  local k c
  k=$(openssl pkcs12 -in "$1" -nocerts -nodes -passin "env:$2" 2>/dev/null | openssl pkey -pubout | openssl sha256)
  c=$(openssl x509 -in "$3" -noout -pubkey | openssl sha256)
  [ "$k" = "$c" ] || die "$1: the private key doesn't match the certificate."
}
check_bundle "$OUT/screenshot-mac-signing.p12" MAC_PW "$WORK/mac.crt"
check_bundle "$OUT/screenshot-win-signing.pfx" WIN_PW "$WORK/win.crt"
for f in mac win; do
  ext=$(openssl x509 -in "$WORK/$f.crt" -noout -ext extendedKeyUsage,basicConstraints 2>/dev/null)
  grep -q "Code Signing" <<<"$ext" || die "$f certificate is missing Code Signing."
  grep -q "CA:FALSE" <<<"$ext" || die "$f certificate should not be a CA."
done
[ "$(openssl x509 -in "$WORK/win.crt" -noout -subject -nameopt RFC2253)" = "subject=CN=$WIN_CN" ] \
  || die "Windows certificate subject must be exactly CN=$WIN_CN."
[[ "$THUMB" =~ ^[0-9A-F]{40}$ ]] || die "Couldn't work out the Windows thumbprint."
echo "Both files are in the classic format, hold the right certificate, and their keys match."

MAC_UNTIL=$(openssl x509 -in "$WORK/mac.crt" -noout -enddate | cut -d= -f2)
WIN_UNTIL=$(openssl x509 -in "$WORK/win.crt" -noout -enddate | cut -d= -f2)

cat > "$OUT/README.txt" <<EOF
Screenshot signing certificates — created $(date -u +%Y-%m-%d) by tools/make-certs.sh
Keep these files and their two passwords together in your password manager.
The passwords are NOT in this folder.

screenshot-mac-signing.p12   macOS certificate + private key   "$MAC_NAME"
                             valid until $MAC_UNTIL
screenshot-win-signing.pfx   Windows certificate + private key "CN=$WIN_CN"
                             valid until $WIN_UNTIL
                             thumbprint $THUMB
Screenshot-signing.cer       Windows public certificate (safe to share; PCs trust this one)

GitHub secrets in $REPO:
  MACOS_CERT_P12_BASE64, MACOS_CERT_PASSWORD,
  WINDOWS_CERT_PFX_BASE64, WINDOWS_CERT_PASSWORD, WINDOWS_CERT_THUMBPRINT
See docs/SIGNING.md for restoring, replacing and expiry.
EOF

# ---- 5. Store the GitHub secrets -------------------------------------------------------------
if [ "$UPLOAD" = 1 ]; then
  say "Saving the five GitHub secrets…"
  set_secret() { gh_user secret set "$1" --repo "$REPO" >/dev/null && echo "  ✓ $1"; }
  base64 -w0 "$OUT/screenshot-mac-signing.p12" | set_secret MACOS_CERT_P12_BASE64
  printf '%s' "$MAC_PW"                         | set_secret MACOS_CERT_PASSWORD
  base64 -w0 "$OUT/screenshot-win-signing.pfx" | set_secret WINDOWS_CERT_PFX_BASE64
  printf '%s' "$WIN_PW"                         | set_secret WINDOWS_CERT_PASSWORD
  printf '%s' "$THUMB"                          | set_secret WINDOWS_CERT_THUMBPRINT
else
  say "Skipped GitHub secrets (--no-upload)."
fi

# ---- 6. What to do now -----------------------------------------------------------------------
say "Done. Save these two passwords in your password manager now (they're shown only once):"
printf '\n  macOS  .p12 password:  %s\n  Windows .pfx password: %s\n' "$MAC_PW" "$WIN_PW"
cat <<EOF

Then:
  1. In the Explorer on the left, download every file in .signing-backup/
     (right-click → Download…) and add them to your password manager next to the passwords.
  2. Delete the folder here:   rm -rf .signing-backup
  3. Delete this Codespace (github.com/codespaces → … → Delete).
  4. On GitHub: Actions → "Check signing" → Run workflow. Both jobs must be green.
  5. On your Windows PC, trust Screenshot-signing.cer once (docs/SIGNING.md §3).
EOF
