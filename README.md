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

## 3. Controls & Non-Gamer Accessibility Suite

| Action | Primary Input (Unified Input System) | Accessibility Behavior |
| :--- | :--- | :--- |
| **Move** | `W`, `A`, `S`, `D` / Arrow Keys | **Smart Auto-Follow Camera**: When moving without mouse input, the camera automatically glides behind the player heading. Non-gamers don't have to fight two-handed mouse+keyboard coordination! |
| **Look / Orbit Camera** | Mouse Delta | Optional manual camera control; smoothly yields back to auto-follow when released. |
| **Sprint** | `Left Shift` | Speed boosts to `6.4m/s` (faster than guards' `3.8m/s` chase speed). |
| **Crouch** | `C` or `Left Ctrl` | Drops player height to `0.9m`; slows guard detection buildup by **55%**. |
| **Jump** | `Space` | Vault onto curbs, crates, and containers. |
| **Echo Pulse** | `Q` | 360° LiDAR sweep (32m range, softened acoustic noise radius of ~7m). |
| **Passive Sonar** | Automatic (Walking) | **Silent Micro-Sonar**: 36 rays automatically map immediate curbs and walls (9.5m) every 1.3s with zero noise emitted! |
| **Defuse Bomb** | Hold `E` for 3.0s | In-world HUD displays progress bar and distance markers. |
| **Smoke Pellets** | Automatic (On Contact) | **Second Chances**: 3 charges that automatically deploy when a guard reaches you, stunning them for 4.5s and knocking them back. |
| **Silent Takedown** | `F` | Incapacitates an unaware guard from behind. |
| **Toggle MiniMap** | `Tab` or `M` | Top-down SLAM radar. |
| **Pause / Resume** | `Escape` | Toggles pause overlay and unlocks cursor. |

---

## 4. Accessibility & Quality-of-Life Enhancements

1. **In-World Tactical Waypoints**:
   - High-visibility tactical markers (`◆ BOMB #1 [Xm]`) clamp to screen edges so players never get lost in the dark.
   - When all 3 bombs are defused, a glowing green `★ EXTRACTION POINT` beacon immediately illuminates the escape route.
2. **Detection Grace Period & Dynamic Flashlights**:
   - Guards no longer trigger instant game over when glancing at the player.
   - A 2.8s grace period allows the player to duck behind cover or crouch (`C`). Guard flashlights shift color from warm yellow $\rightarrow$ alerting orange $\rightarrow$ red to give clear visual warning.
3. **Escapable Chase Speed**:
   - Guard chase speed has been rebalanced to `3.8m/s` (player walks at `3.6m/s` and sprints at `6.4m/s`). Simply sprinting away easily breaks line of sight.
4. **Relaxed Mission Timer**:
   - The global timer is set to a generous **10:00 minutes** (with an optional untimed `detectiveMode` toggle).
5. **Procedural Audio Engine**:
   - In-game audio (LiDAR sonar chirp, 3-note defusal success chord, guard alert tones, and smoke whoosh) is synthesized procedurally in C# via `AudioClip.Create` — zero external asset dependencies required.

---

## 4. Character Visuals & Mixamo Humanoid Import Guide

The Night Warden features a grounded, modular AAA tactical aesthetic built from script-free URP/Lit materials and an articulated low-poly armor rig:
- **Cowl**: Full head cowl with sharp, angular ears (angled 8° back and 14° outward) and angled cold-white emissive eye lenses (`#EEF6FF` with HDR emission intensity `2.5`).
- **Segmented Armor**: Upper pectoral chest plate, sternum ridge, abdominal armor tiers, and tactical shoulder pauldrons.
- **Stealth Bracers & Gloves**: Reinforced forearm gauntlets with scalloped blade ridges and tactical gloves.
- **Utility Belt**: Dark gunmetal center buckle with 4 modular equipment pouches.
- **Tactical Cape**: 4-segment bone-chain cape attached to upper back/shoulders driven by `CapeSway.cs` spring-damper physics with double-sided rendering (`_Cull = 0`).

### Mixamo Character Import Steps (Optional FBX Upgrade)
If you wish to swap the procedural tactical under-rig for an animated Mixamo humanoid character:
1. Go to [Mixamo.com](https://www.mixamo.com) and search for **"Y Bot"**, **"SWAT"**, or **"Vanguard"**.
2. Download the model as **FBX for Unity (.fbx)** in T-Pose.
3. Drag the `.fbx` into your Unity Project at `Assets/Models/`.
4. Select the FBX file in Unity's Project window and open the **Inspector**:
   - Go to the **Rig** tab.
   - Set **Animation Type** to `Humanoid`.
   - Set **Avatar Definition** to `Create From This Model`.
   - Click **Apply**.
5. Drag the imported character prefab into the scene under `Player`.
6. Parent the tactical child armor meshes from `Vigilante_VisualRig` to the corresponding bones:
   - `Cowl_Head` & `Cowl_Ear_L/R` & `Cowl_EyeLens_L/R` $\rightarrow$ Parent to `mixamorig:Head`.
   - `Armor_ChestPlate`, `Armor_AbPlate_*`, `Vigilante_Cape` $\rightarrow$ Parent to `mixamorig:Spine2` (Chest).
   - `UtilityBelt_Strap` $\rightarrow$ Parent to `mixamorig:Hips`.
   - `Bracer_Armor_L/R` $\rightarrow$ Parent to `mixamorig:LeftForeArm` / `mixamorig:RightForeArm`.

---

## 5. Lighting, Post-Processing & Atmosphere Specifications

### 1. Cinematic Lighting
- **Cold Moonlight**: Directional light at rotation `(52, -32, 0)`, Color `#7BAEF2` (RGB: 0.48, 0.68, 0.95), Intensity `0.28`, Soft Shadows enabled, Normal Bias `0.4`.
- **Cyan Rim Light**: Follows player high-rear at `(0, 2.2, -1.8)` angled `38°` down. Spot Light, Color `#47E0F2` (Warm Cyan), Intensity `0.45`, Range `4.8m`, Spot Angle `68°`. Separates the dark vigilante silhouette from the pitch-black district without flooding ambient darkness.
- **URP Shadow Distance**: Configured to `40m` in `PC_RPAsset.asset` for crisp close-range character and architecture shadows with zero performance waste on Apple Silicon.
- **Reflection Probe**: Box projection (`120m × 30m × 120m`) capturing dark district ambient specular reflections on wet asphalt and suit armor plates.
- **Light Probe Group**: 3D grid across street and warehouse entrances for dynamic spherical harmonics shading on character movement.

### 2. Global Post-Processing Volume
Configured in `Assets/Settings/DefaultVolumeProfile.asset` and applied globally:
- **Bloom**: Threshold `0.9`, Intensity `0.5`, Scatter `0.7` (eye lenses and bomb LEDs glow softly).
- **Vignette**: Intensity `0.35`, Smoothness `0.4` (darkens screen edges for claustrophobic focus).
- **Film Grain**: Thin1, Intensity `0.15` (cinematic gritty film texture).
- **Color Adjustments**: Contrast `12`, Saturation `-15` (subtle gritty desaturation), Color Filter `#D9EBFF` (cold blue-teal night grade).
- **Tonemapping**: ACES (filmic high-dynamic range response).
- **Depth of Field**: Gaussian, Start `12m`, End `45m` (subtle atmospheric depth blur on distant buildings).
- **Motion Blur**: Camera and Objects, Quality `Low`, Intensity `0.18` (high-speed camera orbit smoothness).

### 3. Wet City Atmosphere
- **Fog**: Exponential Squared, Density `0.024`, Color `(0.025, 0.038, 0.065)` (dark industrial midnight mist).
- **Camera-Following Rain**: 3,000 velocity-stretched particles tracking the Main Camera for dense rain streaks without wasting particles across empty geometry.
- **Ground Rain Splashes**: Localized particle system (120 particles max) parented to the player, triggering rapid ground ripples on wet surfaces.
- **Wet Asphalt Ground**: URP Lit material with normal map `T_Asphalt_Normal.png`, Smoothness `0.85`, providing slick reflections under the moonlight.

### 4. Echo Look Integration
- Point cloud buffer utilizes unlit additive billboard particles (`Universal Render Pipeline/Particles/Unlit`).
- The player character, armor meshes, and cape are set to **Layer 2 (`Ignore Raycast`)**, and `EchoPulse.cs` explicitly masks out Layer 2 so the player's own body is never rendered into the point cloud.

---

## 6. Verification Checklist in Play Mode

1. **Press Play (`⌘+P`)**:
   - Verify character renders in dark tactical charcoal suit with matte armor plates and cold-white glowing eye lenses.
   - Verify faint warm-cyan rim light highlights the vigilante silhouette against the pitch-black background.
2. **Movement & Cape Physics**:
   - Move with `W/A/S/D` and sprint with `Shift`: verify the cape naturally sways, drags, and flutters behind the character via `CapeSway.cs` without clipping through the legs.
3. **Atmospheric Rain**:
   - Observe rain streaks falling around the camera view and rain splashes on the ground around the player.
4. **Echo Pulse (`Q`)**:
   - Press `Q`: 1,500 acoustic rays expand outwards. Verify the environment maps in blue-grey, guards in red, and bombs in yellow.
   - Verify the Night Warden's own body produces **zero** points in the point cloud.
5. **Frame Rate**:
   - Runs locked at 60 fps on Apple Silicon Macs.
