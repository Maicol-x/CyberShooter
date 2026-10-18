# ⚡ FASE 1 - Quick Reference Card

## 🎯 One-Click Setup
```
Tools > CyberShooter > Setup Phase 1 (TPS Core)
```

## ✅ Validate
```
Tools > CyberShooter > Validate Phase 1 Setup
```

## 🔄 Reset (if something breaks)
```
Tools > CyberShooter > Reset Phase 1 (Clean Scene)
```

---

## 🎮 Controls (Play Mode)

| Key | Action |
|-----|--------|
| **W A S D** | Move (relative to camera) |
| **Mouse** | Look around (camera orbits player) |
| **Space** | Jump |
| **Left Shift** | Sprint |
| **Escape** | Unlock cursor |
| **Click** | Re-lock cursor |

---

## 🔧 Key Inspector Settings (Verify After Setup)

### PlayerMotor (on Player)
```
Character Controller → (auto)
Camera Transform → Main Camera
Input Actions → InputSystem_Actions
Action Map: "Player"
Move: "Move" | Jump: "Jump" | Sprint: "Sprint"
Walk: 4 | Sprint: 8 | RotSpeed: 15
Gravity: -9.81 | JumpHeight: 1.5
Ground Layer: Ground + Default
```

### PlayerLook (on Player)
```
Player Body → Player
Camera Transform → Main Camera
Input Actions → InputSystem_Actions
Look: "Look"
Sensitivity: 1 | Invert Y: Off
Min Pitch: -85 | Max Pitch: 85
Lock Cursor: On | Raw Mouse: On
```

### ThirdPersonCameraController (on Main Camera)
```
Target → Player
Offset: (0.5, 1.5, -3)
Rot Speed: 10 | Pitch: -30 to 60
Collision Layers: Default + Ground
Camera Radius: 0.3 | Min Dist: 0.5
Smooth Time: 0.1
Input Actions → InputSystem_Actions
Look: "Look"
```

---

## 🐛 Common Issues & Fixes

| Problem | Fix |
|---------|-----|
| **No movement** | Check PlayerMotor has Input Actions assigned |
| **Camera won't rotate** | Check PlayerLook has Input Actions + cameraTransform |
| **Camera clips walls** | Verify ThirdPersonCameraController collisionLayers |
| **Player floats/sinks** | Check CharacterController center Y = 0.9 |
| **Cursor won't lock** | PlayerLook.lockCursorOnStart = true |
| **Input not working** | Verify InputSystem package installed |
| **"Ground" layer missing** | Run Setup again (auto-creates layer) |

---

## 📦 Required Packages
- ✅ **Input System** (1.20.0+) - already in manifest
- ✅ **Universal RP** (17.6.0+) - already in manifest
- ❌ **Cinemachine** - NOT REQUIRED (using custom controller)

---

## 🏗️ Scene Hierarchy After Setup
```
▼ Player (tag: Player)
    ├── CharacterController
    ├── PlayerMotor
    ├── PlayerLook
    ├── BodyVisual (capsule mesh, semi-transparent)
    ▼ Main Camera (child of Player)
        ├── Camera
        ├── AudioListener
        ├── UniversalAdditionalCameraData
        └── ThirdPersonCameraController

▼ Ground (layer: Ground)
    ├── MeshFilter (Plane)
    ├── MeshRenderer (dark material)
    └── BoxCollider

▼ Directional Light
▼ Global Volume
```

---

## 📝 Next: Phase 2
After validating Phase 1 works:
- Weapon system (shoot, reload, ammo)
- Crosshair UI
- Hit detection
- Enemy targets