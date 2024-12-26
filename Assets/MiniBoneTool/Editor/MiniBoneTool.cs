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

    private Vector2 scrollPosition;

    private Texture2D logoTexture; // �ΰ� �ؽ�ó

    // UI�� ���� ũ�⸦ �����ϴ� ����
    private static float windowWidth = 400f; // �⺻��: 400px

    private float timer = 0f;

    [MenuItem("Tools/Mini Bone Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<MiniBoneTool>($"Mini Bone Tool (Ver {ver})");
        window.minSize = new Vector2(windowWidth, 300f); // �ּ� ũ�� ����
        window.maxSize = new Vector2(windowWidth, 1000f); // �ִ� ũ�� ����

    }

    private void OnEnable()
    {
        serializedObjectRef = new SerializedObject(this);

        // EditorApplication.update += OnGUI;

        EditorApplication.update += OnEditorUpdate;

        LoadLogoTexture();
    }



    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        HideWireframe();

        RemoveGizmoDrawer();
    }


    private void OnEditorUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= 0.25f) 
        {
            Repaint(); // GUI ����

            timer = 0f;
        }
    }


    private void OnGUI()
    {
        // UI ���� ũ�� ����
        EditorGUILayout.BeginHorizontal();
        GUILayout.Space((position.width - windowWidth) * 0.5f);
        EditorGUILayout.BeginVertical(GUILayout.Width(windowWidth));

        if (meshObject != null && rootBone == null)
        {
            ResetUI();
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        RemoveMissingBones();

        DrawLogo();

        GUILayout.Label("  �⺻ ����", EditorStyles.boldLabel);

        if(meshObject == null)
            SetGUIColor(Color.blue + Color.white);

        meshObject = (GameObject)EditorGUILayout.ObjectField("��� ������Ʈ", meshObject, typeof(GameObject), true);

        SetGUIColor(Color.white);

        if (meshObject != null)
        {
            CheckRootBone();

            DrawButton("Root Bone �߰�", CreateRootBone, rootBone == null);

            if (rootBone != null)
            {
                bool canLoad = (rootBone.GetComponent<MiniBoneSaveData>() != null);
                EditorGUI.BeginDisabledGroup(!canLoad);
                if (GUILayout.Button("����� ������ �ҷ�����", GUILayout.Height(25)))
                {
                    MiniBoneDataManager.LoadBoneData(rootBone, boneObjects);

                    AddGizmoDrawer();

                    ShowGizmo();

                    SceneView.RepaintAll();
                }
                EditorGUI.EndDisabledGroup();

                if(boneObjects.Count == 0)
                        SetGUIColor(Color.blue + Color.white);

                DrawButton("���ο� Bone ����", () =>
                {
                    CreateBone();

                    AddGizmoDrawer();
                });

                SetGUIColor(Color.white);

                if (boneObjects.Count > 0)
                {
                    GUILayout.Label("  Bone ���", EditorStyles.boldLabel);

                    serializedObjectRef.Update();

                    for (int i = 0; i < boneObjects.Count; i++)
                    {
                        EditorGUILayout.BeginVertical("box");
                        bool removed = DrawBoneDataEditor(boneObjects[i]);
                        EditorGUILayout.EndVertical();

                        if (removed)
                        {
                            boneObjects.RemoveAt(i);
                            i--;
                        }
                    }

                    serializedObjectRef.ApplyModifiedProperties();

                    EditorGUILayout.Space(5);

                    EditorGUILayout.BeginHorizontal();
                    if (CheckGizmoComponent() == false)
                    {
                        DrawButton("Gizmo ���̱�", ShowGizmo);
                    }
                    else
                    {
                        if (gizmoHidden)
                        {
                            DrawButton("Gizmo ���̱�", ShowGizmo);
                        }
                        else
                        {
                            DrawButton("Gizmo �����", HideGizmo);
                        }
                    }

                    if(wireframeMode == true)
                    {
                        if (GUILayout.Button("�Ϲ� ���", GUILayout.Width(175), GUILayout.Height(25)))
                        {
                            HideWireframe();
                        }
                    }
                    else
                    {
                        if (GUILayout.Button("���̾� ������ ���", GUILayout.Width(175), GUILayout.Height(25)))
                        {
                            ShowWireframe();
                        }
                    }

                    EditorGUILayout.EndHorizontal();

                    DrawButton("�۾� ����", () =>
                    {
                        if (meshObject == null || rootBone == null)
                        {
                            Debug.LogError("Mesh ������Ʈ�� Root Bone�� �������� �ʾҽ��ϴ�.");
                            return;
                        }

                        RemoveNonBoneChildren();

                        MiniBoneUtility.StartRigging(meshObject, rootBone, boneObjects);

                        MiniBoneDataManager.SaveBoneData(rootBone, boneObjects);


                        EditorUtility.DisplayDialog("SystemAlert", "�۾� �Ϸ�!\n\nBone ����� ���� ����\nRoot ������Ʈ�� ����Ǿ����ϴ�.", "OK");
                    });

                    SetGUIColor(Color.yellow);

                    DrawButton("������Ʈ ����", FinalAct, CheckGizmoComponent());

                    SetDefaultGUIColor();
                }
                else
                {
                    EditorGUILayout.Space(2);

                    GUILayout.Label("Bone�� Root ������Ʈ ������ ������ �˴ϴ�.\n\n- Tip -\n Bone ������Ʈ�� �ڽ����� �ٸ� Bone ������Ʈ�� �߰���\n���� ������ ������ ���� �ֽ��ϴ�.");

                }

            }
            else
            {
                EditorGUILayout.Space(2);

                GUILayout.Label("Root Bone�� �߰��ϸ� '��� ������Ʈ' ������\nRoot ������Ʈ�� �����˴ϴ�.");
            }
        }
        else
        {
            EditorGUILayout.Space(2);

            GUILayout.Label("Mesh Renderer �� Skind Mesh Renderer ������Ʈ��\n����ϴ� ������Ʈ�� ����� �����ϴ�.");
            EditorGUILayout.Space(1);

            GUILayout.Label("�⺻ ��� ����� Helper.txt���� Ȯ���� �� �ֽ��ϴ�.");

        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        GUILayout.Space((position.width - windowWidth) * 0.5f);
        EditorGUILayout.EndHorizontal();
    }

    void CheckRootBone()
    {
        if (meshObject == null)
            return;

        rootBone = meshObject.transform.Find("Root");
    }

    private bool DrawBoneDataEditor(MiniBoneData boneData)
    {
        Color slotColor = Color.white;

        if (Selection.activeGameObject != null)
        {
            if (boneData.bone == Selection.activeGameObject.transform)
                slotColor = Color.blue + Color.white;
        }



        bool removed = false;

        EditorGUILayout.BeginHorizontal();

        // ���� ǥ�� ĭ
        Rect colorRect = GUILayoutUtility.GetRect(18, 18);
        EditorGUI.DrawRect(colorRect, boneData.color);

        SetGUIColor(slotColor);

        // �� ũ�� ����
        GUILayout.Label("Bone �̸�", GUILayout.Width(80)); // ���⼭ 100�� ���ϴ� ũ��� ����

        // ObjectField
        Transform newBone = (Transform)EditorGUILayout.ObjectField(
            boneData.bone,
            typeof(Transform),
            true,
            GUILayout.MinWidth(150)
        );

        SetDefaultGUIColor();

        if (GUILayout.Button("����", GUILayout.Width(60)))
        {
            if (boneData.bone != null)
            {
                Selection.activeObject = boneData.bone.gameObject;
            }
        }

        if (GUILayout.Button("����", GUILayout.Width(50)))
        {
            if (EditorUtility.DisplayDialog("�� ����", "���� �����Ͻðڽ��ϱ�?", "Ȯ��", "���"))
            {
                if (boneData.bone != null)
                {
                    DestroyImmediate(boneData.bone.gameObject);
                }
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

            EditorGUI.BeginChangeCheck();

            float newRadius = EditorGUILayout.FloatField("Widget ����", boneData.influenceRadius);
            float newStrength = EditorGUILayout.FloatField("Widget ����", boneData.influenceStrength);

            if (EditorGUI.EndChangeCheck())
            {
                boneData.influenceRadius = newRadius;
                boneData.influenceStrength = newStrength;

                SceneView.RepaintAll();
            }
        }

        SetDefaultGUIColor();

        return removed;
    }

    private void CreateRootBone()
    {
        if (meshObject == null)
        {
            Debug.LogError("Mesh ������Ʈ�� ���� �������ּ���.");
            return;
        }

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

        Debug.Log("Root Bone�� �����Ǿ����ϴ�.");
    }

    private void CreateBone()
    {
        if (rootBone == null)
        {
            Debug.LogError("Root Bone�� �������� �ʾҽ��ϴ�.");
            return;
        }

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
        Debug.Log($"{boneName}�� �����Ǿ����ϴ�.");

        boneObjects.Add(new MiniBoneData
        {
            bone = newBone.transform,
            color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.8f, 1f)
        });

        Selection.activeObject = newBone.gameObject;
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
                    if (existingIndex > maxIndex)
                    {
                        maxIndex = existingIndex;
                    }
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
                if (bd.bone == child)
                {
                    isBone = true;
                    break;
                }
            }

            if (!isBone)
            {
                toRemove.Add(child);
            }
            else
            {
                RemoveNonBoneRecursive(child);
            }
        }

        foreach (var child in toRemove)
        {
            Debug.LogWarning($"'{child.name}'��(��) ���� �ƴϹǷ� �����˴ϴ�.");
            DestroyImmediate(child.gameObject);
        }
    }

    private void FinalAct()
    {
        RemoveGizmoDrawer();
        /*
        rootBone = null;
        meshObject = null;
        ResetUI();
        */
    }

    bool CheckGizmoComponent()
    {
        MiniBoneGizmoDrawer component = meshObject.GetComponent<MiniBoneGizmoDrawer>();

        if (component == null)
            return false;

        else
            return true;
    }

    private void ResetUI()
    {
        rootBone = null;
        boneObjects.Clear();
        
        RemoveGizmoDrawer();
    }

    private void RemoveMissingBones()
    {
        for (int i = boneObjects.Count - 1; i >= 0; i--)
        {
            if (boneObjects[i].bone == null)
            {
                boneObjects.RemoveAt(i);
            }
        }
    }


    private void AddGizmoDrawer()
    {
        if (meshObject == null) return;

        var drawer = meshObject.GetComponent<MiniBoneGizmoDrawer>()
                     ?? meshObject.AddComponent<MiniBoneGizmoDrawer>();
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
        if (logoTexture == null)
        {
            Debug.LogWarning($"�ΰ� �̹����� ã�� �� �����ϴ�. ���: {texturePath}");
        }
    }

    private void DrawButton(string label, System.Action action, bool condition = true)
    {
        EditorGUI.BeginDisabledGroup(!condition);
        if (GUILayout.Button(label, GUILayout.Height(25)))
        {
            action();
        }
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

        if (sceneView != null)
            sceneView.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Wireframe);

        wireframeMode = true;
    }

    void HideWireframe()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;

        if(sceneView != null)
            sceneView.cameraMode = SceneView.GetBuiltinCameraMode(DrawCameraMode.Textured);

        wireframeMode = false;
    }

    private void ShowGizmo()
    {
        if(CheckGizmoComponent() == false)
        {
            AddGizmoDrawer();
        }

        gizmoSize = 2;
        gizmoHidden = false;
        UpdateGizmoDrawer();
    }


    private void UpdateGizmoDrawer()
    {
        var drawer = meshObject.GetComponent<MiniBoneGizmoDrawer>();
        if (drawer != null)
        {
            drawer.gizmoSize = gizmoSize;
        }
        SceneView.RepaintAll();
    }

    public void SetGUIColor(Color Sender)
    {
        GUI.color = Sender;
    }

    public void SetDefaultGUIColor()
    {
        GUI.color = Color.white;
    }
}
