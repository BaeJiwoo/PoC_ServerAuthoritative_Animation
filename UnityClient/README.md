# Bob snapshot client

Unity 6000.3.23f1. Open this project and use **POC > Run Server-Authoritative Demo**.
Start the .NET server first; default endpoint: `ws://127.0.0.1:5000/ws`.

The client sends only normalized WASD `{x,z}` input at 20 Hz. It displays Bob from
the user's StickManFight project using the full server-authored skeleton.
The generated Resources/ServerCharacter.prefab contains a skinned mesh and no
Animator or Animation components. All runtime pose changes come from snapshots.

Presentation buffers snapshots by 100 ms and interpolates every render frame:
Vector3.Lerp for root/bone positions and scales, Quaternion.Slerp for rotations.
World-space hitboxes use the same snapshot pair and interpolation fraction.
This adds display latency; authoritative server poses are unchanged. During large
turns, independently interpolated world boxes can differ slightly from the skinned pose.
The client holds the latest received pose on buffer underrun, without extrapolation.
Reconnects, changed rigs and snapshot gaps over one second reset the buffer.
The HUD shows `Interpolation: 100 ms | Lerp / Slerp`.

Each snapshot contains:

- root: world position, rotation, yawDegrees
- isMoving, animationPhase, serverTime
- skeletonId: must match the generated character prefab
- bones: path, localPosition, localRotation, localScale, worldPosition, worldRotation
- hitboxes: name, center, rotation, size (world space)

Head/torso, upper/lower arms and upper/lower legs are displayed as 10 colored
wire boxes. The root marker and forward arrow show the server-facing direction.

To regenerate Bob's walk, use **POC > Bake Bob WalkForward for Server**.
To select another Mixamo clip, use **POC > Bake Imported Walk Clip...**.
This evaluates the source Humanoid animation only in the editor and produces both
the server JSON and the matching display prefab. Rebuild/restart the server afterward.

The source FBX files and metadata were copied from StickManFight without modifying
that project. The generated white material works with this POC's built-in renderer.
The original TPS controller, weapon rig and Blend Trees are not used.

See the repository root README for bake commands, tests and protocol details.

