param(
    [Parameter(Mandatory = $true)][string]$Connection
)

# Regenerates entities and PixelPayDbContext from a MySQL that has the Prisma migrations applied.
# Prisma owns the schema: never add EF migrations. Hand edits live in *.Partial.cs / Enums / OnModelCreatingPartial.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$tables = 'users', 'wallets', 'wallet_transactions', 'categories', 'games', 'game_products', 'providers',
    'provider_products', 'orders', 'payments', 'coupons', 'coupon_usages', 'cashback_rules', 'affiliates',
    'affiliate_commissions', 'notifications', 'audit_logs', 'resellers'

# Prisma enum columns scaffold as string; JSON columns scaffold as string.
$enumProps = @{
    'User.Role'                      = 'UserRole'
    'WalletTransaction.Type'         = 'WalletTransactionType'
    'GameProduct.ProductType'        = 'ProductType'
    'Order.PaymentMethod'            = 'PaymentMethod'
    'Order.Status'                   = 'OrderStatus'
    'Payment.PaymentMethod'          = 'PaymentMethod'
    'Payment.Status'                 = 'PaymentStatus'
    'Coupon.DiscountType'            = 'DiscountType'
    'CashbackRule.CashbackType'      = 'CashbackType'
    'Affiliate.Status'               = 'AffiliateStatus'
    'AffiliateCommission.Status'     = 'CommissionStatus'
    'Notification.Channel'           = 'NotificationChannel'
    'Reseller.Status'                = 'ResellerStatus'
}
$jsonProps = 'WalletTransaction.Metadata', 'Game.Metadata', 'GameProduct.Metadata', 'Provider.Metadata',
    'Order.Metadata', 'Notification.Metadata', 'AuditLog.OldValues', 'AuditLog.NewValues'

$args = @(
    'ef', 'dbcontext', 'scaffold', $Connection, 'Pomelo.EntityFrameworkCore.MySql',
    '--project', 'src/PixelPay.Infrastructure',
    '--context', 'PixelPayDbContext',
    '--context-dir', 'Persistence',
    '--output-dir', '../PixelPay.Domain/Entities',
    '--namespace', 'PixelPay.Domain.Entities',
    '--context-namespace', 'PixelPay.Infrastructure.Persistence',
    '--no-onconfiguring', '--force'
)
foreach ($t in $tables) { $args += '--table'; $args += $t }

$env:DOTNET_ROLL_FORWARD = 'Major'
& dotnet @args
if ($LASTEXITCODE -ne 0) { throw 'dotnet ef scaffold failed' }

$entityDir = Join-Path $root 'src/PixelPay.Domain/Entities'
foreach ($file in Get-ChildItem $entityDir -Filter *.cs) {
    $entity = $file.BaseName
    $text = Get-Content $file.FullName -Raw
    foreach ($key in $enumProps.Keys) {
        $e, $p = $key.Split('.')
        if ($e -ne $entity) { continue }
        $text = $text -replace "public string $p \{ get; set; \} = null!;", "public $($enumProps[$key]) $p { get; set; }"
    }
    foreach ($key in $jsonProps) {
        $e, $p = $key.Split('.')
        if ($e -ne $entity) { continue }
        $text = $text -replace "public string\? $p \{ get; set; \}", "public RawJson? $p { get; set; }"
    }
    # Columns with a default constraint scaffold as bool?; Prisma always writes them.
    $text = $text -replace 'public bool\? (\w+) \{', 'public bool $1 {'
    $text = "using PixelPay.Domain.Common;`r`nusing PixelPay.Domain.Enums;`r`n" + $text
    Set-Content $file.FullName $text -NoNewline -Encoding utf8
}
