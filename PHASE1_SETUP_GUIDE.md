# 🟢 FASE 1: El Núcleo (Movimiento y Cámara) - Guía de Configuración

## Resumen
Configuración completa de movimiento en tercera persona (TPS) con:
- **PlayerMotor**: Movimiento con CharacterController (WASD, gravedad, salto, rotación hacia dirección de movimiento)
- **PlayerLook**: Rotación de cámara con ratón (clamp vertical -85° a +85°)
- **ThirdPersonCameraController**: Cámara over-the-shoulder con colisión anti-paredes (sin Cinemachine requerido)

---

## 📁 Archivos Creados

| Archivo | Descripción |
|---------|-------------|
| `Assets/Scripts/Player/PlayerMotor.cs` | Movimiento del personaje |
| `Assets/Scripts/Player/PlayerLook.cs` | Rotación de cámara (mouse) |
| `Assets/Scripts/Camera/ThirdPersonCameraController.cs` | Controlador de cámara 3ª persona con colisión |
| `Assets/Scripts/Camera/CinemachineCameraCollision.cs` | Alternativa para Cinemachine (opcional) |
| `Assets/Scripts/Editor/SetupPhase1.cs` | **Script de configuración automática** |

---

## 🚀 Configuración Automática (Recomendado)

### En el Editor de Unity:
1. Abre el proyecto `CyberShooter`
2. Ve a **Tools > CyberShooter > Setup Phase 1 (TPS Core)**
3. ¡Listo! Se crea automáticamente:
   - Plano de suelo (100x100 unidades)
   - Player con CharacterController en (0, 1.5, 0)
   - Cámara como hija del Player (offset over-the-shoulder)
   - Scripts PlayerMotor, PlayerLook, ThirdPersonCameraController configurados
   - Input Actions vinculados

### Validar configuración:
**Tools > CyberShooter > Validate Phase 1 Setup**

---

## ⚙️ Configuración Manual (si prefieres hacerlo paso a paso)

### 1. Crear el Suelo
```
GameObject > 3D Object > Plane
- Name: "Ground"
- Position: (0, 0, 0)
- Scale: (10, 1, 10)
- Layer: "Ground" (crear nuevo layer si no existe)
```

### 2. Crear el Player
```
GameObject > Create Empty
- Name: "Player"
- Tag: "Player"
- Position: (0, 1.5, 0)

Add Component > CharacterController:
- Radius: 0.3
- Height: 1.8
- Center: (0, 0.9, 0)
- Slope Limit: 45
- Step Offset: 0.3
- Skin Width: 0.08

Add Component > PlayerMotor (script)
Add Component > PlayerLook (script)
```

### 3. Configurar la Cámara
```
Seleccionar "Main Camera" (o crear nueva: GameObject > Camera)
- Hacerla HIJA de "Player" (arrastrar en Hierarchy)
- Transform local:
  - Position: (0.5, 1.5, -3)  ← Offset over-the-shoulder (ligeramente a la derecha)
  - Rotation: (0, 0, 0)
  - Scale: (1, 1, 1)

Camera settings:
- Clear Flags: Skybox
- Field of View: 60
- Near: 0.3, Far: 1000
- HDR: ✓
- MSAA: ✓

Add Component > ThirdPersonCameraController (script)
```

### 4. Vincular Referencias en Inspector

#### PlayerMotor (en Player):
- **Character Controller**: (arrastrar el CharacterController del Player)
- **Camera Transform**: (arrastrar Main Camera)
- **Input Actions**: (arrastrar Assets/InputSystem_Actions.inputactions)
- **Action Map Name**: "Player"
- **Move Action Name**: "Move"
- **Jump Action Name**: "Jump"
- **Sprint Action Name**: "Sprint"
- **Walk Speed**: 4
- **Sprint Speed**: 8
- **Rotation Speed**: 15
- **Gravity**: -9.81
- **Jump Height**: 1.5
- **Ground Layer**: Ground + Default

#### PlayerLook (en Player):
- **Player Body**: (arrastrar el Player)
- **Camera Transform**: (arrastrar Main Camera)
- **Input Actions**: (arrastrar Assets/InputSystem_Actions.inputactions)
- **Look Action Name**: "Look"
- **Look Sensitivity**: 1
- **Invert Y**: false
- **Min Pitch**: -85
- **Max Pitch**: 85
- **Lock Cursor On Start**: ✓

#### ThirdPersonCameraController (en Main Camera):
- **Target**: (arrastrar Player)
- **Offset**: (0.5, 1.5, -3)
- **Rotation Speed**: 10
- **Min Pitch**: -30
- **Max Pitch**: 60
- **Collision Layers**: Default + Ground
- **Camera Radius**: 0.3
- **Min Distance**: 0.5
- **Collision Smooth Time**: 0.1
- **Input Actions**: (arrastrar Assets/InputSystem_Actions.inputactions)
- **Look Action Name**: "Look"

---

## 🎮 Controles (Input System)

| Acción | Tecla/Input | Descripción |
|--------|-------------|-------------|
| **Move** | WASD / Left Stick | Movimiento relativo a la cámara |
| **Look** | Mouse Delta / Right Stick | Rotación cámara (yaw/pitch) |
| **Jump** | Space / A (gamepad) | Saltar |
| **Sprint** | Left Shift / L3 | Correr |

---

## ✅ Checklist de Verificación

- [ ] Player tiene tag "Player"
- [ ] CharacterController configurado (radius 0.3, height 1.8)
- [ ] PlayerMotor y PlayerLook añadidos al Player
- [ ] Main Camera es HIJA del Player
- [ ] ThirdPersonCameraController en Main Camera
- [ ] InputSystem_Actions referenciado en los 3 scripts
- [ ] Ground tiene layer "Ground" y collider
- [ ] Cursor se bloquea al entrar en Play Mode
- [ ] WASD mueve relativo a la cámara
- [ ] Mouse rota cámara (Y clamp -85° a +85°)
- [ ] Space salta
- [ ] Shift corre
- [ ] Cámara no atraviesa paredes (colisión funcional)

---

## 🔧 Troubleshooting

### "El personaje no se mueve"
- Verifica que `InputSystem_Actions.inputactions` esté referenciado en PlayerMotor
- Verifica que el Action Map sea "Player" y la acción "Move" exista
- Verifica que `cameraTransform` apunte a la Main Camera

### "La cámara no rota con el mouse"
- Verifica PlayerLook: `cameraTransform` y `playerBody` asignados
- Verifica Input Actions: acción "Look" tipo Value/Vector2 con binding `<Mouse>/delta`
- Verifica que `lockCursorOnStart` esté activado

### "La cámara atraviesa paredes"
- Verifica ThirdPersonCameraController: `collisionLayers` incluye Default/Ground
- Verifica `cameraRadius` = 0.3, `minDistance` = 0.5
- El script usa SphereCast desde el target hacia la cámara

### "El personaje gira raro / no mira hacia donde se mueve"
- PlayerMotor rota el cuerpo hacia la dirección de movimiento (input WASD relativo a cámara)
- PlayerLook rota el cuerpo en Yaw (mouse X) y la cámara en Pitch (mouse Y)
- Si hay conflicto, asegúrate de que **solo PlayerMotor** rota el transform del Player en Y
- PlayerLook debe rotar `playerBody` (el Player) en Yaw, no el transform directo si PlayerMotor ya lo hace

### "Error de compilación: InputSystem no encontrado"
- Window > Package Manager > Unity Registry > Input System > Install
- O usa el Input Manager clásico (cambiar los scripts para usar `Input.GetAxis`)

---

## 📝 Notas de Implementación

### PlayerMotor.cs
- Usa `CharacterController.Move()` (no Rigidbody)
- Gravedad personalizada con coyote time (0.1s) y jump buffer (0.1s)
- Rotación suave hacia dirección de movimiento (Slerp, 15 speed)
- Soporte para sprint (Shift) y movimiento relativo a cámara

### PlayerLook.cs
- Yaw (mouse X) rota el `playerBody` (el Player completo)
- Pitch (mouse Y) rota solo la `cameraTransform` (clamp -85 a +85)
- Soporte para invertir Y y raw mouse input
- Bloqueo/desbloqueo cursor con Escape / Click

### ThirdPersonCameraController.cs
- **Sin dependencia de Cinemachine** - funciona standalone
- Orbita alrededor del target usando yaw/pitch del mouse
- Colisión con SphereCast (radio 0.3m) - empuja cámara hacia adelante al chocar
- Suavizado de distancia con `SmoothDamp` (0.1s)
- Offset configurable para estilo over-the-shoulder

---

## 🎯 Próximos Pasos (Fase 2)

Una vez validada la Fase 1:
1. **Fase 2**: Sistema de armas (Weapon Manager, disparo, recarga)
2. **Fase 3**: Enemigos y IA básica
3. **Fase 4**: UI, HUD, salud, munición
4. **Fase 5**: Nivel completo, pickups, progression

---

## 🔗 Archivos de Referencia

- `Assets/InputSystem_Actions.inputactions` - Actions definidas (Move, Look, Jump, Sprint, Attack, Interact, etc.)
- `Assets/Settings/PC_RPAsset.asset` - URP Asset (HDR enabled)
- `Assets/Scripts/Player/PlayerMotor.cs` - Movimiento completo
- `Assets/Scripts/Player/PlayerLook.cs` - Look completo
- `Assets/Scripts/Camera/ThirdPersonCameraController.cs` - Cámara 3ª persona con colisión