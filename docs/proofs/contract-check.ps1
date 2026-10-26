param([string]$Email,[string]$Password,[string]$StokvelId,
      [string]$Api='http://localhost:5051',[string]$Origin='http://localhost:3000')

$base = "$Api/api/v1"
$login = Invoke-RestMethod -Method Post -Uri "$base/auth/login" -ContentType 'application/json' `
  -Body (@{ email=$Email; password=$Password } | ConvertTo-Json)
$token = $login.accessToken
$auth = @{ Authorization = "Bearer $token" }

$home_ = Invoke-RestMethod -Uri "$base/me/home" -Headers $auth
$cycleId = ($home_.stokvels | Where-Object { $_.stokvelId -eq $StokvelId }).currentCycle.id
if (-not $cycleId) { throw 'No open cycle found for that stokvel.' }

$members = (Invoke-RestMethod -Uri "$base/stokvels/$StokvelId/members?pageSize=100" -Headers $auth).items
$paid = (Invoke-RestMethod -Uri "$base/stokvels/$StokvelId/cycles/$cycleId/contributions?pageSize=100" -Headers $auth).items |
  ForEach-Object { $_.userId }
$free = @($members | Where-Object { $paid -notcontains $_.userId })
if ($free.Count -lt 2) { throw 'Need two members who have not contributed this cycle yet.' }
$A = $free[0].userId; $B = $free[1].userId

$url = "$base/stokvels/$StokvelId/contributions"
$tmp = Join-Path $env:TEMP 'contrib.json'
$script:last = ''

function Send([string]$label, [string]$member, [long]$cents, [string]$key, [bool]$quiet=$false) {
  $json = @{ userId=$member; contributionCycleId=$cycleId; amountInCents=$cents } | ConvertTo-Json -Compress
  [IO.File]::WriteAllText($tmp, $json, (New-Object Text.UTF8Encoding($false)))
  $a = @('-s','-i','-X','POST',$url,'-H',"Authorization: Bearer $token",
         '-H','Content-Type: application/json','-H',"Origin: $Origin",'--data-binary',"@$tmp")
  if ($key) { $a += @('-H',"Idempotency-Key: $key") }
  $script:last = (& curl.exe @a) -join "`n"
  if (-not $quiet) { "=== $label ==="; "request body: $json"; $script:last; "" }
}
function Status { if ($script:last -match 'HTTP/\S+ (\d+)') { [int]$Matches[1] } else { 0 } }

$K1 = [guid]::NewGuid().ToString()
Send '1a. First send (key K1, member A, 15050 cents)' $A 15050 $K1
Send '1b. SAME key K1, same body (replay)' $A 15050 $K1
Send '2.  SAME key K1, DIFFERENT amount (20000)' $A 20000 $K1
Send '3.  NEW key, member A already contributed' $A 15050 ([guid]::NewGuid().ToString())

# 4. Use up the rate limit with keyless requests (400 each, but they spend tokens), then retry one key.
$K4 = [guid]::NewGuid().ToString()
$n = 0
while ($n -lt 60) { Send 'drain' $B 15050 '' $true; $n++; if ((Status) -eq 429) { break } }
"=== 4. Rate limit reached after $n keyless requests ==="; ""
Send '4a. Key K4 while limited (member B)' $B 15050 $K4
$wait = 10
if ($script:last -match '(?im)^Retry-After:\s*(\d+)') { $wait = [int]$Matches[1] }
"(waiting $($wait + 1) seconds as Retry-After said)"; ""
Start-Sleep -Seconds ($wait + 1)
Send '4b. SAME key K4 after the wait' $B 15050 $K4
Send '4c. SAME key K4 once more (should replay, not a second payment)' $B 15050 $K4

Send '5.  No Idempotency-Key at all' $B 15050 ''

"=== 6. CORS preflight from $Origin ==="
(& curl.exe -s -i -X OPTIONS $url -H "Origin: $Origin" -H 'Access-Control-Request-Method: POST' `
  -H 'Access-Control-Request-Headers: idempotency-key,content-type,authorization') -join "`n"
"";
"=== OpenAPI: is the header described? ==="
(Invoke-RestMethod -Uri "$Api/openapi/v1.json").paths.'/api/v1/stokvels/{id}/contributions'.post.parameters |
  Where-Object { $_.name -eq 'Idempotency-Key' } | ConvertTo-Json -Compress