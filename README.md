# 🚀 AR Application Automated Build Setup

This guide provides clear, step-by-step instructions to configure and use the automated build system for your AR maintenance applications.

---

## 🛠️ Phase 1: Initial Configuration

Before starting, you must configure the build script with your local environment paths.

1.  Open `build_maquette.bat` in a text editor.
2.  Update the following variables to match your system:
    ```batch
    REM Path to your main Unity project
    set "UNITY_PROJECT="

    REM Where you want the final .apk to be saved
    set "OUTPUT_DIR="

    REM Path to your Unity Editor executable
    set "UNITY_PATH=C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Unity.exe"
    ```

---

## 📂 Phase 2: Folder & Model Generation

The generation of the application structure is automated via a Python script.

1.  **Create a Workspace**: Create a new folder anywhere on your computer (name it after your project, e.g., `MyNewARProject`).
2.  **Setup Scripts**: Place the `Generate.py` script inside this folder.
3.  **Run Automation**: 
    - Run the `Generate.py` script.
    - **Drop your `.glb` 3D model** into the same folder.
4.  **Automatic Processing**: The script will detect the GLB and automatically:
    - Create a main project folder named after the GLB file.
    - Create the required subfolder structure (`ModelInfos` and `ModelParts`).
    - Move the `.glb` file into `ModelInfos/3DMODEL`.
    - Generate an initial `Data.json`.

---

## 📝 Phase 3: Content & Descriptions

Once the folders are created, you need to manually place your assets in the correct locations.

### 📍 1. Main Project Assets (ModelInfos)
Navigate to the `ModelInfos` folder inside your project directory.
- **QRCode**: Place your `QRCode.png` image inside the `QRCode` folder.
- **Video**: Place your presentation video (mp4, avi, etc.) inside the `Video` folder.
- **Button Images**: Place UI images associated with the main model in the `Button_Images` folder.
- **Description**: Edit the generated Excel file in the `Description` folder to add global metadata.

### 🧩 2. Part Assets (ModelParts)
Navigate to the `ModelParts` folder. You will see a hierarchy matching your 3D model.
For each part folder, you can add:
- **Video**: A video specific to that part inside its `Video` folder.
- **Button Images**: Images to show for that part inside its `Button_Images` folder.
- **Description**: Edit the Excel file in the `Description` folder to add part-specific details (e.g., Key: `Material`, Value: `Steel`).

> [!NOTE]
> The script watches for changes. If you add a file, `Data.json` will automatically regenerate.

---

## ✅ Phase 4: Final Validation Checklist

Before building, verify that the following files are present in your Project folder:

- [ ] **Folder Structure**: `ModelInfos` and `ModelParts` exist.
- [ ] **Model**: The `.glb` file is inside `ModelInfos/3DMODEL`.
- [ ] **Target**: The `QRCode.png` is inside `ModelInfos/QRCode`.
- [ ] **Build Script**: The `build_maquette.bat` file is accessible (root or project folder).
- [ ] **Assets**: Images and videos are placed in their respective `Button_Images` or `Video` subfolders.

---

## 🏗️ Phase 5: Executing the Build

1.  **Close Unity Editor**: The build process requires exclusive access to the project.
2.  **Start Build**: Double-click `build_maquette.bat`.

### 🔄 What Happens Next?
- **[1/3] Data Check**: The script verifies all required files exist.
- **[2/3] Unity Process**: Unity launches in batch mode (headless). This takes several minutes.
- **[3/3] Completion**: Your finished `.apk` is moved to the `OUTPUT_DIR`.

---

## � Technical verification

> [!TIP]
> Always validate that the `name` in `parts` exactly matches the name of the object in your 3D model hierarchy.

> [!IMPORTANT]
> **Model Optimization**: For best AR performance, keep your `.glb` files under 200MB.
