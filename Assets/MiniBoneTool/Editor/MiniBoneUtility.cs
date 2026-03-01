using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class MiniBoneUtility
{
    public static void StartRigging(GameObject meshObject, Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (meshObject == null || rootBone == null)
        {
            Debug.LogError("Mesh 오브젝트와 Root Bone을 설정해주세요.");
            return;
        }

        var renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
        if (renderer == null)
        {
            ConvertToSkinnedMeshRenderer(meshObject);
            renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
        }

        var originalMesh = renderer.sharedMesh;
        if (originalMesh == null)
        {
            Debug.LogError("Mesh 오브젝트에 유효한 Mesh가 없습니다.");
            return;
        }

        // [핵심 개선] 메쉬 이름이 _Rigged로 끝난다면 이미 생성된 에셋이므로 무조건 덮어쓰기
        bool isAlreadyOurRiggedMesh = originalMesh.name.EndsWith("_Rigged");

        if (isAlreadyOurRiggedMesh)
        {
            // 새로운 에셋을 만들지 않고 기존 에셋에 가중치 정보만 업데이트
            ConfigureRigging(meshObject, rootBone, boneObjects, renderer, originalMesh);
            EditorUtility.SetDirty(originalMesh);
            AssetDatabase.SaveAssets();
        }
        else
        {
            // 최초 1회에 한해 _Rigged 복사본 에셋을 생성
            var newMesh = Object.Instantiate(originalMesh);
            newMesh.name = originalMesh.name.Replace("(Clone)", "") + "_Rigged";

            string savePath = GetMeshSavePath(newMesh.name);
            if (string.IsNullOrEmpty(savePath)) return;

            AssetDatabase.CreateAsset(newMesh, savePath);
            AssetDatabase.SaveAssets();

            renderer.sharedMesh = newMesh;
            ConfigureRigging(meshObject, rootBone, boneObjects, renderer, newMesh);
        }
    }

    private static string GetMeshSavePath(string meshName)
    {
        string utilityPath = GetAssetPath("MiniBoneUtility");
        if (string.IsNullOrEmpty(utilityPath)) return null;

        string directory = System.IO.Path.GetDirectoryName(utilityPath);
        string meshFolderPath = System.IO.Path.Combine(directory, "Meshs").Replace("\\Editor", "");

        if (!AssetDatabase.IsValidFolder(meshFolderPath))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(meshFolderPath), "Meshs");
        }

        string basePath = System.IO.Path.Combine(meshFolderPath, meshName);
        return AssetDatabase.GenerateUniqueAssetPath(basePath + ".asset");
    }

    public static string GetAssetPath(string AssetName)
    {
        string[] allAssetPaths = AssetDatabase.GetAllAssetPaths();
        foreach (var path in allAssetPaths)
        {
            if (path.Contains($"/{AssetName}.")) return path;
        }
        return null;
    }

    private static void ConvertToSkinnedMeshRenderer(GameObject meshObject)
    {
        var meshRenderer = meshObject.GetComponent<MeshRenderer>();
        var meshFilter = meshObject.GetComponent<MeshFilter>();
        if (meshRenderer == null || meshFilter == null) return;

        var mats = meshRenderer.sharedMaterials;
        var mesh = meshFilter.sharedMesh;

        Object.DestroyImmediate(meshRenderer);
        Object.DestroyImmediate(meshFilter);

        var smr = meshObject.AddComponent<SkinnedMeshRenderer>();
        smr.sharedMesh = mesh;
        smr.sharedMaterials = mats;
    }

    private static void ConfigureRigging(GameObject meshObject, Transform rootBone, List<MiniBoneData> boneObjects, SkinnedMeshRenderer renderer, Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        var boneWeights = new BoneWeight[vertices.Length];

        Transform[] bones = new Transform[boneObjects.Count + 1];
        Matrix4x4[] bindPoses = new Matrix4x4[bones.Length];

        bones[0] = rootBone;
        bindPoses[0] = rootBone.worldToLocalMatrix * meshObject.transform.localToWorldMatrix;

        for (int i = 0; i < boneObjects.Count; i++)
        {
            bones[i + 1] = boneObjects[i].bone;
            bindPoses[i + 1] = boneObjects[i].bone.worldToLocalMatrix * meshObject.transform.localToWorldMatrix;
        }

        for (int i = 0; i < vertices.Length; i++)
        {
            var weights = CalculateBoneWeights(meshObject, boneObjects, vertices[i]);
            NormalizeBoneWeights(weights, boneWeights, i);
        }

        mesh.boneWeights = boneWeights;
        mesh.bindposes = bindPoses;
        renderer.bones = bones;
        renderer.rootBone = rootBone;
    }

    private static List<BoneWeight> CalculateBoneWeights(GameObject meshObject, List<MiniBoneData> boneObjects, Vector3 vertex)
    {
        Vector3 worldPosition = meshObject.transform.TransformPoint(vertex);
        List<BoneWeight> weights = new List<BoneWeight>();

        float maxRaw = 0f;
        float[] rawWeights = new float[boneObjects.Count];

        for (int j = 0; j < boneObjects.Count; j++)
        {
            var boneData = boneObjects[j];
            float totalBoneWeight = 0f;

            float dist = Vector3.Distance(worldPosition, boneData.bone.position);
            if (dist <= boneData.influenceRadius)
            {
                float t = dist / boneData.influenceRadius;
                float evalX = 1.0f - t;
                float falloff = boneData.falloffCurve != null ? boneData.falloffCurve.Evaluate(evalX) : 1.0f;
                totalBoneWeight += falloff * boneData.influenceStrength;
            }

            foreach (Transform child in boneData.bone)
            {
                if (child.name.StartsWith("HelperNode"))
                {
                    float hDist = Vector3.Distance(worldPosition, child.position);
                    if (hDist <= boneData.helperRadius)
                    {
                        float ht = hDist / boneData.helperRadius;
                        float hEvalX = 1.0f - ht;
                        float hFalloff = boneData.falloffCurve != null ? boneData.falloffCurve.Evaluate(hEvalX) : 1.0f;
                        totalBoneWeight += hFalloff * boneData.helperStrength;
                    }
                }
            }

            rawWeights[j] = totalBoneWeight;
            if (rawWeights[j] > maxRaw) maxRaw = rawWeights[j];
        }

        float excess = Mathf.Max(0f, maxRaw - 1.0f);

        for (int j = 0; j < boneObjects.Count; j++)
        {
            if (rawWeights[j] > 0f)
            {
                float finalRaw = Mathf.Max(0f, rawWeights[j] - excess);

                if (finalRaw > 0.001f)
                {
                    weights.Add(new BoneWeight { boneIndex0 = j + 1, weight0 = finalRaw });
                }
            }
        }

        return weights;
    }

    private static void NormalizeBoneWeights(List<BoneWeight> weights, BoneWeight[] boneWeights, int index)
    {
        weights.Sort((a, b) => b.weight0.CompareTo(a.weight0));

        float totalChildWeight = 0f;
        foreach (var w in weights) totalChildWeight += w.weight0;

        if (totalChildWeight < 1.0f)
        {
            weights.Add(new BoneWeight { boneIndex0 = 0, weight0 = 1.0f - totalChildWeight });
            weights.Sort((a, b) => b.weight0.CompareTo(a.weight0));
        }

        int count = Mathf.Min(4, weights.Count);
        BoneWeight finalWeight = new BoneWeight();
        float finalSum = 0;

        for (int w = 0; w < count; w++)
        {
            finalSum += weights[w].weight0;
            if (w == 0) { finalWeight.boneIndex0 = weights[w].boneIndex0; finalWeight.weight0 = weights[w].weight0; }
            else if (w == 1) { finalWeight.boneIndex1 = weights[w].boneIndex0; finalWeight.weight1 = weights[w].weight0; }
            else if (w == 2) { finalWeight.boneIndex2 = weights[w].boneIndex0; finalWeight.weight2 = weights[w].weight0; }
            else if (w == 3) { finalWeight.boneIndex3 = weights[w].boneIndex0; finalWeight.weight3 = weights[w].weight0; }
        }

        if (finalSum > 0)
        {
            finalWeight.weight0 /= finalSum;
            finalWeight.weight1 /= finalSum;
            finalWeight.weight2 /= finalSum;
            finalWeight.weight3 /= finalSum;
        }
        else
        {
            finalWeight.boneIndex0 = 0;
            finalWeight.weight0 = 1.0f;
        }

        boneWeights[index] = finalWeight;
    }
}