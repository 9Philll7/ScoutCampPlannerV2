param(
    [Parameter(Mandatory = $true)]
    [string] $Source,

    [Parameter(Mandatory = $true)]
    [string] $Destination
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$destinationDirectory = Split-Path -Parent $Destination
New-Item -ItemType Directory -Force -Path $destinationDirectory | Out-Null

$sourceImage = [System.Drawing.Image]::FromFile($Source)
try {
    $bitmap = [System.Drawing.Bitmap]::new($sourceImage, 256, 256)
    try {
        $pngStream = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
            $pngBytes = $pngStream.ToArray()
        }
        finally {
            $pngStream.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}
finally {
    $sourceImage.Dispose()
}

$output = [System.IO.File]::Create($Destination)
try {
    $writer = [System.IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]1)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$pngBytes.Length)
        $writer.Write([uint32]22)
        $writer.Write($pngBytes)
    }
    finally {
        $writer.Dispose()
    }
}
finally {
    $output.Dispose()
}

Write-Host "Prepared Tauri icon: $Destination"
