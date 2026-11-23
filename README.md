# Echo: Dark Facility 📡🔦

> **A 3D First-Person LiDAR Horror Game demonstrating Simultaneous Localization and Mapping (SLAM).**  
> Built in **Unity 6 LTS (URP)** using **C#** for macOS (Apple Silicon & Intel) and Windows.

---

## 1. Executive Summary

**Echo: Dark Facility** is an atmospheric first-person horror exploration game that demonstrates real-world robotic SLAM principles through core gameplay mechanics.

You wake up trapped in a pitch-black underground research facility. Normal ambient lighting does not exist. Your only tool for navigation is a **handheld 3D LiDAR scanner**. When you sweep or pulse the scanner, thousands of glowing laser hit points paint the walls, corridors, and objects around you in 3D, incrementally building a **point cloud** and a **3D voxel occupancy map**.

However, active scanning emits acoustic noise across the facility. An invisible predatory entity hunts by sound. You must scan to see your path, find replacement fuses, power up the central generator, and locate the unlocked exit door before the creature tracks your acoustic emissions.

---

## 2. SLAM Concepts and Implementation Mapping

This project grounds academic SLAM concepts directly into gameplay systems:

| SLAM Domain | Theoretical Foundation | In-Game Implementation (`Assets/Scripts/...`) |
| :--- | :--- | :--- |
| **LiDAR Rangefinding** | Planar & 3D Time-of-Flight (ToF) range measurement | `LidarScanner.cs`: Casts 3D rays via continuous cone sweeps (1,500–3,000 rays/sec) and $360^\circ$ radial pulses. |
| **Point Cloud Representation** | Dense 3D unorganized point collections $\{p_i = (x, y, z, c)\}$ | `PointCloudRenderer.cs`: Manages a high-capacity ring buffer (300,000 points) rendered on GPU particle chunks with distance and age blending. |
| **Voxel Occupancy Mapping** | Volumetric Bayesian grid mapping $P(m_{xyz} \mid z_{1:t})$ | `VoxelMap.cs`: Discretizes continuous space into 0.5m voxels. Uses 3D-DDA raymarching to mark beam path as `Free` and impacts as `Occupied`. |
| **Pose Tracking & Localization** | State estimation $\mathbf{x}_t = [x, y, z, \theta]^T$ inside the global frame | `PlayerController.cs`: Tracks character kinematic state, velocity, heading, and odometry in global coordinate space. |
| **Map Slicing & Representation** | 2D projection of 3D point cloud for topological planning | `MapDisplay.cs`: Generates a real-time top-down 2D floor slice of the voxel grid displayed on the player's handheld terminal (`Tab`). |
| **Acoustic Noise Constraints** | Sensor trade-offs and energy/emission footprints | `NoiseSystem.cs`: Propagates distance-attenuated acoustic waves whenever scanning or running, alerting the `MonsterAI`. |

---

## 3. Project Architecture

The codebase follows clean C# software architecture separated by domain namespaces:

```
Assets/Scripts/
├── Player/
│   ├── PlayerController.cs     # First-person character physics, sprint/crouch, mouse look, head bob
│   └── LidarScanner.cs         # Handheld scanner firing sweep/pulse rays, battery energy management
├── Mapping/
│   ├── PointCloudRenderer.cs   # 300,000 point ring buffer with GPU particle system chunk allocation
│   ├── VoxelMap.cs             # 3D voxel occupancy grid with 3D-DDA line traversal
│   └── MapDisplay.cs           # Handheld terminal projecting 2D top-down map slice from voxel data
├── AI/
│   ├── MonsterAI.cs            # 4-state NavMesh machine (Patrol, Investigate, Hunt, Search)
│   └── NoiseSystem.cs          # Centralized acoustic event dispatcher and falloff calculator
├── Gameplay/
│   ├── ObjectiveManager.cs     # Fuse discovery, generator power restoration, emergency lighting
│   └── GameManager.cs          # State machine (Menu, Play, Pause, GameOver, Victory) and difficulty presets
├── Audio/
│   └── AudioManager.cs         # 3D spatial audio, dynamic proximity tension curve, scanner audio loops
└── UI/
    └── UIManager.cs            # Minimal HUD (battery, decibel meter, fuse counters), terminal map, menus
```

---

## 4. Controls

| Action | Input (Keyboard & Mouse) | Description |
| :--- | :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` | First-person movement |
| **Look** | Mouse | First-person camera look |
| **Sprint** | Hold `Left Shift` | Increases speed; generates loud footstep noise |
| **Crouch** | `C` or `Left Ctrl` | Lowers profile; near-silent movement |
| **LiDAR Sweep** | Hold `Left Mouse Button` | Continuous cone scatter; low continuous noise |
| **LiDAR Pulse** | `Right Mouse Button` or `Space` | $360^\circ$ radial burst; reveals large area; high noise spike |
| **Handheld Map** | `Tab` or `M` | Toggles the 2D top-down SLAM terminal display |
| **Interact** | `E` | Pick up fuses, insert into generator, open exit door |
| **Pause** | `Escape` | Toggles the pause menu |

---

## 5. Visual Point Cloud Color Classification

Hit points are automatically categorized and colored in real time:

- ⚪ **White / Pale Cyan**: Static facility geometry (walls, floors, ceilings, obstacles).
- 🔵 **Cyan**: Collectible items and interactive equipment.
- 🟡 **Yellow**: Critical mission objectives (fuses, generator, exit door).
- 🔴 **Red**: The hostile creature (revealed only when hit by laser rays).

---

## 6. Unity 6 Project Setup Guide

### 6.1 Prerequisites
1. Install **Unity Hub** from [unity.com](https://unity.com).
2. Install **Unity 6 LTS** (Apple Silicon editor version recommended on Mac; Windows editor on PC).
3. Clone or open this repository in Unity Hub.

### 6.2 Scene Setup & Component Wiring
1. **Scene Creation**:
   - Create a new 3D scene configured for Universal Render Pipeline (URP).
   - Set the Main Camera background / Environment Lighting to solid black (`#000000`) and ambient intensity to 0 to create total darkness.

2. **Configure Unity Tags**:
   - Navigate to **Edit** &rarr; **Project Settings** &rarr; **Tags and Layers**.
   - Add the following tags:
     - `Player`
     - `Monster`
     - `Fuse`
     - `Generator`
     - `ExitDoor`
     - `Objective`

3. **Hierarchy Layout**:
   - **Player**:
     - Attach `CharacterController`, `PlayerController.cs`, and `LidarScanner.cs`.
     - Child Camera with AudioListener.
   - **Managers**:
     - Create an empty GameObject `Managers` and attach:
       - `PointCloudRenderer.cs`
       - `VoxelMap.cs`
       - `NoiseSystem.cs`
       - `ObjectiveManager.cs`
       - `GameManager.cs`
       - `AudioManager.cs`
       - `UIManager.cs`
       - `MapDisplay.cs`
   - **Monster**:
     - Create a GameObject with `NavMeshAgent` and `MonsterAI.cs`. Tag as `Monster`.
     - Assign patrol waypoint transforms.
   - **Environment**:
     - Construct rooms and corridors using ProBuilder or basic 3D Cubes with BoxColliders.
     - Bake the NavMesh via **Window** &rarr; **AI** &rarr; **Navigation**.

---

## 7. Performance Targets

- **Frame Rate**: Locked 60 FPS at 1080p on Apple Silicon Mac (M1 or newer) and Windows PCs.
- **Point Capacity**: 300,000 points actively rendered without frame drops.
- **Memory Footprint**: Sparse voxel hashing maintains low memory usage even in large facilities.

---

## 8. License

This project is released under the [MIT License](LICENSE). Developed as an educational 3rd-year SLAM robotics demonstration game.
