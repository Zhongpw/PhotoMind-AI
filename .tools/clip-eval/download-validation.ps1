# Downloads a small real-image validation set from Wikimedia Commons (search by keyword).
# Ground truth = the search keyword's mapped category. Thumbnails at ~400px (CLIP resizes to 224).
$ErrorActionPreference = "Stop"
$outRoot = Join-Path $PSScriptRoot "validation"

$categories = @{
    "nature"        = @("mountain landscape", "green forest", "ocean beach")
    "everyday life" = @("family dinner", "birthday party", "travel suitcase")
    "animals"       = @("house cat", "dog", "bird")
    "architecture"  = @("city skyline", "building facade", "famous landmark")
    "food"          = @("pizza", "sushi", "chocolate cake")
}

$total = 0
foreach ($cat in $categories.Keys) {
    $catDir = Join-Path $outRoot $cat
    New-Item -ItemType Directory -Force -Path $catDir | Out-Null
    $idx = 0
    foreach ($kw in $categories[$cat]) {
        $idx++
        $enc = [Uri]::EscapeDataString($kw)
        $api = "https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch=$enc&gsrnamespace=6&gsrlimit=3&prop=imageinfo&iiprop=url&iiurlwidth=400&format=json"
        try {
            $r = Invoke-RestMethod -Uri $api -TimeoutSec 45
            $page = $r.query.pages.PSObject.Properties.Value | Where-Object { $_.imageinfo } | Select-Object -First 1
            if (-not $page) { Write-Output "NOIMG  $cat / $kw"; continue }
            $url = $page.imageinfo[0].thumburl
            if (-not $url) { Write-Output "NOURL  $cat / $kw"; continue }
            $dest = Join-Path $catDir ("{0:D2}_{1}.jpg" -f $idx, ($kw -replace '[^a-zA-Z0-9]+','_'))
            Invoke-WebRequest -Uri $url -OutFile $dest -TimeoutSec 60
            $size = (Get-Item $dest).Length
            Write-Output ("OK     {0,-14} {1,-24} {2} bytes" -f $cat, $kw, $size)
            $total++
        } catch {
            Write-Output ("ERR    {0,-14} {1,-24} {2}" -f $cat, $kw, $_.Exception.Message)
        }
    }
}
Write-Output "DONE total=$total"
