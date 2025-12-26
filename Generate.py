import os
import sys
import json
import shutil
import time
from pathlib import Path
from pygltflib import GLTF2
from watchdog.observers import Observer
from watchdog.events import FileSystemEventHandler
try:
    import openpyxl
    from openpyxl.styles import Font
except ImportError:
    openpyxl = None



# Supported image extensions
IMAGE_EXTENSIONS = {'.png', '.jpg', '.jpeg', '.gif', '.bmp', '.webp'}
# Supported video extensions
VIDEO_EXTENSIONS = {'.mp4', '.avi', '.mov', '.mkv', '.webm'}


# ============================================================================
# GLB HIERARCHY EXTRACTION
# ============================================================================

def extract_hierarchy(glb_path):
    """Extract hierarchical structure from a GLB file."""
    try:
        gltf = GLTF2().load(glb_path)
        
        # Build a dictionary of node index to node data
        nodes_dict = {}
        
        if gltf.nodes:
            for idx, node in enumerate(gltf.nodes):
                node_name = node.name if node.name else f"Node_{idx}"
                nodes_dict[idx] = {
                    'name': node_name,
                    'children': node.children if node.children else []
                }
        
        # Find root nodes (nodes that are not children of any other node)
        all_children = set()
        for node_data in nodes_dict.values():
            all_children.update(node_data['children'])
        
        root_nodes = [idx for idx in nodes_dict.keys() if idx not in all_children]
        
        # Build hierarchy tree recursively
        def build_tree(node_idx):
            """Recursively build tree structure."""
            if node_idx not in nodes_dict:
                return None
            
            node = nodes_dict[node_idx]
            tree_node = {
                'name': node['name'],
                'children': []
            }
            
            for child_idx in node['children']:
                child_tree = build_tree(child_idx)
                if child_tree:
                    tree_node['children'].append(child_tree)
            
            return tree_node
        
        # Build trees for all root nodes
        hierarchy = []
        for root_idx in root_nodes:
            tree = build_tree(root_idx)
            if tree and tree['name'].strip():  # Only include named nodes
                hierarchy.append(tree)
        
        return hierarchy
        
    except Exception as e:
        print(f"❌ Error reading GLB file: {e}")
        return []


def create_folders_from_hierarchy(hierarchy, parent_folder):
    """Recursively create folders from hierarchy tree."""
    created_folders = []
    
    for node in hierarchy:
        node_name = node['name']
        if not node_name or not node_name.strip():
            continue
        
        # Create folder for this node
        node_folder = parent_folder / node_name
        node_folder.mkdir(exist_ok=True)
        created_folders.append(node_folder)
        print(f"✔️  Created: {node_folder}")
        
        # Create Excel file for this part
        create_part_excel(node_folder, node_name)
        
        # Recursively create child folders
        if node['children']:
            child_folders = create_folders_from_hierarchy(node['children'], node_folder)
            created_folders.extend(child_folders)
    
    return created_folders


def create_part_excel(folder_path, name):
    """Create an Excel file with Key/Value columns."""
    if openpyxl is None:
        print(f"⚠️  openpyxl not installed. Skipping Excel creation for: {name}")
        return
    
    excel_path = Path(folder_path) / f"{name}.xlsx"
    if excel_path.exists():
        return

    try:
        wb = openpyxl.Workbook()
        ws = wb.active
        ws.title = "Description"
        
        # Set headers
        ws['A1'] = "Key"
        ws['B1'] = "Value"
        
        # Bold headers
        bold_font = Font(bold=True)
        ws['A1'].font = bold_font
        ws['B1'].font = bold_font
        
        # Adjust column widths
        ws.column_dimensions['A'].width = 20
        ws.column_dimensions['B'].width = 40
        
        wb.save(excel_path)
        print(f"✔️  Created Excel: {excel_path}")
    except Exception as e:
        print(f"❌ Error creating Excel file: {e}")


# ============================================================================
# DATA.JSON GENERATION
# ============================================================================

def find_glb_file(folder_path):
    """Find the GLB file in the main folder."""
    folder = Path(folder_path)
    glb_files = list(folder.glob('*.glb'))
    
    if not glb_files:
        return None
    
    return glb_files[0]


def find_qr_code(folder_path):
    """Find QR code image file in the main folder."""
    folder = Path(folder_path)
    
    # Look for files starting with 'QRCode' or 'qrcode' (case insensitive)
    for file in folder.iterdir():
        if file.is_file() and file.suffix.lower() in IMAGE_EXTENSIONS:
            if file.stem.lower() == 'qrcode':
                return file.name
    
    # Default fallback
    return "QRCode.png"


def scan_folder_for_images(folder_path, base_path):
    """Scan a folder for images and return button entries."""
    folder = Path(folder_path)
    buttons = []
    
    for file in folder.iterdir():
        if file.is_file() and file.suffix.lower() in IMAGE_EXTENSIONS:
            if file.stem.lower() == 'qrcode':
                continue
            # Get relative path from base_path
            relative_path = file.relative_to(base_path)
            # Convert Windows path to forward slashes for JSON
            image_path = str(relative_path).replace('\\', '/')
            
            # Button name is filename without extension
            button_name = file.stem
            
            buttons.append({
                "name": button_name,
                "imagePath": image_path
            })
    
    # Sort buttons by name for consistency
    buttons.sort(key=lambda x: x['name'])
    
    return buttons


def find_video_for_part(part_name, base_path):
    """Find video file matching the part name in Assets/Videos folder."""
    videos_folder = base_path / "Assets" / "Videos"
    
    if not videos_folder.exists():
        return ""
    
    # Look for video files matching the part name
    for ext in VIDEO_EXTENSIONS:
        video_file = videos_folder / f"{part_name}{ext}"
        if video_file.exists():
            # Return relative path from base_path
            relative_path = video_file.relative_to(base_path)
            return str(relative_path).replace('\\', '/')
    
    return ""


def scan_folder_structure(folder_path, base_path):
    """Recursively scan folder structure and build parts array."""
    folder = Path(folder_path)
    parts = []
    
    # Get all subdirectories (excluding Videos)
    subdirs = [d for d in folder.iterdir() if d.is_dir() and d.name != 'Videos']
    
    for subdir in subdirs:
        part_name = subdir.name
        
        # Scan for images in this folder
        buttons = scan_folder_for_images(subdir, base_path)
        
        # Find matching video in Videos folder
        video_path = find_video_for_part(part_name, base_path)
        
        # Recursively scan for child parts
        child_parts = scan_folder_structure(subdir, base_path)
        
        # Build part entry
        part = {
            "name": part_name,
            "video": video_path,
            "datasheet": "",
            "description": [],
            "buttons": buttons,
            "parts": child_parts
        }
        
        parts.append(part)
    
    # Sort parts by name for consistency
    parts.sort(key=lambda x: x['name'])
    
    return parts


def generate_data_json(folder_path):
    """Generate Data.json for the given folder."""
    folder = Path(folder_path)
    
    if not folder.exists() or not folder.is_dir():
        return None
    
    # Find GLB file
    glb_file = find_glb_file(folder)
    
    if not glb_file:
        return None
    
    model_name = glb_file.stem
    # Use the maquette folder name (same as Unity's maquetteName) to derive
    # the canonical GLB filename used in StreamingAssets/Models.
    maquette_name = folder.name
    canonical_model_file = f"{maquette_name.replace(' ', '')}{glb_file.suffix}"
    
    # Check if Assets folder exists
    assets_folder = folder / "Assets"
    
    if not assets_folder.exists():
        return None
    
    # Scan for main model buttons (in base folder, not in Assets)
    main_buttons = scan_folder_for_images(folder, folder)
    
    # Scan for main model description items (empty for now)
    main_description = []
    
    # Recursively scan Assets folder for parts
    parts = scan_folder_structure(assets_folder, folder)
    
    # Find QR code image
    qr_code_file = find_qr_code(folder)
    
    # Build the data structure
    data = [{
        "name": model_name,
        "qr_image_url": qr_code_file,
        "modelFileUrl": canonical_model_file,
        "Description": main_description,
        "Buttons": main_buttons,
        "parts": parts
    }]
    
    return data


def validate_and_read_excel(excel_path):
    """Read Excel and validate content (Cols A & B only, A needs B)."""
    if openpyxl is None:
        return []

    try:
        wb = openpyxl.load_workbook(excel_path, data_only=True)
        ws = wb.active
        
        description = []
        errors = []
        
        # Check for data in columns beyond B
        for row in ws.iter_rows(min_row=1):
            if len(row) > 2:
                for cell in row[2:]:
                    if cell.value is not None:
                        errors.append(f"Row {cell.row}: Data found in Column {cell.column_letter}. Only A and B are allowed.")
                        break
        
        # Process data rows
        for row in ws.iter_rows(min_row=2):
            key = str(row[0].value).strip() if row[0].value is not None else ""
            value = str(row[1].value).strip() if row[1].value is not None else ""
            
            if not key and not value:
                continue
            
            if key and not value:
                errors.append(f"Row {row[0].row}: Key '{key}' has no value in Column B.")
            elif not key and value:
                errors.append(f"Row {row[0].row}: Value '{value}' has no key in Column A.")
            else:
                description.append({"key": key, "value": value})
        
        if errors:
            print(f"\n❌ VALIDATION ERROR in {excel_path.name}:")
            for err in errors:
                print(f"   - {err}")
            return None # Indicate validation failure
            
        return description
    except Exception as e:
        print(f"❌ Error reading Excel file {excel_path}: {e}")
        return None


def update_json_from_excel(model_folder):
    """Walk through structure and update Data.json from Excel files."""
    folder = Path(model_folder)
    data_json_path = folder / "Data.json"
    
    if not data_json_path.exists():
        return
    
    try:
        with open(data_json_path, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        if not data:
            return

        # 1. Update main model description
        main_excel = folder / f"{data[0]['name']}.xlsx"
        if main_excel.exists():
            desc = validate_and_read_excel(main_excel)
            if desc is not None:
                data[0]['Description'] = desc

        # 2. Update parts description recursively
        assets_folder = folder / "Assets"
        if assets_folder.exists():
            def update_parts_recursive(parts_list, current_path):
                for part in parts_list:
                    part_folder = current_path / part['name']
                    part_excel = part_folder / f"{part['name']}.xlsx"
                    
                    if part_excel.exists():
                        desc = validate_and_read_excel(part_excel)
                        if desc is not None:
                            part['description'] = desc
                    
                    if part.get('parts'):
                        update_parts_recursive(part['parts'], part_folder)

            update_parts_recursive(data[0]['parts'], assets_folder)
        
        # Save updated JSON
        with open(data_json_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, indent=4, ensure_ascii=False)
            
    except Exception as e:
        print(f"❌ Error syncing JSON from Excel: {e}")


def save_data_json(folder_path, data):
    """Save data to Data.json file."""
    folder = Path(folder_path)
    data_json_path = folder / "Data.json"
    
    with open(data_json_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=4, ensure_ascii=False)
    
    return data_json_path


def regenerate_data_json(folder_path):
    """Regenerate the Data.json file."""
    data = generate_data_json(folder_path)
    
    if data:
        data_json_path = save_data_json(folder_path, data)
        print(f"✅ Data.json updated: {data_json_path}")
        # Sync data from Excel files back into the JSON
        update_json_from_excel(folder_path)
        return True
    
    return False


# ============================================================================
# FOLDER STRUCTURE CREATION
# ============================================================================

def create_folder_structure(glb_file_path, auto_mode=False):
    """Create the complete folder structure for a GLB file."""
    glb_path = Path(glb_file_path)
    
    # Wait a bit to ensure file is fully copied
    if auto_mode:
        time.sleep(1)
    
    if not glb_path.exists():
        print(f"❌ File not found: {glb_file_path}")
        return None
    
    if glb_path.suffix.lower() != '.glb':
        print(f"❌ Not a GLB file: {glb_file_path}")
        return None
    
    # Get the model name (filename without extension)
    model_name = glb_path.stem
    
    # Create main folder in the same directory as the GLB file
    parent_dir = glb_path.parent
    main_folder = parent_dir / model_name
    
    if main_folder.exists():
        if auto_mode:
            print(f"⚠️  Folder '{model_name}' already exists! Skipping...")
            return None
        else:
            print(f"⚠️  Folder '{model_name}' already exists!")
            response = input("Do you want to overwrite it? (y/n): ").lower()
            if response != 'y':
                print("❌ Operation cancelled.")
                return None
            shutil.rmtree(main_folder)
    
    print(f"\n📂 Creating folder structure for: {model_name}")
    
    # Create main folder
    main_folder.mkdir(exist_ok=True)
    print(f"✔️  Created: {main_folder}")
    
    # Create Assets folder
    assets_folder = main_folder / "Assets"
    assets_folder.mkdir(exist_ok=True)
    print(f"✔️  Created: {assets_folder}")
    
    # Extract hierarchical structure from GLB
    print(f"🔍 Extracting hierarchy from GLB file...")
    hierarchy = extract_hierarchy(glb_file_path)
    
    if hierarchy:
        print(f"✔️  Found {len(hierarchy)} top-level parts:")
        create_folders_from_hierarchy(hierarchy, assets_folder)
    else:
        print("⚠️  No hierarchy found in GLB file (or couldn't extract structure)")
    
    # Create Videos folder
    videos_folder = assets_folder / "Videos"
    videos_folder.mkdir(exist_ok=True)
    print(f"✔️  Created: {videos_folder}")
    
    # Create empty Data.json
    data_json_path = main_folder / "Data.json"
    with open(data_json_path, 'w', encoding='utf-8') as f:
        json.dump([], f, indent=4)
    print(f"✔️  Created: {data_json_path}")
    
    # Move GLB file to main folder
    try:
        new_glb_path = main_folder / glb_path.name
        shutil.move(str(glb_path), str(new_glb_path))
        print(f"✔️  Moved GLB file to: {new_glb_path}")
    except Exception as e:
        print(f"⚠️  Could not move GLB file: {e}")
    
    # Create Excel file for the main model
    create_part_excel(main_folder, model_name)
    
    print(f"\n🎉 Folder structure created successfully!")
    print(f"📍 Location: {main_folder}\n")
    
    # Generate initial Data.json
    print(f"🔄 Generating Data.json...")
    regenerate_data_json(main_folder)
    
    return main_folder


# ============================================================================
# FILE SYSTEM WATCHER
# ============================================================================

class UnifiedFileHandler(FileSystemEventHandler):
    """Handles file system events for both GLB and image files."""
    
    def __init__(self, watch_folder):
        self.watch_folder = Path(watch_folder)
        self.processed_glb_files = set()
        self.last_update = {}
        self.update_delay = 1  # Wait 1 second before updating
    
    def _find_model_folder(self, file_path):
        """Find the model folder for a given file (folder containing .glb and Data.json)."""
        path = Path(file_path)
        
        # Walk up the directory tree to find a folder with a .glb file
        current = path.parent
        
        while current != self.watch_folder and current.parent != current:
            # Check if this folder has a .glb file
            if list(current.glob('*.glb')):
                return current
            current = current.parent
        
        return None
    
    def _should_process_file(self, file_path):
        """Check if file should trigger an update."""
        path = Path(file_path)
        
        # Only process image, video, or excel files
        if path.suffix.lower() not in IMAGE_EXTENSIONS and \
           path.suffix.lower() not in VIDEO_EXTENSIONS and \
           path.suffix.lower() != '.xlsx':
            return False
        
        # Process QRCode files (they should trigger updates)
        # No need to exclude them anymore
        
        # Find model folder
        model_folder = self._find_model_folder(file_path)
        if not model_folder:
            return False
        
        # Throttle updates per model folder
        current_time = time.time()
        last_time = self.last_update.get(str(model_folder), 0)
        
        if current_time - last_time < self.update_delay:
            return False
        
        self.last_update[str(model_folder)] = current_time
        return True
    
    def _handle_file_event(self, event_type, file_path):
        """Handle image or video file events."""
        if not self._should_process_file(file_path):
            return
        
        path = Path(file_path)
        model_folder = self._find_model_folder(file_path)
        
        if model_folder:
            if path.suffix.lower() == '.xlsx':
                ext_type = "Excel"
            else:
                ext_type = "Video" if path.suffix.lower() in VIDEO_EXTENSIONS else "Image"
                
            print(f"\n📸 {ext_type} {event_type}: {path.name}")
            
            # Wait a moment to ensure file is fully written
            time.sleep(0.5)
            
            print(f"🔄 Updating Data.json from Excel/Files for: {model_folder.name}")
            if path.suffix.lower() == '.xlsx':
                update_json_from_excel(model_folder)
            else:
                regenerate_data_json(model_folder)
                # After regenerating the structure, also sync Excel data back in
                update_json_from_excel(model_folder)
    
    def on_created(self, event):
        """Called when a file or directory is created."""
        if event.is_directory:
            return
        
        file_path = Path(event.src_path)
        if file_path.suffix.lower() in IMAGE_EXTENSIONS and file_path.stem.lower() == 'qrcode' and file_path.parent == self.watch_folder:
            model_folders = []
            for subdir in self.watch_folder.iterdir():
                if subdir.is_dir() and list(subdir.glob('*.glb')):
                    model_folders.append(subdir)
            if len(model_folders) == 1:
                model_folder = model_folders[0]
                try:
                    dest_path = model_folder / file_path.name
                    shutil.move(str(file_path), str(dest_path))
                    print(f"\n📱 QRCode image moved to: {dest_path}")
                    time.sleep(0.5)
                    print(f"🔄 Updating Data.json for: {model_folder.name}")
                    regenerate_data_json(model_folder)
                except Exception as e:
                    print(f"⚠️  Could not move QRCode image: {e}")
            return
        
        # Handle GLB files
        if file_path.suffix.lower() == '.glb' and file_path not in self.processed_glb_files:
            # Only process GLB files directly in the watch folder
            if file_path.parent == self.watch_folder:
                print(f"\n🔔 New GLB file detected: {file_path.name}")
                self.processed_glb_files.add(file_path)
                
                model_folder = create_folder_structure(file_path, auto_mode=True)
                
                # If folder was created, start watching it for images
                if model_folder:
                    print(f"\n👀 Now watching {model_folder.name} for image updates...")
        
        # Handle image, video, and excel files
        elif file_path.suffix.lower() in IMAGE_EXTENSIONS or \
             file_path.suffix.lower() in VIDEO_EXTENSIONS or \
             file_path.suffix.lower() == '.xlsx':
            self._handle_file_event("added", file_path)
    
    def on_deleted(self, event):
        """Called when a file is deleted."""
        if not event.is_directory:
            file_path = Path(event.src_path)
            if file_path.suffix.lower() in IMAGE_EXTENSIONS or \
               file_path.suffix.lower() in VIDEO_EXTENSIONS or \
               file_path.suffix.lower() == '.xlsx':
                self._handle_file_event("removed", file_path)


def watch_folder(folder_path):
    """Watch a folder for GLB and image files."""
    folder = Path(folder_path)
    
    print("=" * 60)
    print("🔍 Unified GLB & Data.json Watcher - AUTO MODE")
    print("=" * 60)
    print(f"\n📁 Watching folder: {folder}")
    print("📥 Drop a .glb file → Creates folder structure & Data.json")
    print("🖼️  Drop images → Auto-updates Data.json")
    print("🎬 Drop videos in Assets/Videos/ → Auto-links to matching parts")
    print("📱 Drop QRCode image → Auto-updates qr_image_url")
    print("⏹️  Press Ctrl+C to stop watching\n")
    
    event_handler = UnifiedFileHandler(folder)
    observer = Observer()
    observer.schedule(event_handler, str(folder), recursive=True)
    observer.start()
    
    try:
        while True:
            time.sleep(1)
    except KeyboardInterrupt:
        print("\n\n⏹️  Stopping file watcher...")
        observer.stop()
    
    observer.join()
    print("✅ Watcher stopped. Goodbye!")


# ============================================================================
# MAIN
# ============================================================================

def main():
    """Main entry point."""
    # Get the directory where the script is located
    script_dir = Path(__file__).parent.absolute()
    
    # If a file is provided as argument, process it directly
    if len(sys.argv) > 1:
        arg = sys.argv[1].strip().strip('"')
        arg_path = Path(arg)
        
        # Check if it's a GLB file
        if arg_path.suffix.lower() == '.glb':
            print("=" * 60)
            print("GLB Folder Structure Generator - MANUAL MODE")
            print("=" * 60)
            model_folder = create_folder_structure(arg, auto_mode=False)
            
            if model_folder:
                print("\n✅ All done!")
            else:
                print("\n❌ Failed to create folder structure.")
                sys.exit(1)
        
        # Check if it's a folder (for regenerating Data.json)
        elif arg_path.is_dir():
            print("=" * 60)
            print("Data.json Generator - MANUAL MODE")
            print("=" * 60)
            print(f"\n🔄 Generating Data.json for: {arg_path.name}")
            
            success = regenerate_data_json(arg_path)
            
            if success:
                print("\n✅ Done!")
            else:
                print("\n❌ Failed to generate Data.json")
                sys.exit(1)
        else:
            print(f"❌ Invalid argument: {arg}")
            print("Please provide either a .glb file or a folder path")
            sys.exit(1)
    else:
        # Start watching the script's directory
        watch_folder(script_dir)


if __name__ == "__main__":
    main()
