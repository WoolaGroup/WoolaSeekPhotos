# Woola Photos — Setup Script
# Generates secure random credentials and JWT key

$configPath = "D:\Empresas\WOOLA\Sistemas\6-WoolaPhotos\src\Woola.PhotoManager.Backend.WebApi\appsettings.json"
$config = Get-Content $configPath -Raw | ConvertFrom-Json

# Generate secure random password (16 chars)
$password = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 16 | ForEach-Object { [char]$_ })
# Generate secure JWT key (32 chars)
$jwtKey = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | ForEach-Object { [char]$_ })

Write-Host "Generated secure credentials:"
Write-Host "  Password: $password"
Write-Host "  JWT Key:  $jwtKey"
Write-Host ""

$config.Auth.DefaultPassword = $password
$config.Jwt.Key = $jwtKey

$config | ConvertTo-Json -Depth 10 | Set-Content $configPath

Write-Host "appsettings.json updated with secure credentials."
Write-Host ""
Write-Host "Login credentials:"
Write-Host "  Username: admin"
Write-Host "  Password: $password"
