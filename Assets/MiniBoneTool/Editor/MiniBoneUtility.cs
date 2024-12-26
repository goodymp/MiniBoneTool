using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// ���� ������ �����ϴ� ��ƿ��Ƽ Ŭ����
/// </summary>
public static class MiniBoneUtility
{
    /// <summary>
    /// ���� ������ �����ϴ� �޼���
    /// </summary>
    public static void StartRigging(GameObject meshObject, Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (meshObject == null || rootBone == null)
        {
            Debug.LogError("Mesh ������Ʈ�� Root Bone�� �������ּ���.");
            return;
        }

        // 1. SkinnedMeshRenderer Ȯ��
        var renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
        if (renderer == null)
        {
            ConvertToSkinnedMeshRenderer(meshObject);
            renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
        }

        // 2. ��� �޽�
        var originalMesh = renderer.sharedMesh;
        if (originalMesh == null)
        {
            Debug.LogError("Mesh ������Ʈ�� ��ȿ�� Mesh�� �����ϴ�.");
            return;
        }

        Debug.Log($"[Check Rig] bindposes: {originalMesh.bindposes?.Length}, boneWeights: {originalMesh.boneWeights?.Length}");

        // 3. �̹� ��Ų ������ �ִ��� Ȯ��
        bool hasBindPoses = (originalMesh.bindposes != null && originalMesh.bindposes.Length > 0);
        bool hasBoneWeights = (originalMesh.boneWeights != null && originalMesh.boneWeights.Length > 0);
        bool isAlreadyRigged = hasBindPoses && hasBoneWeights;

        if (isAlreadyRigged)
        {
            Debug.Log("�̹� ��Ų ����(���� ������)�� ���Ե� �޽��Դϴ�. ���� �޽��� �����ϴ�.");
            ConfigureRigging(meshObject, rootBone, boneObjects, renderer, originalMesh);
        }
        else
        {
            Debug.Log("��Ų ������ ���� �޽��Դϴ�. ���ο� �޽��� �����Ͽ� ���� �����͸� �����մϴ�.");

            var newMesh = Object.Instantiate(originalMesh);
            newMesh.name = originalMesh.name + "_Rigged";

            // 4. MiniBoneUtility ���� ��θ� �������� Mesh ���� ��� ����
            string assetPath = AssetDatabase.GetAssetPath(originalMesh);
            string savePath = GetMeshSavePath(newMesh.name);

            if (string.IsNullOrEmpty(savePath))
            {
                Debug.LogError("Mesh ���� ��θ� ã�� �� �����ϴ�.");
                return;
            }

            // 5. �� �޽��� �������� ����
            AssetDatabase.CreateAsset(newMesh, savePath);
            AssetDatabase.SaveAssets();

            // SkinnedMeshRenderer�� �� �޽� ����
            renderer.sharedMesh = newMesh;

            ConfigureRigging(meshObject, rootBone, boneObjects, renderer, newMesh);

            Debug.Log($"���� �Ϸ�: '{savePath}' �� ���ο� ���� �޽��� ����Ǿ����ϴ�.");
        }
    }

    /// <summary>
    /// MiniBoneUtility ���� ��θ� �������� Meshs ���� ��θ� ã�� ������ ���� ��θ� ��ȯ
    /// </summary>
    private static string GetMeshSavePath(string meshName)
    {
        string utilityPath = GetAssetPath("MiniBoneUtility");


        if (string.IsNullOrEmpty(utilityPath))
        {
            Debug.LogError("MiniBoneUtility ���� ��θ� ã�� �� �����ϴ�.");
            return null;
        }

        // MiniBoneUtility ���� �������� Meshs ���� Ȯ��
        string directory = System.IO.Path.GetDirectoryName(utilityPath);

        string meshFolderPath = System.IO.Path.Combine(directory, "Meshs");

        meshFolderPath = meshFolderPath.Replace("\\Editor", "");

        // Meshs ������ �������� ������ ����
        if (!AssetDatabase.IsValidFolder(meshFolderPath))
        {
            AssetDatabase.CreateFolder(meshFolderPath.Replace("\\Meshs", ""), "Meshs");
        }

        // ������ ���� �̸� ���� (001, 002 �� �߰�)
        string basePath = System.IO.Path.Combine(meshFolderPath, meshName);
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(basePath + ".asset");

        return uniquePath;
    }

    //������Ʈ���� �ش� �̸��� ���Ե� ������ ��� ã�´�.
    public static string GetAssetPath(string AssetName)
    {
        string result = null;

        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();

        for (int i = 0; i < allAssetPaths.Length; i++)
        {
            if (allAssetPaths[i].Contains($"/{AssetName}."))
            {
                result = allAssetPaths[i];

                break;
            }
        }

        return result;
    }



    /// <summary>
    /// ���� MeshRenderer & MeshFilter�� SkinnedMeshRenderer�� ��ȯ
    /// </summary>
    private static void ConvertToSkinnedMeshRenderer(GameObject meshObject)
    {
        var meshRenderer = meshObject.GetComponent<MeshRenderer>();
        var meshFilter = meshObject.GetComponent<MeshFilter>();

        if (meshRenderer == null || meshFilter == null)
        {
            Debug.LogError("MeshRenderer �Ǵ� MeshFilter�� �����ϴ�.");
            return;
        }

        var originalMaterials = meshRenderer.sharedMaterials;
        var originalMesh = meshFilter.sharedMesh;

        // ���� ������Ʈ ����
        Object.DestroyImmediate(meshRenderer);
        Object.DestroyImmediate(meshFilter);

        // SkinnedMeshRenderer �߰�
        var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = originalMesh;
        skinnedMeshRenderer.sharedMaterials = originalMaterials;

        Debug.Log("MeshRenderer�� SkinnedMeshRenderer�� ��ȯ�Ǿ����ϴ�.");
    }

    /// <summary>
    /// �� �迭�� ���ε�����, ���ؽ� BoneWeight ����
    /// </summary>
    private static void ConfigureRigging(GameObject meshObject,
                                         Transform rootBone,
                                         List<MiniBoneData> boneObjects,
                                         SkinnedMeshRenderer renderer,
                                         Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        var boneWeights = new BoneWeight[vertices.Length];

        // bones[0] = rootBone
        Transform[] bones = new Transform[boneObjects.Count + 1];
        Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];

        bones[0] = rootBone;
        bindPoses[0] = rootBone.worldToLocalMatrix * meshObject.transform.localToWorldMatrix;

        for (int i = 0; i < boneObjects.Count; i++)
        {
            var boneData = boneObjects[i];
            bones[i + 1] = boneData.bone;
            bindPoses[i + 1] = boneData.bone.worldToLocalMatrix * meshObject.transform.localToWorldMatrix;
        }

        // ���ؽ��� �� ����Ʈ ���
        AssignBoneWeights(meshObject, boneObjects, vertices, boneWeights);

        // ���� ���� �ݿ�
        mesh.boneWeights = boneWeights;
        mesh.bindposes = bindPoses;

        // ��Ű�� ���� ����
        renderer.bones = bones;
        renderer.rootBone = rootBone;
    }

    /// <summary>
    /// ���ؽ� �迭�� ��ȸ�ϸ� �� ����Ʈ ���
    /// </summary>
    private static void AssignBoneWeights(GameObject meshObject,
                                          List<MiniBoneData> boneObjects,
                                          Vector3[] vertices,
                                          BoneWeight[] boneWeights)
    {
        for (int i = 0; i < vertices.Length; i++)
        {
            var weights = CalculateBoneWeights(meshObject, boneObjects, vertices[i]);
            NormalizeBoneWeights(weights, boneWeights, i);
        }
    }

    /// <summary>
    /// �� ���ؽ��� ���� �� ����Ʈ �ĺ� ��� ����
    /// </summary>
    private static List<BoneWeight> CalculateBoneWeights(GameObject meshObject,
                                                         List<MiniBoneData> boneObjects,
                                                         Vector3 vertex)
    {
        Vector3 worldPosition = meshObject.transform.TransformPoint(vertex);
        List<BoneWeight> weights = new List<BoneWeight>();

        // rootBone�� boneIndex=0, child bone�� (j+1)
        for (int j = 0; j < boneObjects.Count; j++)
        {
            var boneData = boneObjects[j];
            float distance = Vector3.Distance(worldPosition, boneData.bone.position) * 0.99f;

            // ���� ���� ���̸� ����Ʈ ���
            if (distance <= boneData.influenceRadius)
            {
                float weight = Mathf.Clamp01(
                    boneData.influenceStrength * (1.0f - (distance / boneData.influenceRadius))
                );

                weights.Add(new BoneWeight
                {
                    boneIndex0 = j + 1, // 0�� RootBone
                    weight0 = weight
                });
            }
        }

        // ��� ������ ������ ���� ���ϸ� rootBone(0) ����Ʈ = 1
        if (weights.Count == 0)
        {
            weights.Add(new BoneWeight
            {
                boneIndex0 = 0,
                weight0 = 1.0f
            });
        }

        return weights;
    }

    /// <summary>
    /// �ִ� 4���� BoneWeight�� ��� & ����ȭ
    /// </summary>
    private static void NormalizeBoneWeights(List<BoneWeight> weights,
                                             BoneWeight[] boneWeights,
                                             int index)
    {
        // ����Ʈ �������� ����
        weights.Sort((a, b) => b.weight0.CompareTo(a.weight0));

        // �ִ� 4��������
        weights = weights.GetRange(0, Mathf.Min(4, weights.Count));

        float totalWeight = 0f;
        BoneWeight finalWeight = new BoneWeight();

        for (int w = 0; w < weights.Count; w++)
        {
            switch (w)
            {
                case 0:
                    finalWeight.boneIndex0 = weights[w].boneIndex0;
                    finalWeight.weight0 = weights[w].weight0;
                    break;
                case 1:
                    finalWeight.boneIndex1 = weights[w].boneIndex0;
                    finalWeight.weight1 = weights[w].weight0;
                    break;
                case 2:
                    finalWeight.boneIndex2 = weights[w].boneIndex0;
                    finalWeight.weight2 = weights[w].weight0;
                    break;
                case 3:
                    finalWeight.boneIndex3 = weights[w].boneIndex0;
                    finalWeight.weight3 = weights[w].weight0;
                    break;
            }
            totalWeight += weights[w].weight0;
        }

        // ���� 1���� ������ ���� ����Ʈ�� ����ִ� ���� ä��
        if (totalWeight < 1.0f)
        {
            float remaining = 1.0f - totalWeight;
            if (finalWeight.weight0 == 0f) finalWeight.weight0 = remaining;
            else if (finalWeight.weight1 == 0f) finalWeight.weight1 = remaining;
            else if (finalWeight.weight2 == 0f) finalWeight.weight2 = remaining;
            else if (finalWeight.weight3 == 0f) finalWeight.weight3 = remaining;
        }

        // ���� ����ȭ
        float normalizationFactor =
            finalWeight.weight0 + finalWeight.weight1 +
            finalWeight.weight2 + finalWeight.weight3;

        if (normalizationFactor > 0)
        {
            finalWeight.weight0 /= normalizationFactor;
            finalWeight.weight1 /= normalizationFactor;
            finalWeight.weight2 /= normalizationFactor;
            finalWeight.weight3 /= normalizationFactor;
        }

        // boneWeights �迭�� ����
        boneWeights[index] = finalWeight;
    }
}
