using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

public class MiniBoneTool : EditorWindow
{
    static float ver = 1.1f;

    private GameObject meshObject;
    private Transform rootBone;

    [SerializeField]
    private List<MiniBoneData> boneObjects = new List<MiniBoneData>();

    private SerializedObject serializedObjectRef;

    private float gizmoSize = 2.0f;
    private bool gizmoHidden = false;
    private bool wireframeMode = false;

    private bool editMode = false;
    private bool scrollToBottom = false;

    private Vector2 lastMouseDownPos;

    private Vector2 scrollPosition;
    private Texture2D logoTexture;

    private static float windowWidth = 400f;
    private float timer = 0f;

    [MenuItem("Tools/Mini Bone Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<MiniBoneTool>($"Mini Bone Tool (Ver {ver})");
        window.minSize = new Vector2(windowWidth, 300f);
        window.maxSize = new Vector2(windowWidth, 1000f);
    }

    private void OnEnable()
    {
        serializedObjectRef = new SerializedObject(this);
        EditorApplication.update += OnEditorUpdate;
        SceneView.duringSceneGui += OnSceneGUI;
        LoadLogoTexture();
    }

    private void OnDisable()
    {
        Tools.hidden = false;
        SceneView.duringSceneGui -= OnSceneGUI;

        if (editMode)
        {
            editMode = false;
            ToggleEditMode(false);
        }

        EditorApplication.update -= OnEditorUpdate;
        HideWireframe();
        RemoveGizmoDrawer();
        SetHelperNodesActive(false);
    }

    private void OnEditorUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= 0.25f)
        {
            Repaint();
            timer = 0f;
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (editMode && meshObject != null && Selection.activeGameObject == meshObject)
        {
            Tools.hidden = true;
        }
        else
        {
            Tools.hidden = false;
        }

        if (boneObjects == null || boneObjects.Count == 0) return;

        Event e = Event.current;

        if (e.type == EventType.MouseDown && e.button == 0 && !e.alt)
        {
            lastMouseDownPos = e.mousePosition;
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && !e.alt)
        {
            if (Vector2.Distance(lastMouseDownPos, e.mousePosition) < 5f)
            {
                Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
                List<GameObject> hitCandidates = new List<GameObject>();

                foreach (var bd in boneObjects)
                {
                    if (bd.bone == null) continue;

                    float distCam = Vector3.Distance(sceneView.camera.transform.position, bd.bone.position);
                    float clickRadius = 0.035f * distCam;
                    float distToRay = Vector3.Cross(ray.direction, bd.bone.position - ray.origin).magnitude;

                    if (distToRay < clickRadius) hitCandidates.Add(bd.bone.gameObject);

                    foreach (Transform child in bd.bone)
                    {
                        if (child.name.StartsWith("HelperNode"))
                        {
                            float hDistCam = Vector3.Distance(sceneView.camera.transform.position, child.position);
                            float hClickRadius = 0.03f * hDistCam;
                            float hDistToRay = Vector3.Cross(ray.direction, child.position - ray.origin).magnitude;

                            if (hDistToRay < hClickRadius) hitCandidates.Add(child.gameObject);
                        }
                    }
                }

                if (hitCandidates.Count > 0)
                {
                    float bestDist = float.MaxValue;
                    GameObject closestObj = null;
                    foreach (var hit in hitCandidates)
                    {
                        float d = Vector3.Cross(ray.direction, hit.transform.position - ray.origin).magnitude;
                        if (d < bestDist)
                        {
                            bestDist = d;
                            closestObj = hit;
                        }
                    }

                    List<GameObject> overlappingHits = new List<GameObject>();
                    Vector3 targetPos = closestObj.transform.position;
                    foreach (var hit in hitCandidates)
                    {
                        if (Vector3.Distance(hit.transform.position, targetPos) < 0.001f)
                        {
                            overlappingHits.Add(hit);
                        }
                    }

                    overlappingHits.Sort((a, b) =>
                    {
                        bool aIsHelper = a.name.StartsWith("HelperNode");
                        bool bIsHelper = b.name.StartsWith("HelperNode");
                        if (aIsHelper && !bIsHelper) return -1;
                        if (!aIsHelper && bIsHelper) return 1;
                        return a.name.CompareTo(b.name);
                    });

                    GameObject currentSelected = Selection.activeGameObject;
                    GameObject nextSelection = overlappingHits[0];

                    if (overlappingHits.Contains(currentSelected))
                    {
                        if (overlappingHits.Count > 1)
                        {
                            int currentIndex = overlappingHits.IndexOf(currentSelected);
                            int nextIndex = (currentIndex + 1) % overlappingHits.Count;
                            nextSelection = overlappingHits[nextIndex];
                        }
                        else
                        {
                            nextSelection = currentSelected;
                        }
                    }

                    if (Selection.activeGameObject != nextSelection)
                    {
                        Selection.activeGameObject = nextSelection;

                        // [해결] 선택이 변경될 때 피봇(이동 툴 등)의 조작 상태를 완전히 강제 초기화!
                        // 유니티가 "평면 핸들을 아직 잡고 있다"고 착각하는 것을 방지합니다.
                        GUIUtility.hotControl = 0;
                        GUIUtility.keyboardControl = 0;

                        e.Use();
                        Repaint();
                    }
                }
            }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space((position.width - windowWidth) * 0.5f);
        EditorGUILayout.BeginVertical(GUILayout.Width(windowWidth));

        if (meshObject != null && rootBone == null)
        {
            ResetUI();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUIStyle.none, GUI.skin.verticalScrollbar);

        RemoveMissingBones();
        DrawLogo();

        GUILayout.Label("  기본 설정", EditorStyles.boldLabel);

        if (meshObject == null) SetGUIColor(Color.blue + Color.white);
        meshObject = (GameObject)EditorGUILayout.ObjectField("대상 오브젝트", meshObject, typeof(GameObject), true);
        SetGUIColor(Color.white);

        if (meshObject != null)
        {
            CheckRootBone();

            DrawButton("Root Bone 추가", CreateRootBone, rootBone == null);

            if (rootBone != null)
            {
                bool canLoad = (rootBone.GetComponent<MiniBoneSaveData>() != null);
                EditorGUI.BeginDisabledGroup(!canLoad);
                if (GUILayout.Button("저장된 데이터 불러오기", GUILayout.Height(25)))
                {
                    RevertToSavedState();
                }
                EditorGUI.EndDisabledGroup();

                if (boneObjects.Count == 0) SetGUIColor(Color.blue + Color.white);
                DrawButton("새로운 Bone 생성", () =>
                {
                    CreateBone();
                    AddGizmoDrawer();
                });
                SetGUIColor(Color.white);

                if (boneObjects.Count > 0)
                {
                    GUILayout.Label("  Bone 목록", EditorStyles.boldLabel);

                    serializedObjectRef.Update();

                    bool disableValues = IsMeshRiggedWithCurrentBones() && !editMode;

                    for (int i = 0; i < boneObjects.Count; i++)
                    {
                        EditorGUILayout.BeginVertical("box");
                        bool removed = DrawBoneDataEditor(boneObjects[i], disableValues);
                        EditorGUILayout.EndVertical();

                        if (removed)
                        {
                            boneObjects.RemoveAt(i);
                            i--;
                        }
                    }

                    serializedObjectRef.ApplyModifiedProperties();
                }
                else
                {
                    EditorGUILayout.Space(2);
                    GUILayout.Label("Bone은 Root 오브젝트 하위에 생성이 됩니다.\n\n- Tip -\n Bone 오브젝트의 자식으로 다른 Bone 오브젝트를 추가해\n계층 구조를 구성할 수도 있습니다.");
                }
            }
            else
            {
                EditorGUILayout.Space(2);
                GUILayout.Label("Root Bone을 추가하면 '대상 오브젝트' 하위에\nRoot 오브젝트가 생성됩니다.");
            }
        }
        else
        {
            EditorGUILayout.Space(2);
            GUILayout.Label("Mesh Renderer 및 Skind Mesh Renderer 컴포넌트를\n사용하는 오브젝트를 끌어다 놓습니다.");
            EditorGUILayout.Space(1);
            GUILayout.Label("기본 사용 방법은 Helper.txt에서 확인할 수 있습니다.");
        }

        if (scrollToBottom)
        {
            scrollPosition.y = float.MaxValue;
            if (Event.current.type == EventType.Repaint)
            {
                scrollToBottom = false;
            }
        }

        EditorGUILayout.EndScrollView();

        if (meshObject != null && rootBone != null && boneObjects.Count > 0)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(5);

            EditorGUILayout.BeginHorizontal();
            if (editMode)
            {
                SetGUIColor(Color.green);
                if (GUILayout.Button("작업 모드 종료", GUILayout.Height(35)))
                {
                    editMode = false;
                    ToggleEditMode(false);
                }
                SetDefaultGUIColor();

                if (GUILayout.Button("설정 초기화", GUILayout.Width(100), GUILayout.Height(35)))
                {
                    RevertToSavedState();
                }
            }
            else
            {
                SetGUIColor(new Color(0.6f, 1f, 0.6f));
                if (GUILayout.Button("작업 모드", GUILayout.Height(35)))
                {
                    editMode = true;
                    ToggleEditMode(true);
                }
                SetDefaultGUIColor();

                if (GUILayout.Button("설정 초기화", GUILayout.Width(100), GUILayout.Height(35)))
                {
                    RevertToSavedState();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (editMode)
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox("작업 모드 활성화 중:\n새로운 본을 추가하거나 위치, 범위 설정값을 실시간으로 확인하세요.\n작업 완료 후 [작업 모드 종료] 버튼을 누르면 업데이트 됩니다.", MessageType.Info);
            }

            EditorGUILayout.Space(2);
            SetGUIColor(Color.yellow);
            DrawButton("컴포넌트 정리", FinalAct, CheckGizmoComponent());
            SetDefaultGUIColor();

            EditorGUILayout.Space(5);
        }

        EditorGUILayout.EndVertical();
        GUILayout.Space((position.width - windowWidth) * 0.5f);
        EditorGUILayout.EndHorizontal();
    }

    private void SetHelperNodesActive(bool isActive)
    {
        if (boneObjects == null) return;
        foreach (var bd in boneObjects)
        {
            if (bd.bone != null)
            {
                foreach (Transform child in bd.bone)
                {
                    if (child.name.StartsWith("HelperNode"))
                    {
                        child.gameObject.SetActive(isActive);
                    }
                }
            }
        }
    }

    private bool IsMeshRiggedWithCurrentBones()
    {
        if (meshObject == null || rootBone == null) return false;
        var smr = meshObject.GetComponent<SkinnedMeshRenderer>();
        if (smr == null || smr.sharedMesh == null) return false;

        if (!smr.sharedMesh.name.EndsWith("_Rigged")) return false;
        if (smr.sharedMesh.boneWeights == null || smr.sharedMesh.boneWeights.Length == 0) return false;
        if (smr.bones == null || smr.bones.Length != boneObjects.Count + 1) return false;

        return true;
    }

    private void RevertToSavedState()
    {
        if (rootBone == null || meshObject == null) return;

        var saveData = rootBone.GetComponent<MiniBoneSaveData>();
        if (saveData == null || saveData.savedBoneData.Count == 0)
        {
            return;
        }

        GUI.FocusControl(null);
        MiniBoneDataManager.LoadBoneData(rootBone, boneObjects);
        RemoveNonBoneChildren();

        if (!editMode)
        {
            MiniBoneUtility.StartRigging(meshObject, rootBone, boneObjects);
            MiniBoneDataManager.SaveBoneData(rootBone, boneObjects);
            SetHelperNodesActive(false);

            HideGizmo();
            HideWireframe();
        }
        else
        {
            ToggleEditMode(true);
        }

        SceneView.RepaintAll();
    }

    private void SnapshotPositions()
    {
        if (boneObjects == null) return;

        foreach (var bd in boneObjects)
        {
            if (bd.bone != null)
            {
                bd.savedLocalPosition = bd.bone.localPosition;
                bd.savedLocalRotation = bd.bone.localRotation;

                bd.helperLocalPositions.Clear();
                bd.helperLocalRotations.Clear();

                foreach (Transform child in bd.bone)
                {
                    if (child.name.StartsWith("HelperNode"))
                    {
                        bd.helperLocalPositions.Add(child.localPosition);
                        bd.helperLocalRotations.Add(child.localRotation);
                    }
                }
            }
        }
    }

    private void ToggleEditMode(bool enable)
    {
        if (meshObject == null || rootBone == null) return;

        if (enable)
        {
            SetHelperNodesActive(true);

            var smr = meshObject.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.sharedMesh != null)
            {
                smr.sharedMesh.boneWeights = new BoneWeight[0];
                smr.sharedMesh.bindposes = new Matrix4x4[0];
                smr.bones = new Transform[0];
            }

            ShowGizmo();
            ShowWireframe();
        }
        else
        {
            RemoveNonBoneChildren();
            SnapshotPositions();
            MiniBoneUtility.StartRigging(meshObject, rootBone, boneObjects);
            MiniBoneDataManager.SaveBoneData(rootBone, boneObjects);
            SetHelperNodesActive(false);

            HideGizmo();
            HideWireframe();
        }
    }

    void CheckRootBone()
    {
        if (meshObject == null) return;
        rootBone = meshObject.transform.Find("Root");
    }

    private bool DrawBoneDataEditor(MiniBoneData boneData, bool isDisabled)
    {
        Color slotColor = Color.white;

        if (Selection.activeGameObject != null)
        {
            if (boneData.bone == Selection.activeGameObject.transform)
                slotColor = Color.blue + Color.white;
            else
            {
                foreach (Transform child in boneData.bone)
                {
                    if (child.gameObject == Selection.activeGameObject) slotColor = Color.blue + Color.white;
                }
            }
        }

        bool removed = false;

        EditorGUILayout.BeginHorizontal();

        Rect colorRect = GUILayoutUtility.GetRect(18, 18);
        EditorGUI.DrawRect(colorRect, boneData.color);

        if (GUI.Button(colorRect, new GUIContent("", "클릭 시 색상이 랜덤으로 변경됩니다."), GUIStyle.none))
        {
            boneData.color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.8f, 1f);
            SceneView.RepaintAll();
        }

        SetGUIColor(slotColor);
        GUILayout.Label("Bone 이름", GUILayout.Width(80));

        Transform newBone = (Transform)EditorGUILayout.ObjectField(
            boneData.bone,
            typeof(Transform),
            true,
            GUILayout.MinWidth(150)
        );

        SetDefaultGUIColor();

        if (GUILayout.Button("선택", GUILayout.Width(60)))
        {
            if (boneData.bone != null) Selection.activeObject = boneData.bone.gameObject;
        }

        if (GUILayout.Button("삭제", GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("본 제거", "정말 제거하시겠습니까?", "확인", "취소"))
            {
                if (boneData.bone != null) DestroyImmediate(boneData.bone.gameObject);
                removed = true;
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(3);

        if (!removed)
        {
            if (newBone != boneData.bone && newBone != null)
            {
                boneData.bone = newBone;
                Selection.activeObject = newBone.gameObject;
            }

            EditorGUI.BeginDisabledGroup(isDisabled);

            EditorGUI.BeginChangeCheck();
            float newRadius = EditorGUILayout.FloatField("영향력 반경 (Radius)", boneData.influenceRadius);
            float newStrength = EditorGUILayout.FloatField("가중치 강도 (Strength)", boneData.influenceStrength);

            if (boneData.falloffCurve == null) boneData.falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            AnimationCurve newCurve = EditorGUILayout.CurveField("감쇠 커브 (Falloff)", boneData.falloffCurve);

            if (EditorGUI.EndChangeCheck())
            {
                boneData.influenceRadius = newRadius;
                boneData.influenceStrength = newStrength;
                boneData.falloffCurve = newCurve;
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(2);

            boneData.showHelperUI = EditorGUILayout.Foldout(boneData.showHelperUI, "🔹 Helper Node 설정", true, EditorStyles.foldoutHeader);

            if (boneData.showHelperUI)
            {
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("  Node 관리", GUILayout.Width(100));
                if (GUILayout.Button("생성", GUILayout.Width(50)))
                {
                    CreateHelperNode(boneData);
                }
                if (GUILayout.Button("초기화", GUILayout.Width(50)))
                {
                    RemoveHelperNodes(boneData);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.BeginChangeCheck();
                float newHRad = EditorGUILayout.FloatField(" └ 영향력 반경 (Radius)", boneData.helperRadius);
                float newHStr = EditorGUILayout.FloatField(" └ 가중치 강도 (Strength)", boneData.helperStrength);

                if (EditorGUI.EndChangeCheck())
                {
                    boneData.helperRadius = newHRad;
                    boneData.helperStrength = newHStr;
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndVertical();
            }

            EditorGUI.EndDisabledGroup();
        }

        SetDefaultGUIColor();
        return removed;
    }

    private void CreateHelperNode(MiniBoneData boneData)
    {
        if (boneData.bone == null) return;

        int count = 0;
        foreach (Transform child in boneData.bone)
        {
            if (child.name.StartsWith("HelperNode")) count++;
        }

        GameObject helper = new GameObject($"HelperNode_{count + 1:D2}");
        helper.transform.parent = boneData.bone;
        helper.transform.localPosition = Vector3.zero;
        Selection.activeGameObject = helper;
        SceneView.RepaintAll();
    }

    private void RemoveHelperNodes(MiniBoneData boneData)
    {
        if (boneData.bone == null) return;
        List<GameObject> toDestroy = new List<GameObject>();
        foreach (Transform child in boneData.bone)
        {
            if (child.name.StartsWith("HelperNode")) toDestroy.Add(child.gameObject);
        }
        foreach (var go in toDestroy) DestroyImmediate(go);
        SceneView.RepaintAll();
    }

    private void CreateRootBone()
    {
        if (meshObject == null) return;

        rootBone = new GameObject("Root")
        {
            transform =
            {
                position   = meshObject.transform.position,
                rotation   = meshObject.transform.rotation,
                localScale = meshObject.transform.localScale
            }
        }.transform;

        rootBone.transform.parent = meshObject.transform;
        Selection.activeObject = rootBone.gameObject;
        Debug.Log("Root Bone이 생성되었습니다.");
    }

    private void CreateBone()
    {
        if (rootBone == null) return;

        int maxIndex = GetMaxBoneIndex(rootBone);
        int nextIndex = maxIndex + 1;
        string boneName = $"Bone_{nextIndex:D3}";

        var newBone = new GameObject(boneName)
        {
            transform =
            {
                parent        = rootBone,
                localPosition = Vector3.zero
            }
        };

        boneObjects.Add(new MiniBoneData
        {
            bone = newBone.transform,
            color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.8f, 1f),
            falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)
        });

        Selection.activeObject = newBone.gameObject;

        scrollToBottom = true;
        Repaint();

        if (!editMode)
        {
            editMode = true;
            ToggleEditMode(true);
        }
    }

    private int GetMaxBoneIndex(Transform parent)
    {
        int maxIndex = 0;
        foreach (Transform child in parent)
        {
            if (child.name.StartsWith("Bone_"))
            {
                string numberStr = child.name.Substring(5);
                if (int.TryParse(numberStr, out int existingIndex))
                {
                    if (existingIndex > maxIndex) maxIndex = existingIndex;
                }
            }
            maxIndex = Mathf.Max(maxIndex, GetMaxBoneIndex(child));
        }
        return maxIndex;
    }

    private void RemoveNonBoneChildren()
    {
        RemoveNonBoneRecursive(rootBone);
    }

    private void RemoveNonBoneRecursive(Transform parent)
    {
        List<Transform> toRemove = new List<Transform>();
        foreach (Transform child in parent)
        {
            bool isBone = false;
            foreach (var bd in boneObjects)
            {
                if (bd.bone == child || child.name.StartsWith("HelperNode"))
                {
                    isBone = true;
                    break;
                }
            }
            if (!isBone) toRemove.Add(child);
            else RemoveNonBoneRecursive(child);
        }

        foreach (var child in toRemove) DestroyImmediate(child.gameObject);
    }

    private void FinalAct()
    {
        RemoveGizmoDrawer();
        SetHelperNodesActive(false);
    }

    bool CheckGizmoComponent()
    {
        return meshObject.GetComponent<MiniBoneGizmoDrawer>() != null;
    }

    private void ResetUI()
    {
        rootBone = null;
        boneObjects.Clear();
        editMode = false;
        RemoveGizmoDrawer();
    }

    private void RemoveMissingBones()
    {
        for (int i = boneObjects.Count - 1; i >= 0; i--)
        {
            if (boneObjects[i].bone == null) boneObjects.RemoveAt(i);
        }
    }

    private void AddGizmoDrawer()
    {
        if (meshObject == null) return;
        var drawer = meshObject.GetComponent<MiniBoneGizmoDrawer>() ?? meshObject.AddComponent<MiniBoneGizmoDrawer>();
        drawer.boneObjects = boneObjects;
        drawer.gizmoSize = gizmoSize;
    }

    private void RemoveGizmoDrawer()
    {
        if (meshObject == null) return;
        DestroyImmediate(meshObject.GetComponent<MiniBoneGizmoDrawer>());
    }

    private void DrawLogo()
    {
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (logoTexture != null)
        {
            float size = 0.35f;
            int width = 1024;
            int height = 256;
            GUILayout.Label(logoTexture, GUILayout.Width(width * size), GUILayout.Height(height * size));
        }
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private void LoadLogoTexture()
    {
        string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this));
        string directoryPath = System.IO.Path.GetDirectoryName(scriptPath);
        string texturePath = Path.Combine(directoryPath, "Img/Simple_Rig_Logo.psd");
        logoTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    }

    private void DrawButton(string label, System.Action action, bool condition = true)
    {
        EditorGUI.BeginDisabledGroup(!condition);
        if (GUILayout.Button(label, GUILayout.Height(25))) action();
        EditorGUI.EndDisabledGroup();
    }

    private void HideGizmo()
    {
        gizmoSize = 0.0f;
        gizmoHidden = true;
        UpdateGizmoDrawer();
    }

    void ShowWireframe()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null) sceneView.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Wireframe);
        wireframeMode = true;
    }

    void HideWireframe()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null) sceneView.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);
        wireframeMode = false;
    }

    private void ShowGizmo()
    {
        if (CheckGizmoComponent() == false) AddGizmoDrawer();
        gizmoSize = 2;
        gizmoHidden = false;
        UpdateGizmoDrawer();
    }

    private void UpdateGizmoDrawer()
    {
        var drawer = meshObject.GetComponent<MiniBoneGizmoDrawer>();
        if (drawer != null) drawer.gizmoSize = gizmoSize;
        SceneView.RepaintAll();
    }

    public void SetGUIColor(Color Sender) { GUI.color = Sender; }
    public void SetDefaultGUIColor() { GUI.color = Color.white; }
}