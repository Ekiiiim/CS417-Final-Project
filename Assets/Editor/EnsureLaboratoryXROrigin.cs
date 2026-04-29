using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

[InitializeOnLoad]
public static class EnsureLaboratoryXROrigin
{
    private const string LaboratoryScenePath = "Assets/Laboratory/Scenes/Laboratory.unity";
    private const string XrOriginPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.3.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string LaboratoryRootName = "laboratory";
    private const string ExpansionRootName = "__LaboratoryExpansion";
    private const string MinimapCameraName = "__MinimapCamera";
    private const string LaboratoryLeftName = "laboratory_left";
    private const string LaboratoryRightName = "laboratory_right";
    private const string LaboratoryTopName = "laboratory_top";
    private const string LaboratoryTopRightName = "laboratory_top_right";
    private const string HubPlatformName = "__HubPlatform";
    private const string CenterBridgeName = "__CenterBridge";
    private const string FenceNorthName = "__FenceNorth";
    private const string FenceSouthName = "__FenceSouth";
    private const string FenceEastName = "__FenceEast";
    private const string FenceWestName = "__FenceWest";
    private const string HudCanvasName = "__LaboratoryHUD";
    private const string MinimapPanelName = "__MinimapPanel";
    private const string EnergyPanelName = "__EnergyPanel";
    private const float HorizontalModuleGap = 12f;
    private const float ForwardModuleGap = 4f;

    static EnsureLaboratoryXROrigin()
    {
        EditorApplication.delayCall += EnsureRigExists;
        EditorSceneManager.sceneOpened += OnSceneOpened;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path != LaboratoryScenePath)
        {
            return;
        }

        EditorApplication.delayCall += EnsureRigExists;
    }

    [MenuItem("Tools/Laboratory/Rebuild Expanded Layout")]
    private static void RebuildExpandedLayoutMenu()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != LaboratoryScenePath)
        {
            EditorUtility.DisplayDialog(
                "Laboratory Scene Required",
                "Open Assets/Laboratory/Scenes/Laboratory.unity first, then run this command again.",
                "OK");
            return;
        }

        GameObject laboratoryRoot = FindPrimaryLaboratoryRoot(activeScene, null);
        if (laboratoryRoot == null)
        {
            EditorUtility.DisplayDialog(
                "Laboratory Root Missing",
                "Could not find the root GameObject named 'laboratory' in the active scene.",
                "OK");
            return;
        }

        GameObject expansionRoot = GameObject.Find(ExpansionRootName);
        if (expansionRoot != null)
        {
            Undo.DestroyObjectImmediate(expansionRoot);
        }

        GameObject minimapCamera = GameObject.Find(MinimapCameraName);
        if (minimapCamera != null)
        {
            Undo.DestroyObjectImmediate(minimapCamera);
        }

        ForceBuildExpandedWorld(activeScene, laboratoryRoot);
        EnsureMinimap(activeScene);
        EnsureHud(activeScene);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        GameObject rebuiltExpansionRoot = GameObject.Find(ExpansionRootName);
        int childCount = rebuiltExpansionRoot != null ? rebuiltExpansionRoot.transform.childCount : 0;
        Debug.Log($"Laboratory expansion rebuilt. Expansion children: {childCount}");
    }

    [MenuItem("Tools/Laboratory/Rebuild Expanded Layout", true)]
    private static bool ValidateRebuildExpandedLayoutMenu()
    {
        return SceneManager.GetActiveScene().path == LaboratoryScenePath;
    }

    private static void ForceBuildExpandedWorld(Scene activeScene, GameObject laboratoryRoot)
    {
        Bounds labBounds = CalculateWorldBounds(laboratoryRoot.transform);
        if (labBounds.size == Vector3.zero)
        {
            return;
        }

        GameObject expansionRoot = new GameObject(ExpansionRootName);
        Undo.RegisterCreatedObjectUndo(expansionRoot, "Create Laboratory Expansion");
        SceneManager.MoveGameObjectToScene(expansionRoot, activeScene);

        float moduleWidth = Mathf.Max(8f, labBounds.size.x);
        float moduleDepth = Mathf.Max(8f, labBounds.size.z);
        float horizontalSpacing = moduleWidth + HorizontalModuleGap;
        float forwardSpacing = moduleDepth + ForwardModuleGap;

        CreateLabCloneForced(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(-horizontalSpacing, 0f, 0f), LaboratoryLeftName);
        CreateLabCloneForced(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(horizontalSpacing, 0f, 0f), LaboratoryRightName);
        CreateLabCloneForced(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(0f, 0f, forwardSpacing), LaboratoryTopName);
        CreateLabCloneForced(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(horizontalSpacing, 0f, forwardSpacing), LaboratoryTopRightName);

        EnsureExteriorPlatform(activeScene, expansionRoot.transform, laboratoryRoot.transform.position, labBounds, horizontalSpacing, forwardSpacing);
    }

    private static void CreateLabCloneForced(Scene activeScene, GameObject sourceRoot, Transform parent, Vector3 offset, string cloneName)
    {
        GameObject clone = Object.Instantiate(sourceRoot);
        Undo.RegisterCreatedObjectUndo(clone, "Duplicate Laboratory Module");
        clone.name = cloneName;
        SceneManager.MoveGameObjectToScene(clone, activeScene);
        clone.transform.SetParent(parent);
        clone.transform.position = sourceRoot.transform.position + offset;
        clone.transform.rotation = sourceRoot.transform.rotation;
        clone.transform.localScale = sourceRoot.transform.localScale;

        Camera[] cloneCameras = clone.GetComponentsInChildren<Camera>(true);
        foreach (Camera cloneCamera in cloneCameras)
        {
            Object.DestroyImmediate(cloneCamera.gameObject);
        }

        Light[] cloneLights = clone.GetComponentsInChildren<Light>(true);
        foreach (Light cloneLight in cloneLights)
        {
            Object.DestroyImmediate(cloneLight.gameObject);
        }
    }

    private static void EnsureRigExists()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying || Application.isPlaying)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path != LaboratoryScenePath)
        {
            return;
        }

        bool sceneChanged = false;
        XROrigin existingRig = Object.FindFirstObjectByType<XROrigin>();
        if (existingRig == null)
        {
            GameObject xrOriginPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrOriginPrefabPath);
            if (xrOriginPrefab == null)
            {
                Debug.LogWarning("Could not find XR Origin prefab to place in the Laboratory scene.");
                return;
            }

            Camera sourceCamera = FindSourceCamera();
            GameObject instance = PrefabUtility.InstantiatePrefab(xrOriginPrefab, activeScene) as GameObject;
            if (instance == null)
            {
                return;
            }

            Undo.RegisterCreatedObjectUndo(instance, "Create Laboratory XR Origin");

            existingRig = instance.GetComponent<XROrigin>();
            if (existingRig != null && existingRig.Camera != null)
            {
                PlaceRigFromSourceCamera(instance.transform, existingRig.Camera.transform, sourceCamera);
            }
            else if (sourceCamera != null)
            {
                instance.transform.SetPositionAndRotation(
                    sourceCamera.transform.position,
                    Quaternion.Euler(0f, sourceCamera.transform.eulerAngles.y, 0f));
            }

            DisableLegacySceneCameras(existingRig != null ? existingRig.Camera : null);
            Selection.activeGameObject = instance;
            sceneChanged = true;
        }

        if (existingRig != null)
        {
            sceneChanged |= EnsureRigGameplaySetup(activeScene, existingRig);
            bool expandedWorld = EnsureExpandedWorld(activeScene, existingRig);
            sceneChanged |= expandedWorld;
            if (expandedWorld)
            {
                sceneChanged |= EnsureEnvironmentColliders(activeScene, existingRig.transform);
                sceneChanged |= EnsureDoorSetup(activeScene);
            }
            sceneChanged |= EnsureMinimap(activeScene);
            sceneChanged |= EnsureHud(activeScene);
        }

        if (sceneChanged && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode && !Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
        }
    }

    private static Camera FindSourceCamera()
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Camera cameraComponent in cameras)
        {
            if (!cameraComponent.gameObject.activeInHierarchy || !cameraComponent.enabled)
            {
                continue;
            }

            if (cameraComponent.CompareTag("MainCamera"))
            {
                return cameraComponent;
            }
        }

        return cameras.Length > 0 ? cameras[0] : null;
    }

    private static void PlaceRigFromSourceCamera(Transform rigTransform, Transform rigCameraTransform, Camera sourceCamera)
    {
        if (sourceCamera == null)
        {
            rigTransform.position = Vector3.zero;
            rigTransform.rotation = Quaternion.identity;
            return;
        }

        rigTransform.rotation = Quaternion.Euler(0f, sourceCamera.transform.eulerAngles.y, 0f);
        Vector3 cameraOffset = rigCameraTransform.position - rigTransform.position;
        rigTransform.position = sourceCamera.transform.position - cameraOffset;
    }

    private static void DisableLegacySceneCameras(Camera xrCamera)
    {
        Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Camera cameraComponent in cameras)
        {
            if (cameraComponent != null && cameraComponent.gameObject.name == MinimapCameraName)
            {
                continue;
            }

            if (xrCamera != null && cameraComponent == xrCamera)
            {
                continue;
            }

            cameraComponent.gameObject.SetActive(false);
        }
    }

    private static bool EnsureRigGameplaySetup(Scene activeScene, XROrigin xrOrigin)
    {
        bool changed = false;

        if (xrOrigin.GetComponent<XRPlayerEnergy>() == null)
        {
            Undo.AddComponent<XRPlayerEnergy>(xrOrigin.gameObject);
            changed = true;
        }

        if (xrOrigin.GetComponent<XRPlayerCollisionHandler>() == null)
        {
            Undo.AddComponent<XRPlayerCollisionHandler>(xrOrigin.gameObject);
            changed = true;
        }

        if (xrOrigin.GetComponent<XREditorDesktopLocomotion>() == null)
        {
            Undo.AddComponent<XREditorDesktopLocomotion>(xrOrigin.gameObject);
            changed = true;
        }

        if (xrOrigin.GetComponent<XRMoveSpeedModifier>() == null)
        {
            Undo.AddComponent<XRMoveSpeedModifier>(xrOrigin.gameObject);
            changed = true;
        }

        changed |= EnsureEnvironmentColliders(activeScene, xrOrigin.transform);
        changed |= EnsureDoorSetup(activeScene);
        return changed;
    }

    private static bool EnsureExpandedWorld(Scene activeScene, XROrigin xrOrigin)
    {
        GameObject laboratoryRoot = FindPrimaryLaboratoryRoot(activeScene, xrOrigin != null ? xrOrigin.transform : null);
        if (laboratoryRoot == null)
        {
            return false;
        }

        Bounds labBounds = CalculateWorldBounds(laboratoryRoot.transform);
        if (labBounds.size == Vector3.zero)
        {
            return false;
        }

        bool changed = false;
        GameObject expansionRoot = GameObject.Find(ExpansionRootName);
        if (expansionRoot == null)
        {
            expansionRoot = new GameObject(ExpansionRootName);
            Undo.RegisterCreatedObjectUndo(expansionRoot, "Create Laboratory Expansion");
            SceneManager.MoveGameObjectToScene(expansionRoot, activeScene);
            changed = true;
        }
        else if (!HasExpectedExpansionLayout(expansionRoot.transform))
        {
            ClearExpansionRoot(expansionRoot.transform);
            changed = true;
        }

        changed |= RemoveLegacyCorridorObjects(expansionRoot.transform);

        float moduleWidth = Mathf.Max(8f, labBounds.size.x);
        float moduleDepth = Mathf.Max(8f, labBounds.size.z);
        float horizontalSpacing = moduleWidth + HorizontalModuleGap;
        float forwardSpacing = moduleDepth + ForwardModuleGap;

        changed |= EnsureLabClone(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(-horizontalSpacing, 0f, 0f), LaboratoryLeftName);
        changed |= EnsureLabClone(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(horizontalSpacing, 0f, 0f), LaboratoryRightName);
        changed |= EnsureLabClone(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(0f, 0f, forwardSpacing), LaboratoryTopName);
        changed |= EnsureLabClone(activeScene, laboratoryRoot, expansionRoot.transform, new Vector3(horizontalSpacing, 0f, forwardSpacing), LaboratoryTopRightName);
        changed |= EnsureExteriorPlatform(activeScene, expansionRoot.transform, laboratoryRoot.transform.position, labBounds, horizontalSpacing, forwardSpacing);
        return changed;
    }

    private static bool HasExpectedExpansionLayout(Transform expansionRoot)
    {
        if (expansionRoot == null)
        {
            return false;
        }

        return expansionRoot.Find(LaboratoryLeftName) != null &&
               expansionRoot.Find(LaboratoryRightName) != null &&
               expansionRoot.Find(LaboratoryTopName) != null &&
               expansionRoot.Find(LaboratoryTopRightName) != null &&
               expansionRoot.Find(HubPlatformName) != null &&
               expansionRoot.Find(CenterBridgeName) != null;
    }

    private static void ClearExpansionRoot(Transform expansionRoot)
    {
        if (expansionRoot == null)
        {
            return;
        }

        List<GameObject> childrenToRemove = new List<GameObject>();
        for (int index = 0; index < expansionRoot.childCount; index++)
        {
            childrenToRemove.Add(expansionRoot.GetChild(index).gameObject);
        }

        foreach (GameObject child in childrenToRemove)
        {
            Object.DestroyImmediate(child);
        }
    }

    private static bool RemoveLegacyCorridorObjects(Transform expansionRoot)
    {
        if (expansionRoot == null)
        {
            return false;
        }

        List<GameObject> childrenToRemove = new List<GameObject>();
        for (int index = 0; index < expansionRoot.childCount; index++)
        {
            Transform child = expansionRoot.GetChild(index);
            if (child.name.StartsWith("__Corridor"))
            {
                childrenToRemove.Add(child.gameObject);
            }
        }

        foreach (GameObject child in childrenToRemove)
        {
            Object.DestroyImmediate(child);
        }

        return childrenToRemove.Count > 0;
    }

    private static GameObject FindPrimaryLaboratoryRoot(Scene activeScene, Transform xrOriginTransform)
    {
        foreach (GameObject rootObject in activeScene.GetRootGameObjects())
        {
            if (rootObject == null || rootObject.name != LaboratoryRootName)
            {
                continue;
            }

            if (xrOriginTransform != null && rootObject.transform == xrOriginTransform)
            {
                continue;
            }

            return rootObject;
        }

        return null;
    }

    private static bool EnsureLabClone(Scene activeScene, GameObject sourceRoot, Transform parent, Vector3 offset, string cloneName)
    {
        Transform existingChild = parent.Find(cloneName);
        if (existingChild != null)
        {
            existingChild.position = sourceRoot.transform.position + offset;
            existingChild.rotation = sourceRoot.transform.rotation;
            existingChild.localScale = sourceRoot.transform.localScale;
            return false;
        }

        GameObject clone = Object.Instantiate(sourceRoot);
        Undo.RegisterCreatedObjectUndo(clone, "Duplicate Laboratory Module");
        clone.name = cloneName;
        SceneManager.MoveGameObjectToScene(clone, activeScene);
        clone.transform.SetParent(parent);
        clone.transform.position = sourceRoot.transform.position + offset;
        clone.transform.rotation = sourceRoot.transform.rotation;
        clone.transform.localScale = sourceRoot.transform.localScale;

        Camera[] cloneCameras = clone.GetComponentsInChildren<Camera>(true);
        foreach (Camera cloneCamera in cloneCameras)
        {
            Object.DestroyImmediate(cloneCamera.gameObject);
        }

        Light[] cloneLights = clone.GetComponentsInChildren<Light>(true);
        foreach (Light cloneLight in cloneLights)
        {
            Object.DestroyImmediate(cloneLight.gameObject);
        }

        return true;
    }

    private static bool EnsureExteriorPlatform(Scene activeScene, Transform parent, Vector3 sourcePosition, Bounds labBounds, float horizontalSpacing, float forwardSpacing)
    {
        float platformWidth = Mathf.Max(24f, horizontalSpacing + labBounds.size.x + 8f);
        float platformDepth = Mathf.Max(24f, forwardSpacing + labBounds.size.z + 8f);
        float platformY = labBounds.min.y - 0.05f;
        Vector3 platformCenter = new Vector3(
            sourcePosition.x + horizontalSpacing * 0.5f,
            platformY,
            sourcePosition.z + forwardSpacing * 0.5f);

        bool changed = false;
        changed |= EnsurePlatformCube(activeScene, parent, HubPlatformName,
            platformCenter,
            new Vector3(platformWidth, 0.2f, platformDepth));

        changed |= EnsurePlatformCube(activeScene, parent, CenterBridgeName,
            new Vector3(sourcePosition.x + horizontalSpacing * 0.5f, platformY, sourcePosition.z + forwardSpacing * 0.5f),
            new Vector3(6f, 0.22f, 6f));

        changed |= EnsurePlatformFences(activeScene, parent, platformCenter, platformWidth, platformDepth, platformY);
        return changed;
    }

    private static bool EnsurePlatformFences(Scene activeScene, Transform parent, Vector3 platformCenter, float platformWidth, float platformDepth, float platformY)
    {
        bool changed = false;
        float fenceHeight = 1.8f;
        float fenceThickness = 0.28f;
        float halfWidth = platformWidth * 0.5f;
        float halfDepth = platformDepth * 0.5f;
        float fenceY = platformY + fenceHeight * 0.5f + 0.1f;

        changed |= EnsurePlatformCube(activeScene, parent, FenceNorthName,
            new Vector3(platformCenter.x, fenceY, platformCenter.z + halfDepth - fenceThickness * 0.5f),
            new Vector3(platformWidth, fenceHeight, fenceThickness));

        changed |= EnsurePlatformCube(activeScene, parent, FenceSouthName,
            new Vector3(platformCenter.x, fenceY, platformCenter.z - halfDepth + fenceThickness * 0.5f),
            new Vector3(platformWidth, fenceHeight, fenceThickness));

        changed |= EnsurePlatformCube(activeScene, parent, FenceEastName,
            new Vector3(platformCenter.x + halfWidth - fenceThickness * 0.5f, fenceY, platformCenter.z),
            new Vector3(fenceThickness, fenceHeight, platformDepth));

        changed |= EnsurePlatformCube(activeScene, parent, FenceWestName,
            new Vector3(platformCenter.x - halfWidth + fenceThickness * 0.5f, fenceY, platformCenter.z),
            new Vector3(fenceThickness, fenceHeight, platformDepth));

        return changed;
    }

    private static bool EnsurePlatformCube(Scene activeScene, Transform parent, string objectName, Vector3 position, Vector3 scale)
    {
        Transform existingChild = parent.Find(objectName);
        GameObject platform;
        bool changed = false;

        if (existingChild != null)
        {
            platform = existingChild.gameObject;
        }
        else
        {
            platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(platform, "Create Laboratory Platform");
            SceneManager.MoveGameObjectToScene(platform, activeScene);
            platform.name = objectName;
            platform.transform.SetParent(parent);
            changed = true;
        }

        platform.transform.position = position;
        platform.transform.localScale = scale;
        platform.isStatic = true;

        Renderer rendererComponent = platform.GetComponent<Renderer>();
        if (rendererComponent != null)
        {
            rendererComponent.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            rendererComponent.receiveShadows = true;
        }

        return changed;
    }

    private static bool EnsureMinimap(Scene activeScene)
    {
        Camera minimapCamera = null;
        foreach (GameObject rootObject in activeScene.GetRootGameObjects())
        {
            if (rootObject.name == MinimapCameraName)
            {
                minimapCamera = rootObject.GetComponent<Camera>();
                break;
            }
        }

        bool changed = false;
        if (minimapCamera == null)
        {
            GameObject minimapObject = new GameObject(MinimapCameraName);
            Undo.RegisterCreatedObjectUndo(minimapObject, "Create Minimap Camera");
            SceneManager.MoveGameObjectToScene(minimapObject, activeScene);
            minimapCamera = minimapObject.AddComponent<Camera>();
            changed = true;
        }

        if (minimapCamera.GetComponent<MinimapCameraFollow>() == null)
        {
            Undo.AddComponent<MinimapCameraFollow>(minimapCamera.gameObject);
            changed = true;
        }

        minimapCamera.gameObject.SetActive(true);
        minimapCamera.enabled = true;
        ConfigureMinimapCamera(minimapCamera);
        return changed;
    }

    private static bool EnsureHud(Scene activeScene)
    {
        GameObject hudRoot = null;
        foreach (GameObject rootObject in activeScene.GetRootGameObjects())
        {
            if (rootObject.name == HudCanvasName)
            {
                hudRoot = rootObject;
                break;
            }
        }

        bool changed = false;
        if (hudRoot == null)
        {
            hudRoot = new GameObject(HudCanvasName);
            Undo.RegisterCreatedObjectUndo(hudRoot, "Create Laboratory HUD");
            SceneManager.MoveGameObjectToScene(hudRoot, activeScene);
            hudRoot.AddComponent<Canvas>();
            hudRoot.AddComponent<CanvasScaler>();
            hudRoot.AddComponent<GraphicRaycaster>();
            changed = true;
        }

        Canvas canvas = hudRoot.GetComponent<Canvas>();
        CanvasScaler scaler = hudRoot.GetComponent<CanvasScaler>();
        GraphicRaycaster raycaster = hudRoot.GetComponent<GraphicRaycaster>();
        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
        }

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (raycaster != null)
        {
            raycaster.enabled = false;
        }

        changed |= EnsureMinimapOverlay(hudRoot.transform);
        changed |= EnsureEnergyOverlay(hudRoot.transform);
        changed |= EnsureHudController(hudRoot);
        return changed;
    }

    private static bool EnsureMinimapOverlay(Transform hudRoot)
    {
        bool changed = false;
        GameObject minimapPanel = EnsureUiPanel(hudRoot, MinimapPanelName, new Color(0.02f, 0.03f, 0.05f, 0.52f), ref changed);
        RectTransform panelRect = minimapPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.76f, 0.72f);
        panelRect.anchorMax = new Vector2(0.98f, 0.96f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject minimapFrame = EnsureUiPanel(minimapPanel.transform, "__MinimapFrame", new Color(0.8f, 0.86f, 0.92f, 0.2f), ref changed);
        RectTransform frameRect = minimapFrame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.03f, 0.03f);
        frameRect.anchorMax = new Vector2(0.97f, 0.97f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        GameObject markerRoot = EnsureUiElement(minimapPanel.transform, "__PlayerMarkerRoot", ref changed);
        RectTransform markerRect = markerRoot.GetComponent<RectTransform>();
        markerRect.anchorMin = new Vector2(0.5f, 0.5f);
        markerRect.anchorMax = new Vector2(0.5f, 0.5f);
        markerRect.sizeDelta = new Vector2(56f, 56f);
        markerRect.anchoredPosition = Vector2.zero;

        GameObject markerRing = EnsureUiPanel(markerRoot.transform, "__PlayerMarkerRing", new Color(1f, 1f, 1f, 0.92f), ref changed);
        RectTransform ringRect = markerRing.GetComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 0.5f);
        ringRect.anchorMax = new Vector2(0.5f, 0.5f);
        ringRect.sizeDelta = new Vector2(26f, 26f);
        ringRect.anchoredPosition = Vector2.zero;

        GameObject markerDot = EnsureUiPanel(markerRoot.transform, "__PlayerMarkerDot", new Color(0.35f, 0.05f, 0.08f, 0.98f), ref changed);
        RectTransform dotRect = markerDot.GetComponent<RectTransform>();
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        dotRect.sizeDelta = new Vector2(16f, 16f);
        dotRect.anchoredPosition = Vector2.zero;

        GameObject markerArrow = EnsureTextElement(markerRoot.transform, "__PlayerMarkerArrow", "▲", 36, TextAnchor.MiddleCenter, ref changed);
        Text markerArrowText = markerArrow.GetComponent<Text>();
        markerArrowText.color = new Color(1f, 0.22f, 0.22f, 1f);
        markerArrowText.fontStyle = FontStyle.Bold;
        RectTransform arrowRect = markerArrow.GetComponent<RectTransform>();
        arrowRect.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRect.sizeDelta = new Vector2(42f, 42f);
        arrowRect.anchoredPosition = new Vector2(0f, 17f);
        return changed;
    }

    private static bool EnsureEnergyOverlay(Transform hudRoot)
    {
        bool changed = false;
        GameObject energyPanel = EnsureUiPanel(hudRoot, EnergyPanelName, new Color(0.03f, 0.05f, 0.08f, 0.78f), ref changed);
        RectTransform energyPanelRect = energyPanel.GetComponent<RectTransform>();
        energyPanelRect.anchorMin = new Vector2(0.02f, 0.885f);
        energyPanelRect.anchorMax = new Vector2(0.285f, 0.98f);
        energyPanelRect.offsetMin = Vector2.zero;
        energyPanelRect.offsetMax = Vector2.zero;

        GameObject energyAccent = EnsureUiPanel(energyPanel.transform, "__EnergyAccent", new Color(0.18f, 0.88f, 0.95f, 0.95f), ref changed);
        RectTransform accentRect = energyAccent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0.02f, 0.12f);
        accentRect.anchorMax = new Vector2(0.04f, 0.88f);
        accentRect.offsetMin = Vector2.zero;
        accentRect.offsetMax = Vector2.zero;

        GameObject energyTitleObject = EnsureTextElement(energyPanel.transform, "__EnergyTitle", "ENERGY", 17, TextAnchor.UpperLeft, ref changed);
        Text energyTitle = energyTitleObject.GetComponent<Text>();
        energyTitle.color = new Color(0.76f, 0.84f, 0.91f, 0.95f);
        energyTitle.fontStyle = FontStyle.Bold;
        RectTransform titleRect = energyTitleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.08f, 0.62f);
        titleRect.anchorMax = new Vector2(0.45f, 0.9f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        GameObject energyLabelObject = EnsureTextElement(energyPanel.transform, "__EnergyLabel", "100/100", 18, TextAnchor.UpperRight, ref changed);
        Text energyValue = energyLabelObject.GetComponent<Text>();
        energyValue.color = Color.white;
        energyValue.fontStyle = FontStyle.Bold;
        RectTransform labelRect = energyLabelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.52f, 0.62f);
        labelRect.anchorMax = new Vector2(0.94f, 0.9f);
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        GameObject barBackground = EnsureUiPanel(energyPanel.transform, "__EnergyBarBackground", new Color(0.07f, 0.1f, 0.13f, 0.98f), ref changed);
        RectTransform barBgRect = barBackground.GetComponent<RectTransform>();
        barBgRect.anchorMin = new Vector2(0.08f, 0.18f);
        barBgRect.anchorMax = new Vector2(0.94f, 0.46f);
        barBgRect.offsetMin = Vector2.zero;
        barBgRect.offsetMax = Vector2.zero;

        GameObject barFrame = EnsureUiPanel(barBackground.transform, "__EnergyBarFrame", new Color(0.18f, 0.23f, 0.29f, 1f), ref changed);
        RectTransform frameRect = barFrame.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0f, 0f);
        frameRect.anchorMax = new Vector2(1f, 1f);
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;

        GameObject barFill = EnsureUiPanel(barBackground.transform, "__EnergyBarFill", new Color(0.18f, 0.88f, 0.95f, 1f), ref changed);
        RectTransform barFillRect = barFill.GetComponent<RectTransform>();
        barFillRect.anchorMin = new Vector2(0f, 0f);
        barFillRect.anchorMax = new Vector2(1f, 1f);
        barFillRect.offsetMin = new Vector2(5f, 5f);
        barFillRect.offsetMax = new Vector2(-5f, -5f);

        GameObject barShine = EnsureUiPanel(barFill.transform, "__EnergyBarShine", new Color(1f, 1f, 1f, 0.14f), ref changed);
        RectTransform shineRect = barShine.GetComponent<RectTransform>();
        shineRect.anchorMin = new Vector2(0f, 0.56f);
        shineRect.anchorMax = new Vector2(1f, 1f);
        shineRect.offsetMin = Vector2.zero;
        shineRect.offsetMax = Vector2.zero;
        return changed;
    }

    private static bool EnsureHudController(GameObject hudRoot)
    {
        LaboratoryHudController hudController = hudRoot.GetComponent<LaboratoryHudController>();
        bool changed = false;
        if (hudController == null)
        {
            hudController = Undo.AddComponent<LaboratoryHudController>(hudRoot);
            changed = true;
        }

        RectTransform markerRoot = FindUiRectTransform(hudRoot.transform, "__PlayerMarkerRoot");
        RectTransform energyFill = FindUiRectTransform(hudRoot.transform, "__EnergyBarFill");
        Image energyFillImage = energyFill != null ? energyFill.GetComponent<Image>() : null;
        Text energyLabel = FindUiText(hudRoot.transform, "__EnergyLabel");

        var serializedObject = new SerializedObject(hudController);
        serializedObject.FindProperty("playerMarkerRoot").objectReferenceValue = markerRoot;
        serializedObject.FindProperty("energyFill").objectReferenceValue = energyFill;
        serializedObject.FindProperty("energyFillImage").objectReferenceValue = energyFillImage;
        serializedObject.FindProperty("energyLabel").objectReferenceValue = energyLabel;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        return changed;
    }

    private static GameObject EnsureUiElement(Transform parent, string objectName, ref bool changed)
    {
        Transform existingChild = parent.Find(objectName);
        if (existingChild != null)
        {
            return existingChild.gameObject;
        }

        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(uiObject, $"Create {objectName}");
        uiObject.transform.SetParent(parent, false);
        changed = true;
        return uiObject;
    }

    private static GameObject EnsureUiPanel(Transform parent, string objectName, Color color, ref bool changed)
    {
        GameObject panelObject = EnsureUiElement(parent, objectName, ref changed);
        Image image = panelObject.GetComponent<Image>();
        if (image == null)
        {
            image = Undo.AddComponent<Image>(panelObject);
            changed = true;
        }

        image.color = color;
        image.raycastTarget = false;
        return panelObject;
    }

    private static GameObject EnsureTextElement(Transform parent, string objectName, string textValue, int fontSize, TextAnchor alignment, ref bool changed)
    {
        GameObject textObject = EnsureUiElement(parent, objectName, ref changed);
        Text text = textObject.GetComponent<Text>();
        if (text == null)
        {
            text = Undo.AddComponent<Text>(textObject);
            changed = true;
        }

        text.text = textValue;
        Font builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (builtinFont != null)
        {
            text.font = builtinFont;
        }
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return textObject;
    }

    private static RectTransform FindUiRectTransform(Transform root, string objectName)
    {
        Transform child = FindChildRecursive(root, objectName);
        return child != null ? child.GetComponent<RectTransform>() : null;
    }

    private static Text FindUiText(Transform root, string objectName)
    {
        Transform child = FindChildRecursive(root, objectName);
        return child != null ? child.GetComponent<Text>() : null;
    }

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        for (int index = 0; index < root.childCount; index++)
        {
            Transform child = root.GetChild(index);
            if (child.name == objectName)
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static void ConfigureMinimapCamera(Camera minimapCamera)
    {
        if (minimapCamera == null)
        {
            return;
        }

        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = 12f;
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.08f, 0.11f, 0.14f, 1f);
        minimapCamera.nearClipPlane = 0.01f;
        minimapCamera.farClipPlane = 200f;
        minimapCamera.rect = new Rect(0.76f, 0.72f, 0.22f, 0.24f);
        minimapCamera.depth = 10f;
        minimapCamera.allowHDR = false;
        minimapCamera.allowMSAA = true;
        minimapCamera.cullingMask = ~0;
        minimapCamera.targetDisplay = 0;
    }

    private static bool EnsureEnvironmentColliders(Scene activeScene, Transform xrOriginTransform)
    {
        bool changed = false;
        MeshFilter[] meshFilters = Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            GameObject gameObject = meshFilter.gameObject;
            if (gameObject.scene != activeScene || meshFilter.sharedMesh == null)
            {
                continue;
            }

            if (xrOriginTransform != null && gameObject.transform.IsChildOf(xrOriginTransform))
            {
                continue;
            }

            if (gameObject.GetComponent<Collider>() != null || !ShouldAddEnvironmentCollider(gameObject, meshFilter))
            {
                continue;
            }

            MeshCollider meshCollider = Undo.AddComponent<MeshCollider>(gameObject);
            meshCollider.sharedMesh = meshFilter.sharedMesh;
            changed = true;
        }

        return changed;
    }

    private static bool ShouldAddEnvironmentCollider(GameObject gameObject, MeshFilter meshFilter)
    {
        if (!gameObject.activeInHierarchy || gameObject.CompareTag("MainCamera"))
        {
            return false;
        }

        string name = gameObject.name.ToLowerInvariant();
        if (name.Contains("camera") || name.Contains("controller") || name.Contains("hand"))
        {
            return false;
        }

        Vector3 lossyScale = gameObject.transform.lossyScale;
        Vector3 size = Vector3.Scale(meshFilter.sharedMesh.bounds.size, new Vector3(
            Mathf.Abs(lossyScale.x),
            Mathf.Abs(lossyScale.y),
            Mathf.Abs(lossyScale.z)));

        float maxDimension = Mathf.Max(size.x, size.y, size.z);
        float volume = size.x * size.y * size.z;

        if (gameObject.isStatic)
        {
            return maxDimension >= 0.15f;
        }

        if (name.Contains("floor") || name.Contains("wall") || name.Contains("ceiling") ||
            name.Contains("door") || name.Contains("table") || name.Contains("cabinet") ||
            name.Contains("window") || name.Contains("screen") || name.Contains("wash"))
        {
            return true;
        }

        return maxDimension >= 0.75f || volume >= 0.1f;
    }

    private static bool EnsureDoorSetup(Scene activeScene)
    {
        bool changed = false;
        Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Transform sceneTransform in transforms)
        {
            if (sceneTransform.gameObject.scene != activeScene)
            {
                continue;
            }

            if (!IsDoorRoot(sceneTransform))
            {
                continue;
            }

            changed |= EnsureSingleDoorSetup(sceneTransform);
        }

        return changed;
    }

    private static bool EnsureSingleDoorSetup(Transform doorRoot)
    {
        bool changed = false;
        AutoDoorController doorController = doorRoot.GetComponent<AutoDoorController>();
        if (doorController == null)
        {
            doorController = Undo.AddComponent<AutoDoorController>(doorRoot.gameObject);
            changed = true;
        }

        BoxCollider triggerCollider = doorRoot.GetComponent<BoxCollider>();
        if (triggerCollider == null)
        {
            triggerCollider = Undo.AddComponent<BoxCollider>(doorRoot.gameObject);
            triggerCollider.isTrigger = true;
            changed = true;
        }

        Rigidbody rigidbodyComponent = doorRoot.GetComponent<Rigidbody>();
        if (rigidbodyComponent == null)
        {
            rigidbodyComponent = Undo.AddComponent<Rigidbody>(doorRoot.gameObject);
            changed = true;
        }

        rigidbodyComponent.isKinematic = true;
        rigidbodyComponent.useGravity = false;
        if (doorRoot.gameObject.isStatic)
        {
            doorRoot.gameObject.isStatic = false;
            changed = true;
        }

        Bounds localBounds = CalculateLocalBounds(doorRoot);
        Vector3 padding = new Vector3(1f, 0.6f, 1.6f);
        triggerCollider.isTrigger = true;
        triggerCollider.center = localBounds.center + new Vector3(0f, 0.2f, 0f);
        triggerCollider.size = new Vector3(
            Mathf.Max(1.5f, localBounds.size.x + padding.x),
            Mathf.Max(2f, localBounds.size.y + padding.y),
            Mathf.Max(2f, localBounds.size.z + padding.z));

        bool isDoubleDoor = doorRoot.name == "DoubleDoor";
        Transform[] panels = ResolveDoorPanels(doorRoot, isDoubleDoor);
        foreach (Transform panel in panels)
        {
            if (panel != null && panel.gameObject.isStatic)
            {
                panel.gameObject.isStatic = false;
                changed = true;
            }
        }

        Collider[] colliders = ResolveDoorColliders(panels);
        doorController.Configure(panels, colliders, isDoubleDoor);
        return changed;
    }

    private static bool IsDoorRoot(Transform transform)
    {
        string name = transform.name;
        return name == "Door" || name == "DoubleDoor";
    }

    private static Transform[] ResolveDoorPanels(Transform doorRoot, bool isDoubleDoor)
    {
        List<Transform> panels = new();
        foreach (Transform child in doorRoot)
        {
            string childName = child.name.ToLowerInvariant();
            bool looksLikeDoorLeaf = childName.Contains("door") && !childName.Contains("frame");
            if (looksLikeDoorLeaf)
            {
                panels.Add(child);
            }
        }

        if (panels.Count == 0)
        {
            panels.Add(doorRoot);
        }

        panels.Sort((left, right) => left.localPosition.x.CompareTo(right.localPosition.x));

        if (!isDoubleDoor)
        {
            Transform bestSinglePanel = panels.Find(panel =>
            {
                string childName = panel.name.ToLowerInvariant();
                return childName == "door" || childName.StartsWith("door_");
            }) ?? panels[0];

            return new[] { bestSinglePanel };
        }

        if (panels.Count > 2)
        {
            return new[] { panels[0], panels[panels.Count - 1] };
        }

        return panels.ToArray();
    }

    private static Collider[] ResolveDoorColliders(Transform[] panels)
    {
        List<Collider> colliders = new();
        foreach (Transform panel in panels)
        {
            if (panel == null)
            {
                continue;
            }

            Collider[] panelColliders = panel.GetComponentsInChildren<Collider>(true);
            foreach (Collider panelCollider in panelColliders)
            {
                if (panelCollider is BoxCollider boxCollider && boxCollider.isTrigger)
                {
                    continue;
                }

                if (!colliders.Contains(panelCollider))
                {
                    colliders.Add(panelCollider);
                }
            }
        }

        return colliders.ToArray();
    }

    private static Bounds CalculateLocalBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.up, new Vector3(1.5f, 2f, 1f));
        }

        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        foreach (Renderer rendererComponent in renderers)
        {
            Bounds worldBounds = rendererComponent.bounds;
            Vector3[] corners =
            {
                new(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z),
                new(worldBounds.min.x, worldBounds.min.y, worldBounds.max.z),
                new(worldBounds.min.x, worldBounds.max.y, worldBounds.min.z),
                new(worldBounds.min.x, worldBounds.max.y, worldBounds.max.z),
                new(worldBounds.max.x, worldBounds.min.y, worldBounds.min.z),
                new(worldBounds.max.x, worldBounds.min.y, worldBounds.max.z),
                new(worldBounds.max.x, worldBounds.max.y, worldBounds.min.z),
                new(worldBounds.max.x, worldBounds.max.y, worldBounds.max.z)
            };

            foreach (Vector3 corner in corners)
            {
                Vector3 localCorner = root.InverseTransformPoint(corner);
                min = Vector3.Min(min, localCorner);
                max = Vector3.Max(max, localCorner);
            }
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);
        return bounds;
    }

    private static Bounds CalculateWorldBounds(Transform root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }
}
