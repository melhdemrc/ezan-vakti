# Ezan Vakti - Build & Release Script

## 🚀 Build Instructions

### 1. Build Release Version
```powershell
# Clean previous builds
dotnet clean --configuration Release

# Restore packages
dotnet restore

# Build release (self-contained, single file)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

### 2. Output Location
- **Executable**: `bin\Release\net6.0-windows10.0.19041.0\win-x64\publish\EzanVakti.exe`
- **Size**: ~70-90 MB (self-contained with .NET runtime)

---

## 📦 Create Installer

### Option 1: Using Inno Setup (Recommended)

1. **Download Inno Setup**: https://jrsoftware.org/isdl.php
2. **Install** Inno Setup 6.x
3. **Open** `installer.iss` in Inno Setup Compiler
4. **Click** "Compile" (or press F9)
5. **Installer Output**: `installer\EzanVakti-Setup-v1.0.0.exe`

### Option 2: Portable (No Installer)

Just distribute the single `.exe` file from publish folder:
```
EzanVakti.exe
```
Users can run it directly without installation.

---

## 📝 Features

✅ **Single-file executable** - No dependencies needed  
✅ **Self-contained** - Includes .NET 6 runtime  
✅ **Windows 11 optimized** - Taskbar integration  
✅ **System tray** - Minimizes to notification area  
✅ **Auto-startup option** - Runs on Windows startup (installer only)  
✅ **Beautiful icon** - Custom designed prayer times icon  

---

## 🎯 Distribution Options

### For End Users:
1. **Installer** (Recommended): `EzanVakti-Setup-v1.0.0.exe`
   - Professional installation
   - Auto-startup option
   - Uninstaller included
   - ~90 MB

2. **Portable**: `EzanVakti.exe`
   - No installation needed
   - Run from anywhere
   - ~70-80 MB

---

## 🔧 Development Build (Debug)
```powershell
dotnet build
dotnet run
```

---

## 📋 System Requirements

- **OS**: Windows 10 version 1809+ or Windows 11
- **Architecture**: x64 (64-bit)
- **.NET**: Bundled (self-contained)
- **Permissions**: Location access (optional, for GPS)

---

## 🌟 First Launch

1. App appears in **taskbar** (bottom-left)
2. **Right-click** system tray icon for settings
3. Choose location:
   - 🌍 Windows Konum (GPS)
   - 🏙️ Şehir Seç (81 provinces)
4. Prayer times update automatically

🕌 Hayırlı işler!
