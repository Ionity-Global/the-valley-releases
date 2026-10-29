# THE VALLEY - upload client pieces to a GitHub release with curl (watchdog: a transfer that goes silent is killed and retried).
# Usage: powershell -File gh_upload_curl.ps1 -Src <folder> -Tag v4.14.1
# v4.15.1c: curl is called directly (Start-Process split the "Authorization: token ..." header at its spaces); HTTP code + body logged.
# (c) Ionity (Pty) Ltd | Johan Wilhelm van Antwerp
param([string]$Src, [string]$Tag, [string]$Repo = 'Ionity-Global/the-valley-releases')
$log = Join-Path $Src 'gh_upload.log'
function Say($s) { $l = "$(Get-Date -Format HH:mm:ss) $s"; $l; $l | Add-Content $log }
$token = (gh auth token).Trim()
$rel = gh api "repos/$Repo/releases/tags/$Tag" | ConvertFrom-Json
$relId = $rel.id
function Assets { (gh api "repos/$Repo/releases/$relId/assets?per_page=100" | ConvertFrom-Json) }
Say "curl uploader: $Tag (release $relId)"
$pieces = Get-ChildItem $Src -Filter '*_Client.zip.0??' | Sort Name
foreach ($p in $pieces) {
  for ($try = 1; $try -le 8; $try++) {
    $have = Assets | ? { $_.name -eq $p.Name }
    if ($have -and $have.size -eq $p.Length -and $have.state -eq 'uploaded') { Say "ok $($p.Name) (already)"; break }
    if ($have) { Say "removing partial $($p.Name) ($($have.size) $($have.state))"; gh api -X DELETE "repos/$Repo/releases/assets/$($have.id)" | Out-Null }
    Say "upload $($p.Name) ($([int]($p.Length/1MB)) MB) try $try"
    $url = "https://uploads.github.com/repos/$Repo/releases/$relId/assets?name=$($p.Name)"
    $out = Join-Path $Src "ghup_last.json"
    if (Test-Path $out) { Remove-Item $out -Force }
    $code = & curl.exe -sS -X POST -H "Authorization: token $token" -H "Content-Type: application/octet-stream" -w "%{http_code}" --speed-limit 20000 --speed-time 90 --max-time 7200 -T $p.FullName -o $out $url 2>&1
    $ec = $LASTEXITCODE
    $body = if (Test-Path $out) { Get-Content $out -Raw } else { "" }
    if ($ec -eq 0 -and $body -match '"state":\s*"uploaded"') { Say "ok $($p.Name) (HTTP $code)"; break }
    if ($body.Length -gt 300) { $body = $body.Substring(0, 300) }
    Say "FAILED $($p.Name) (curl exit $ec, HTTP $code) $body - retrying"; Start-Sleep (10 * $try)
  }
}
$n = (Assets | ? { $_.name -like '*_Client.zip.0??' -and $_.state -eq 'uploaded' }).Count
Say "done: $n of $($pieces.Count) pieces on GitHub"
