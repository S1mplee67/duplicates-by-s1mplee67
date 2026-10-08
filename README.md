# Duplicates by S1mplee67

<p align="center">
  <b>A fast, 100% offline Windows desktop application that detects duplicate and visually similar photos by analyzing pixel patterns—not just file names or sizes.</b>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square&logo=windows&logoColor=white" alt="Platform: Windows" />
  <img src="https://img.shields.io/badge/Language-C%23-239120?style=flat-square&logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/.NET-Framework%204.8-512BD4?style=flat-square&logo=dotnet&logoColor=white" alt=".NET 4.8" />
  <img src="https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square" alt="License: MIT" />
  <img src="https://img.shields.io/badge/Network-100%25%20Offline-success?style=flat-square" alt="Offline" />
</p>

---

##  About The Project

Inspired by the clean, intuitive **Similar Photos Cleaner** found on modern smartphones (like Vivo Gallery), **Duplicates by S1mplee67** brings that same experience to your Windows laptop or desktop—completely offline, with zero external dependencies and zero tracking.

Unlike ordinary file finders that only look at identical file names or file sizes, **Duplicates by S1mplee67** analyzes the **actual visual pixel content**. This means it can easily detect photos that are:
- **Resized or downscaled** (e.g. 4K original vs 1080p copy)
- **Re-encoded or format-converted** (e.g. JPG to PNG or WebP)
- **Compressed** (e.g. sent through messaging apps like WhatsApp or Messenger)
- **Taken in continuous burst shots** or slight angle variations

---

##  Key Features

- ** Multi-Stage Perceptual Pixel Hashing:**
  - **Horizontal & Vertical Difference Hash (128-bit dHash):** Analyzes structural brightness gradients across pixels.
  - **Average Luminance Hash (64-bit aHash):** Analyzes overall scene lighting.
  - **16-Cell RGB Color Matrix:** Compares chromatic color distribution to prevent false positives between scenes with similar layouts but totally different colors.
  - **MD5 Checksum:** Instantly catches bit-for-bit identical duplicate files.

- ** Mode 1: Whole Album Cleaner (Vivo Style):**
  - Select any folder or album (e.g. Pictures, Downloads, Camera Roll).
  - Scans and groups all similar/duplicate photos chronologically by **Date** (`September 2, 2026`, `August 20, 2026`, etc.).
  - Shows dynamic stats: e.g. `295 similar photos in total, using 416 MB (reclaimable: ~210 MB)`.

- ** Mode 2: Targeted Reference Photo Search:**
  - Pick **1 or 2 reference photos** (via file picker or drag & drop).
  - Search across any target folder to find all matches, ranked by visual similarity percentage!

- ** 1-Click Smart Auto-Selection:**
  - Click **" Auto-Select (Keep Best)"** to automatically preserve the highest resolution and highest quality photo in each group while selecting the duplicate copies for deletion.

- ** Fullscreen Side-by-Side Inspector (`⤢` button):**
  - Double-click or expand any image to view both photos side-by-side in high resolution.
  - Compares exact capture date, resolution, file size, and file path.
  - Quick action buttons: *"Keep Left"*, *"Keep Right"*, or *"Delete Both"*.

- ** Safe Offline Deletion:**
  - By default, moves deleted files to the **Windows Recycle Bin**, allowing you to restore them anytime.
  - Can be toggled for permanent deletion if desired.

- ** 100% Offline & Private:**
  - Zero internet calls, zero telemetry. Your photos never leave your computer.

---

##  Quick Start / Download

### Running the App:
1. Go to the [**Releases**](../../releases) tab on GitHub and download `Duplicates-by-S1mplee67.exe` (or clone the repository).
2. Double-click `Duplicates-by-S1mplee67.exe` or `Run-Duplicates.bat` to launch.
3. Drag & drop a folder onto the window, or browse to select your album.
4. Click **"🔍 Start Scanning Offline"** and let it find your duplicates!

---

##  Building From Source

This project has **zero third-party dependencies** and can be compiled on any Windows PC using the built-in Windows C# compiler:

1. Clone or download this repository:
   ```cmd
   git clone https://github.com/S1mplee67/duplicates.git
   cd duplicates
   ```

2. Run the build script:
   ```cmd
   build.bat
   ```

3. The compiled `Duplicates-by-S1mplee67.exe` will be generated immediately in the root folder.

---

##  Project Structure

```text
duplicates/
├── .gitignore               # Ignored build and temporary files
├── LICENSE                  # MIT License credited to S1mplee67
├── README.md                # Project documentation & GitHub guide
├── build.bat                # 1-click build script (uses native Windows csc.exe)
├── Run-Duplicates.bat       # Quick launcher script
├── Program.cs               # Application entry point [STAThread]
├── MainWindow.cs            # Modern WPF dark-mode UI & Vivo timeline view
├── CompareWindow.cs         # Fullscreen side-by-side photo comparison modal
├── Scanner.cs               # Multi-threaded album & reference photo scanner
├── ImageHasher.cs           # Perceptual image hashing (dHash, aHash, RGB grid)
├── FileOperations.cs        # Safe Windows Recycle Bin deletion helper
└── Models.cs                # Data models and observable collections
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE) - see the [LICENSE](LICENSE) file for details.

Developed with ❤️ by **S1mplee67**.
