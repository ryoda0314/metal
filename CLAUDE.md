# METAL-TEST Unity Project

## Overview
Unity 6 VR project supporting both SteamVR (PC) and Meta Quest (Android).
Cross-platform via `#if UNITY_ANDROID` / `#if !UNITY_ANDROID` conditional compilation.

## Key Architecture

### Platform Switching
- **SteamVR (PC)**: `Valve.VR.InteractionSystem` — `Hand`, `Interactable`, `Throwable`
- **Meta Quest (Android)**: `XR Interaction Toolkit 3.0.7` — `XRGrabInteractable`, `ActionBasedController`, `XRDirectInteractor`
- Scripts use `#if UNITY_ANDROID` blocks to switch implementations. Do NOT wrap entire files in `#if`.

### XR Rig (Meta Quest)
- `Assets/MetaQuest/XRRigSetup.cs` — Runtime XR Origin builder
  - Creates XR Origin, Camera, Controllers, Interactors at Start()
  - `cameraHeightOffset` field adjusts viewpoint height (default -1.0)
  - Auto-detects SteamVR glove FBX models via `OnValidate()` in Editor
  - `ActionBasedController` with input bindings: `/grip`, `/trigger`, `/devicePosition`, `/deviceRotation`
  - `XRDirectInteractor` + trigger SphereCollider on same GameObject as controller (XRI 3.x requirement)
  - Optional hand physics (kinematic Rigidbody + non-trigger colliders for pushing objects)

### Hand Animation
- `Assets/MetaQuest/XRHandAnimator.cs` — Finger curl from controller input
  - Uses SteamVR `ReferencePose_OpenHand` / `ReferencePose_Fist` quaternion data
  - **Delta rotation method**: saves FBX rest pose, applies `Inverse(open) * fist` delta via Slerp
  - Do NOT touch Root/Wrist bones (causes hand collapse)
  - Grip → middle/ring/pinky, Trigger → index, ThumbstickTouch → thumb

### Grab System
- `Assets/CubeController.cs` — Precision grab (Instantaneous, no throw)
- `Assets/CubeController2.cs` — Physics grab (VelocityTracking, throwOnDetach)
- `Assets/MetalSwirlPolisher.cs` — Metal polishing tool (~1400 lines, cross-platform)
- **子オブジェクトに `AddComponent<XRGrabInteractable>()` 禁止** — `[RequireComponent(typeof(Rigidbody))]` により Rigidbody が自動追加され、親子間で物理が壊れる。子オブジェクトでは `GetComponentInParent<XRGrabInteractable>()` で親のものを使う（SteamVR の `GetComponentInParent<Interactable>()` と同パターン）

### Mixed Reality Passthrough (Meta Quest)
- `enablePassthrough` bool on XRRigSetup (default true)
- **OpenXR Features** (Project Settings → XR → OpenXR → Android):
  - `PassthroughBlendFeature` (`Assets/MetaQuest/PassthroughBlendFeature.cs`) — `SetEnvironmentBlendMode(AlphaBlend)` in `OnSessionCreate`
  - `PassthroughBlendFeatureEnabler` (`Assets/MetaQuest/Editor/`) — Editor auto-enable script
  - `ARCameraFeature` (Meta Quest: Camera) — creates/starts passthrough layer via `XR_FB_passthrough`
  - `ARSessionFeature` (Meta Quest: Session) — must be enabled for manifest hook to inject `com.oculus.feature.PASSTHROUGH`
- **Runtime components** (added by XRRigSetup when `enablePassthrough`):
  - `ARSession` — AR subsystem lifecycle
  - `ARCameraManager` — starts `MetaOpenXRCameraSubsystem` which calls `xrPassthroughStartFB`
  - Do NOT add `ARCameraBackground` — `supportsCameraImage=false` so it renders black overlay
  - Camera: `ClearFlags.SolidColor`, `backgroundColor.a = 0`
- **URP settings** (`Assets/Settings/Mobile_RPAsset.asset`):
  - `m_AllowPostProcessAlphaOutput: 1` — ポストプロセスがアルファを保持（0だとα=1に上書きされパススルー不可）
  - `m_PrefilterAlphaOutput: 1` — アルファ出力シェーダーバリアントのストリップ防止
  - `m_SupportsHDR: 0` — HDRフォーマットがアルファを壊す問題を回避
  - ⚠️ **Unity EditorでURP AssetをInspectorで触るとこれらが勝手にリセットされる。修正はYAMLを直接編集すること**
- **地面非表示**: `HideGroundForPassthrough()` でシーン内のPlaneメッシュRendererを無効化（白い地面がパススルーを遮るため）
- **AndroidManifest**: `com.oculus.feature.PASSTHROUGH` は `ModifyAndroidManifestMeta` ビルドフックが自動注入（ARSessionFeature有効時）。初回インストール時は `adb uninstall` してからインストールしないとOS側のキャッシュで `App Enabled for PT: 0` になる場合がある

### Input Bindings (Quest OpenXR)
- Use `/grip` (float) NOT `gripPress` (doesn't exist on Quest)
- Use `/trigger` (float) NOT `triggerPress`
- `InputActionType.Button` with float binding auto-presses at threshold 0.5

## Packages
- `com.unity.xr.interaction.toolkit`: 3.0.7
- `com.unity.xr.meta-openxr`: 2.1.0
- `com.unity.xr.openxr`: 1.15.1
- `com.unity.xr.hands`: 1.5.0
- `com.unity.inputsystem`: 1.14.1
- `com.valvesoftware.unity.openvr`: local SteamVR package

## SteamVR Hand Model
- FBX: `Assets/SteamVR/Models/vr_glove_{left,right}_model_slim.fbx`
- 31 bones per hand: Root, Wrist, 5 fingers x (meta, 0, 1, 2, tip, aux)
- Bone naming: `finger_{thumb,index,middle,ring,pinky}_{0,1,2}_{l,r}`
- Pose data: `Assets/SteamVR/Resources/ReferencePose_OpenHand.asset`, `ReferencePose_Fist.asset`

## Important Notes
- URP project — SteamVR materials may need `EnsureURPMaterials()` to avoid pink rendering
- `CleanupSteamVRComponents()` strips Valve.VR scripts from instantiated hand models on Quest
- Scene: `Assets/Scenes/SampleScene.unity` — XR Rig, Player (SteamVR), Cube, Sphere, Plane

### Scene Hierarchy: kenma.blend (研磨ツール)
- **kenma.blend** (parent): Rigidbody, CubeController2, Interactable, CapsuleCollider×2, BoxCollider×3
- **kenmamen** (child): MeshCollider, MetalSwirlPolisher, CylinderPolisher
- kenmamen は親の Rigidbody / XRGrabInteractable / Interactable を `GetComponentInParent` で参照する。子に独自の Rigidbody や XRGrabInteractable を追加してはいけない
