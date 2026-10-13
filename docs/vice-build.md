# Build and export VICE updates

Use one build command for Core and any selection of the six implemented modules:

```powershell
./scripts/Build-VICE.ps1 -Modules Lifestyle
./scripts/Build-VICE.ps1 -AddOns YouTube
./scripts/Build-VICE.ps1 -Modules Lifestyle -AddOns YouTube
./scripts/Build-VICE.ps1 -Modules Commerce,Lifestyle
./scripts/Build-VICE.ps1 -Modules Enforcement,Syndicate
./scripts/Build-VICE.ps1 -All
./scripts/Build-VICE.ps1  # Core only
```

Core is mandatory and is compiled from the current workspace on every invocation. With no selection, the export contains only Core. `-All` includes Commerce, Arsenal, Lifestyle, Interface, Enforcement and Syndicate. Substances is reserved and cannot be selected yet. Selecting modules is case-insensitive; duplicates are removed. `-All` and `-Modules` cannot be combined.

`-AddOns YouTube` explicitly selects the optional development add-on. It builds and tests Lifestyle as a dependency but exports only Core and `VICE.YouTube.dll`; it requires an already installed Lifestyle 0.1.8 or newer. `-Modules Lifestyle -AddOns YouTube` exports both. `-All` does not select development add-ons. A plain Lifestyle export has no browser integration DLL, companion installer, YouTube guide or YouTube screenshots.

Each selected module keeps its relevant tests and native patch checks. Core's lifecycle, defaults and data-migration checks always run. Lifecycle metadata tests inspect only the selected ordinary module DLLs, so a Lifestyle-only build does not require prior builds of Commerce, Arsenal or Interface. Enforcement and Syndicate keep their existing beta signature/native-address checks.

Exports always use a fresh directory under `artifacts` (or `-OutputDirectory`). The command prints the directory and ZIP path. Old exported DLLs cannot carry into a smaller selection. The shared layout is:

```text
UserLibs/VICE.Core.dll
Mods/VICE.<selected-module>.dll
package.json                    # Selected modules and every DLL's version
hashes.json                     # SHA-256 for packaged files
Install-VICE.ps1
Install-VICEModules.ps1          # Shared installation implementation
Install-TVCompanion.ps1          # Only with -AddOns YouTube
docs/
UserData/VICE/Enforcement/       # Only if Enforcement is selected
UserData/VICE/Syndicate/         # Only if Syndicate is selected
Licenses/                       # Original notices for selected upstream ports
```

The export validator checks the exact DLL set, one Core in UserLibs, version metadata and every packaged file's hash. Installer fixtures check Core-only and selected-module installation, preservation of unselected DLLs/settings, missing-default seeding and rejection of corrupt packages before writes. Extra DLLs fail export validation.

Install while the game is closed:

```powershell
./scripts/Install-VICE.ps1 -PackageDir <printed-export-directory>
# Or build, check, export and install in one command:
./scripts/Build-VICE.ps1 -Modules Commerce,Lifestyle -Install
```

The installer defaults to the modules listed in `package.json`. Core is always updated. An explicit `-Modules` can install a subset of the exported modules. Unselected installed modules are preserved. Selected predecessors and replaced DLLs are backed up outside Mods; DLL failures roll back the previous installation. Existing module JSON data wins over packaged defaults. Original Cartel Enforcer must be removed before installing Syndicate.

Add-on selections are also listed in `package.json` and can be limited with `-AddOns`. YouTube installation checks the selected or installed base DLL's version before writing any DLLs. Installing a base-only update preserves an already installed add-on. To disable the feature, close the game and remove `Mods/VICE.YouTube.dll`; the companion can then be removed independently. Saved selections remain compatible.

The older build commands are compatibility wrappers: `Build-VICELifestyle.ps1`, `Build-VICEEnforcement.ps1`, `Build-VICESyndicate.ps1` select one module; `Build-VICEModules.ps1` selects the four ordinary modules. They share compilation, Core inclusion, checks and packaging through `Build-VICE.ps1`. `Package-VICE.ps1` is the internal exporter for already-built binaries; use the build command for updates so Core is freshly built.

Supported overrides are `-GameDir`, `-DotnetPath`, `-NugetConfig`, `-OutputDirectory` and `-OriginalMoreGuns`. Arsenal still uses the owner's local authorized asset bundle. Builds reference the installed IL2CPP game, MelonLoader and S1API assemblies without downloading them.
