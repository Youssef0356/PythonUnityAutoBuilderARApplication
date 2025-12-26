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
2.  **Setup Scripts**: Place the `Generate.py` script .
3.  **Run Automation**: 
    - Run the `Generate.py` script.
    - **Drop your `.glb` 3D model** into the folder where the script is located and your `QRCode.png` inside this folder.
4.  **Automatic Processing**: The script will automatically:
    - Create a dedicated Maquette folder.
    - Move the `.glb` and `QRCode.png` inside it.
    - Generate a `Data.json` configuration file.
    - Create an `Assets/` directory with subfolders for every part found in the 3D model.

---

## 📝 Phase 3: Content & Descriptions

Now, fill in the details for each part of your model.

### � 1. Part Descriptions (Excel)
Inside each part's subfolder (under `Assets/`), an Excel file is generated.
- **Column A**: Enter the **Key** (e.g., `Marque`, `Type`, `Voltage`).
- **Column B**: Enter the **Value** (e.g., `Siemens`, `Automate`, `24V`).
- Save and close the Excel file when finished.

### 🖼️ 2. Part Images
If you want an image to appear when a specific part is clicked in the AR app:
- Drop the image file into that part's specific subfolder.
- Ensure the image name is clear and correct.

---

## ✅ Phase 4: Final Validation Checklist

Before building, verify that the following files are present in your Maquette folder:

- [ ] **Folder Structure**: Everything is organized in your Maquette directory.
- [ ] **Model**: The `.glb` file is inside the folder.
- [ ] **Target**: The `QRCode.png` is inside the folder.
- [ ] **Build Script**: The `build_maquette.bat` file has been moved/copied into this folder.
- [ ] **Assets**: Images inside subfolders are named correctly.

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
