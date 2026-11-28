# Night Warden: Echo Protocol 🦇📡

> **A 3rd-Person Stealth Echolocation LiDAR Vigilante Game demonstrating Simultaneous Localization and Mapping (SLAM).**  
> Built in **Unity 6 (URP)** using **100% C#** for macOS (Apple Silicon & Intel) and Windows.

---

## 1. Overview & Concept

**Night Warden: Echo Protocol** is a dark, cinematic third-person stealth action game inspired by tactical vigilante thrillers. Set in a rain-slicked industrial district shrouded in total darkness, you play as the **Night Warden** — a masked operative equipped with a tactical cowl and a high-frequency acoustic LiDAR echolocation system.

In this pitch-black environment, traditional vision is useless. You must navigate using your **Echo Pulse (`Q`)**, a 360-degree acoustic sweep that casts 1,500 spherical rays to map obstacles, alleys, rooftops, and threats as a persistent, high-density 3D point cloud.

### The Stealth Dilemma
Every pulse paints your environment, but sound carries. An Echo Pulse creates a loud acoustic disturbance (20m radius). Four to six armed syndicate guards patrol the sector on dynamic NavMesh patrol routes. If they hear your pulse or loud footsteps, they will switch states to **Investigate**, track your position, and enter **Chase** mode with flashlight beams and lethal gunfire.

---

## 2. SLAM Mechanics & Technical Architecture

The game models real-world robotics and spatial perception principles directly in C#:

| System | Technology & Theoretical Foundation | In-Game Implementation (`Assets/Scripts/...`) |
| :--- | :--- | :--- |
| **Acoustic LiDAR Pulse** | 360° spherical raycasting via Fibonacci lattice distribution | `SLAM/EchoPulse.cs`: Casts 1,500 rays over ~1.0s, draining tactical battery with automatic recharge. |
| **Point Cloud Buffer** | High-capacity GPU particle chunk ring buffer (200,000 points) | `SLAM/PointCloudBuffer.cs`: Stores persistent points classified by semantic hit type with billboard distance attenuation. |
| **Semantic Classification** | Real-time tag-based point filtering and color coding | `SLAM/EchoPulse.cs`: Blue-grey for architecture, yellow for bomb triggers, red for hostiles (visible through walls), cyan for climbable ledges. |
| **3D Voxel Occupancy** | Volumetric discretization with 3D-DDA raymarching | `SLAM/VoxelMap.cs`: 0.5m grid recording `Free` and `Occupied` voxels with log-odds occupancy updates. |
| **Top-Down MiniMap** | Real-time horizontal height-slice projection | `SLAM/MiniMap.cs`: Slices 3D voxel data into a 2D top-down navigational radar (`Tab`). |
| **Acoustic Propagation** | Centralized quadratic inverse-square sound falloff | `Stealth/NoiseSystem.cs`: Dispatches noise events from pulses, sprinting, and defusals to alert the AI. |
| **Guard AI State Machine** | Dynamic NavMesh agent perception (Vision cone + Sound hearing) | `AI/GuardAI.cs`: 5 states: `Patrol`, `Investigate`, `Chase`, `Search`, and `Eliminated`. |
| **Takedown Mechanic** | Directional dot-product angle & distance verification | `Player/TakedownAction.cs`: Silent takedown (`F`) from behind crouched guards. |
| **Mission Objective** | Multi-target interaction & shared countdown timer | `Objectives/BombObjective.cs` & `Core/GameManager.cs`: 3 bombs, 6:00 shared timer, extraction zone escape. |

---

## 3. Controls

| Action | Primary Input (Unified Input System) |
| :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` / Arrow Keys |
| **Look / Orbit Camera** | Mouse Delta |
| **Sprint** | `Left Shift` (generates 8m noise radius) |
| **Crouch** | `C` or `Left Ctrl` (reduces noise to ~1.5m; enables stealth) |
| **Jump** | `Space` |
| **Echo Pulse** | `Q` (360° LiDAR sweep, creates 20m acoustic disturbance) |
| **Silent Takedown** | `F` (when crouched directly behind an unaware guard) |
| **Defuse Bomb** | Hold `E` for 3.0 continuous seconds within 2.4m range |
| **Toggle MiniMap** | `Tab` or `M` |
| **Pause / Resume** | `Escape` |

---

## 4. Repository Structure

```
Assets/
├── Editor/
│   └── NightWardenSceneBuilder.cs      # 1-click procedural level generator (alleys, warehouses, lighting, rain)
├── Scenes/
│   └── NightWardenLevel.unity          # Master game scene with baked NavMesh
├── Scripts/
│   ├── AI/
│   │   └── GuardAI.cs                  # NavMesh 5-state AI (Patrol, Investigate, Chase, Search, Eliminated)
│   ├── Core/
│   │   └── GameManager.cs              # 6-minute mission timer, bomb counting, victory/defeat transitions
│   ├── Objectives/
│   │   ├── BombObjective.cs            # 3.0-second defuse interaction & progress tracking
│   │   └── ExtractionPoint.cs          # Final extraction beacon triggered upon all bombs defused
│   ├── Player/
│   │   ├── ThirdPersonPlayerController.cs # Kinematic movement, sprint, crouch, footstep audio & noise
│   │   ├── ThirdPersonCameraFollow.cs  # Smooth collision-avoiding orbital follow camera
│   │   ├── TakedownAction.cs           # Stealth rear-angle assassination detection
│   │   └── WardenInput.cs              # Dual-compatibility input wrapper (New Input System & Legacy)
│   ├── SLAM/
│   │   ├── EchoPulse.cs                # 1,500-ray 360° LiDAR pulse with semantic tagging
│   │   ├── PointCloudBuffer.cs         # 200,000-point GPU particle chunk buffer
│   │   ├── VoxelMap.cs                 # 3D occupancy grid with 3D-DDA raymarching
│   │   └── MiniMap.cs                  # 2D top-down real-time height-sliced radar
│   ├── Stealth/
│   │   └── NoiseSystem.cs              # Global acoustic event bus with distance attenuation
│   └── UI/
│       └── HUDController.cs            # Battery meter, 6:00 countdown, bomb tracker, noise meter, prompt UI
└── Settings/                           # Universal Render Pipeline (URP) configuration assets
```

---

## 5. How to Play in Unity

1. Open **Unity Hub** and select Unity **6000.6.11f1** (or any Unity 6 LTS release).
2. Open the project folder.
3. In the Project window, navigate to `Assets/Scenes/NightWardenLevel.unity` and double-click to open.
4. Press **Play** (`⌘+P` on Mac or `Ctrl+P` on Windows).
5. Navigate the pitch-black district with `W/A/S/D`, press `Q` to reveal your surroundings, defuse all 3 explosive devices, eliminate or evade guards, and reach the Extraction Point!
