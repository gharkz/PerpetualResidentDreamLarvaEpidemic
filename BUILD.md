# Building

Requires the .NET SDK and an installed copy of Elin. Set ELIN_DIR to your Elin folder, then run:

```powershell
dotnet build -c Release
dotnet run --project tests/PerpetualResidentDreamLarvaEpidemicChecks.csproj -c Release
```

Copy the DLL, package.xml, and preview.jpg from dist/PerpetualResidentDreamLarvaEpidemic into Elin/Package/PerpetualResidentDreamLarvaEpidemic.

The package version is Elin compatibility (0.23.355). The mod release is 0.3.2.
