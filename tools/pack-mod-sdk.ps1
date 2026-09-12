# 打包 mod 全部接口包到 artifacts/nuget 本地源目录，供 mod 工程把该目录登记为自己的 NuGet 源后还原。
# 在仓库根执行：./tools/pack-mod-sdk.ps1
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot 'artifacts/nuget'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# 两个引用锚点 + 传递引用闭包。全部 IsPackable=true 的项目都产包，
# 锚点包把闭包声明为版本一致的依赖，缺一包则消费方还原失败。
$projects = @(
    'DungeonChessBattle.Battle.Mod.Interface',
    'DungeonChessBattle.Battle.Mod.Shared',
    'DungeonChessBattle.Battle.Shared',
    'DungeonChessBattle.Game.Mod.Interface',
    'DungeonChessBattle.Game.Mod.Shared',
    'DungeonChessBattle.Game.Shared'
)

foreach ($project in $projects) {
    $csproj = Join-Path $repoRoot "$project/$project.csproj"
    dotnet pack $csproj -c Release -o $outDir
    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet pack 失败：$project"
        exit $LASTEXITCODE
    }
}

Write-Host "mod 接口包已生成：$outDir"
