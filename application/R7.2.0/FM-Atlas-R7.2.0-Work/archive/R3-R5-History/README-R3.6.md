# FM Atlas R3.6 — Live Intelligence & Fitness Validation

This package carries forward the R3.5.1 Aurora Frost application source and adds the R3.6 evidence report for the latest read-only FM27 fitness investigation.

## Important status
This is an evidence-led validation milestone. The candidate memory values are NOT yet mapped to confirmed fitness labels. Do not treat this package as a final self-contained installer or as approval for bulk live imports.

## Build
From this folder in Windows PowerShell:

```powershell
dotnet restore .\FmAtlas.sln
dotnet build .\FmAtlas.sln -c Release
```

Run:

```powershell
dotnet run --project .\src\FmAtlas\FmAtlas.csproj -c Release
```

Framework-dependent single-file publish (requires .NET 8 Desktop Runtime):

```powershell
dotnet publish .\src\FmAtlas\FmAtlas.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\publish\win-x64
```

## R3.6 evidence
See `docs-R3.6/R3.6-Fitness-Validation-Report.md` and `docs-R3.6/Jayden-Danns-Before-After-Changed-Bytes.csv`.

## Safety
Live process access remains read-only. Do not add unverified fitness offsets to production player fields or enable bulk import on the strength of a single before/after comparison.
