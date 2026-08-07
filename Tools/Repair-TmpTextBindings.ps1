param(
    [string]$BaselineCommit = "2f013bd",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

function Get-YamlBlocks {
    param([string]$Content)

    return [regex]::Matches(
        $Content,
        '(?ms)^--- !u!(?<ClassId>\d+) &(?<FileId>-?\d+)(?<Stripped> stripped)?\r?\n(?<Body>.*?)(?=^--- !u!|\z)'
    )
}

function Get-BlockMap {
    param([string]$Content)

    $result = @{}
    foreach ($block in (Get-YamlBlocks $Content)) {
        $result[$block.Groups['FileId'].Value] = $block
    }

    return $result
}

function Get-GameObjectId {
    param([System.Text.RegularExpressions.Match]$Block)

    return [regex]::Match(
        $Block.Groups['Body'].Value,
        '(?m)^  m_GameObject: \{fileID: (?<Id>-?\d+)\}'
    ).Groups['Id'].Value
}

function Get-CurrentTmpByGameObject {
    param(
        [hashtable]$CurrentBlocks,
        [string]$GameObjectId
    )

    foreach ($entry in $CurrentBlocks.GetEnumerator()) {
        $block = $entry.Value
        if ($block.Groups['ClassId'].Value -ne '114') {
            continue
        }

        if ($block.Groups['Body'].Value -notmatch 'm_EditorClassIdentifier: Unity\.TextMeshPro::TMPro\.TextMeshProUGUI') {
            continue
        }

        if ((Get-GameObjectId $block) -eq $GameObjectId) {
            return $entry.Key
        }
    }

    return $null
}

function Get-GitContent {
    param(
        [string]$Commit,
        [string]$AssetPath
    )

    $text = git show "${Commit}:$AssetPath" 2>$null
    if ($LASTEXITCODE -ne 0) {
        return $null
    }

    return ($text -join "`n") + "`n"
}

function Get-AssetPathByGuid {
    param([string]$Guid)

    if ($script:GuidCache.ContainsKey($Guid)) {
        return $script:GuidCache[$Guid]
    }

    $match = rg --case-sensitive -l "^guid: $Guid$" Assets -g '*.meta' | Select-Object -First 1
    if (-not $match) {
        throw "Could not resolve prefab GUID $Guid"
    }

    $assetPath = $match.Substring(0, $match.Length - '.meta'.Length).Replace('\', '/')
    $script:GuidCache[$Guid] = $assetPath
    return $assetPath
}

function Get-CachedAsset {
    param([string]$AssetPath)

    if ($script:AssetCache.ContainsKey($AssetPath)) {
        return $script:AssetCache[$AssetPath]
    }

    $diskPath = Join-Path $script:Root $AssetPath
    if (-not (Test-Path -LiteralPath $diskPath)) {
        throw "Current asset does not exist: $AssetPath"
    }

    $oldContent = Get-GitContent $BaselineCommit $AssetPath
    if ($null -eq $oldContent) {
        throw "Baseline asset does not exist: $AssetPath"
    }

    $currentContent = [IO.File]::ReadAllText($diskPath)
    $asset = [pscustomobject]@{
        Path = $AssetPath
        DiskPath = $diskPath
        OldContent = $oldContent
        CurrentContent = $currentContent
        OldBlocks = Get-BlockMap $oldContent
        CurrentBlocks = Get-BlockMap $currentContent
    }
    $script:AssetCache[$AssetPath] = $asset
    return $asset
}

function Resolve-TextReference {
    param(
        [pscustomobject]$Asset,
        [string]$OldReferenceId,
        [ref]$PendingStrippedBlocks
    )

    if (-not $Asset.OldBlocks.ContainsKey($OldReferenceId)) {
        return $null
    }

    $oldTextBlock = $Asset.OldBlocks[$OldReferenceId]
    $oldBody = $oldTextBlock.Groups['Body'].Value
    $isLegacyText = $oldBody -match 'm_EditorClassIdentifier: UnityEngine\.UI::UnityEngine\.UI\.Text'
    $isStripped = $oldTextBlock.Groups['Stripped'].Success

    if (-not $isLegacyText -and -not $isStripped) {
        return $null
    }

    if (-not $isStripped) {
        if (-not $isLegacyText) {
            return $null
        }

        $gameObjectId = Get-GameObjectId $oldTextBlock
        return Get-CurrentTmpByGameObject $Asset.CurrentBlocks $gameObjectId
    }

    $sourceMatch = [regex]::Match(
        $oldBody,
        '(?m)^  m_CorrespondingSourceObject: \{fileID: (?<SourceId>-?\d+), guid: (?<Guid>[0-9a-f]+), type: 3\}'
    )
    if (-not $sourceMatch.Success) {
        return $null
    }

    $prefabInstanceId = [regex]::Match(
        $oldBody,
        '(?m)^  m_PrefabInstance: \{fileID: (?<Id>-?\d+)\}'
    ).Groups['Id'].Value
    $sourceId = $sourceMatch.Groups['SourceId'].Value
    $guid = $sourceMatch.Groups['Guid'].Value
    $prefabPath = Get-AssetPathByGuid $guid
    $prefab = Get-CachedAsset $prefabPath

    if (-not $prefab.OldBlocks.ContainsKey($sourceId)) {
        return $null
    }

    $sourceOldBlock = $prefab.OldBlocks[$sourceId]
    if ($sourceOldBlock.Groups['Body'].Value -notmatch 'm_EditorClassIdentifier: UnityEngine\.UI::UnityEngine\.UI\.Text') {
        return $null
    }

    $sourceGameObjectId = Get-GameObjectId $sourceOldBlock
    $newSourceId = Get-CurrentTmpByGameObject $prefab.CurrentBlocks $sourceGameObjectId
    if (-not $newSourceId) {
        throw "No TMP component found for $prefabPath source $sourceId"
    }

    foreach ($entry in $Asset.CurrentBlocks.GetEnumerator()) {
        $candidate = $entry.Value
        if (-not $candidate.Groups['Stripped'].Success) {
            continue
        }

        $body = $candidate.Groups['Body'].Value
        if ($body -match "m_CorrespondingSourceObject: \{fileID: $newSourceId, guid: $guid, type: 3\}" -and
            $body -match "m_PrefabInstance: \{fileID: $prefabInstanceId\}") {
            return $entry.Key
        }
    }

    if ($Asset.CurrentBlocks.ContainsKey($OldReferenceId)) {
        throw "Cannot reuse occupied fileID $OldReferenceId in $($Asset.Path)"
    }

    $block = @"
--- !u!114 &$OldReferenceId stripped
MonoBehaviour:
  m_CorrespondingSourceObject: {fileID: $newSourceId, guid: $guid, type: 3}
  m_PrefabInstance: {fileID: $prefabInstanceId}
  m_PrefabAsset: {fileID: 0}
"@
    $PendingStrippedBlocks.Value[$OldReferenceId] = $block
    return $OldReferenceId
}

$script:Root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$script:GuidCache = @{}
$script:AssetCache = @{}
$script:UnsupportedOverrides = @{}
$utf8 = [Text.UTF8Encoding]::new($false)

Push-Location $script:Root
try {
    $assetPaths = git -c core.quotepath=false ls-tree -r --name-only $BaselineCommit -- Assets |
        Where-Object { $_ -match '\.(prefab|unity)$' -and (Test-Path -LiteralPath (Join-Path $script:Root $_)) }

    $totalRepairs = 0
    $touchedAssets = 0
    foreach ($assetPath in $assetPaths) {
        $asset = Get-CachedAsset $assetPath
        $pendingBlocks = @{}
        $currentContent = $asset.CurrentContent
        $assetRepairs = 0

        foreach ($oldOwnerEntry in $asset.OldBlocks.GetEnumerator()) {
            $ownerId = $oldOwnerEntry.Key
            $oldOwner = $oldOwnerEntry.Value
            if ($oldOwner.Groups['ClassId'].Value -ne '114' -or
                -not $asset.CurrentBlocks.ContainsKey($ownerId)) {
                continue
            }

            $currentOwner = $asset.CurrentBlocks[$ownerId]
            $updatedOwnerText = $currentOwner.Value
            $oldReferenceLines = [regex]::Matches(
                $oldOwner.Groups['Body'].Value,
                '(?m)^  (?<Field>[A-Za-z_][A-Za-z0-9_]*): \{fileID: (?<ReferenceId>-?\d+)\}$'
            )

            foreach ($oldReferenceLine in $oldReferenceLines) {
                $field = $oldReferenceLine.Groups['Field'].Value
                $oldReferenceId = $oldReferenceLine.Groups['ReferenceId'].Value
                if ($oldReferenceId -eq '0') {
                    continue
                }

                $newReferenceId = Resolve-TextReference $asset $oldReferenceId ([ref]$pendingBlocks)
                if (-not $newReferenceId) {
                    continue
                }

                $currentFieldPattern = "(?m)^(  $([regex]::Escape($field)): \{fileID: )(?<Id>-?\d+)(?:\}|(?<Broken>0))$"
                $currentFieldMatch = [regex]::Match($updatedOwnerText, $currentFieldPattern)
                if (-not $currentFieldMatch.Success) {
                    continue
                }

                $currentReferenceId = $currentFieldMatch.Groups['Id'].Value
                if (-not $currentFieldMatch.Groups['Broken'].Success -and $currentReferenceId -eq $newReferenceId) {
                    continue
                }

                $replacementId = $newReferenceId
                $updatedOwnerText = [regex]::Replace(
                    $updatedOwnerText,
                    $currentFieldPattern,
                    { param($match) $match.Groups[1].Value + $replacementId + '}' },
                    1
                )
                $assetRepairs++
            }

            if (-not [string]::Equals($updatedOwnerText, $currentOwner.Value, [StringComparison]::Ordinal)) {
                $currentContent = $currentContent.Replace($currentOwner.Value, $updatedOwnerText)
            }
        }

        $overridePattern = '(?m)^(?<Prefix>\s*- target: \{fileID: )(?<SourceId>-?\d+)(?<Middle>, guid: (?<Guid>[0-9a-f]+), type: 3\}\r?\n\s+propertyPath: )(?<Property>[^\r\n]+)(?<ValueLine>\r?\n\s+value: (?<Value>[^\r\n]*))(?<ObjectLine>\r?\n\s+objectReference: \{fileID: [^\r\n]+)$'
        $overrideMatches = [regex]::Matches($currentContent, $overridePattern)
        foreach ($overrideMatch in $overrideMatches) {
            $sourceId = $overrideMatch.Groups['SourceId'].Value
            $guid = $overrideMatch.Groups['Guid'].Value
            $prefabPath = Get-AssetPathByGuid $guid
            $prefab = Get-CachedAsset $prefabPath

            if (-not $prefab.OldBlocks.ContainsKey($sourceId)) {
                continue
            }

            $oldSourceBlock = $prefab.OldBlocks[$sourceId]
            if ($oldSourceBlock.Groups['Body'].Value -notmatch 'm_EditorClassIdentifier: UnityEngine\.UI::UnityEngine\.UI\.Text') {
                continue
            }

            $sourceGameObjectId = Get-GameObjectId $oldSourceBlock
            $newSourceId = Get-CurrentTmpByGameObject $prefab.CurrentBlocks $sourceGameObjectId
            if (-not $newSourceId) {
                throw "No TMP component found for override target $prefabPath source $sourceId"
            }

            $property = $overrideMatch.Groups['Property'].Value
            if ($property -eq 'm_FontData.m_AlignByGeometry') {
                $currentContent = $currentContent.Replace($overrideMatch.Value + "`n", '')
                $assetRepairs++
                continue
            }

            switch -CaseSensitive ($property) {
                'm_Text' { $property = 'm_text' }
                'm_text' { $property = 'm_text' }
                'm_FontData.m_FontSize' { $property = 'm_fontSize' }
                'm_FontData.m_FontStyle' { $property = 'm_fontStyle' }
                'm_FontData.m_MinSize' { $property = 'm_fontSizeMin' }
                'm_FontData.m_LineSpacing' { $property = 'm_lineSpacing' }
                'm_RaycastTarget' { $property = 'm_RaycastTarget' }
                'm_Maskable' { $property = 'm_Maskable' }
                'm_Color.r' { $property = 'm_Color.r' }
                'm_Color.g' { $property = 'm_Color.g' }
                'm_Color.b' { $property = 'm_Color.b' }
                'm_Color.a' { $property = 'm_Color.a' }
                default {
                    $script:UnsupportedOverrides[$property] = $assetPath
                    continue
                }
            }

            $value = $overrideMatch.Groups['Value'].Value
            if ($overrideMatch.Groups['Property'].Value -eq 'm_FontData.m_LineSpacing') {
                if ($value -ne '1') {
                    throw "Unsupported legacy line spacing '$value' in $assetPath"
                }
                $value = '0'
            }

            $replacement = $overrideMatch.Groups['Prefix'].Value + $newSourceId +
                $overrideMatch.Groups['Middle'].Value + $property +
                ($overrideMatch.Groups['ValueLine'].Value -replace '(?<=value: ).*$', $value) +
                $overrideMatch.Groups['ObjectLine'].Value
            $currentContent = $currentContent.Replace($overrideMatch.Value, $replacement)
            $assetRepairs++
        }

        if ($pendingBlocks.Count -gt 0) {
            $currentContent = $currentContent.TrimEnd("`r", "`n") + "`n" +
                (($pendingBlocks.GetEnumerator() | Sort-Object Key | ForEach-Object { $_.Value.TrimEnd("`r", "`n") }) -join "`n") + "`n"
        }

        if ($assetRepairs -gt 0) {
            $touchedAssets++
            $totalRepairs += $assetRepairs
            Write-Output "$assetPath : $assetRepairs binding(s)"
            if ($Apply) {
                [IO.File]::WriteAllText($asset.DiskPath, $currentContent, $utf8)
            }
        }
    }

    if ($script:UnsupportedOverrides.Count -gt 0) {
        Write-Output 'Unsupported legacy Text overrides:'
        $script:UnsupportedOverrides.GetEnumerator() | Sort-Object Key | ForEach-Object {
            Write-Output "  $($_.Key) : $($_.Value)"
        }
        if ($Apply) {
            throw 'Refusing to apply with unsupported Text overrides.'
        }
    }

    Write-Output "TMP binding repair: assets=$touchedAssets bindings=$totalRepairs apply=$Apply"
}
finally {
    Pop-Location
}
