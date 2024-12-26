using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// 리깅 로직을 전담하는 유틸리티 클래스
/// </summary>
public static class MiniBoneUtility
{
    /// <summary>
    /// 실제 리깅을 시작하는 메서드
    /// </summary>
    public static void StartRigging(GameObject meshObject, Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (meshObject == null || rootBone == null)
        {
            Debug.LogError("Mesh 오브젝트와 Root Bone을 설정해주세요.");
            return;
        }

        // 1. SkinnedMeshRenderer 확인
        var renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
        if (renderer == null)
        {
            ConvertToSkinnedMeshRenderer(meshObject);
            renderer = meshObject.GetComponent<SkinnedMeshRenderer>();
        }

        // 2. 대상 메쉬
        var originalMesh = renderer.sharedMesh;
        if (originalMesh == null)
        {
            Debug.LogError("Mesh 오브젝트에 유효한 Mesh가 없습니다.");
            return;
        }

        Debug.Log($"[Check Rig] bindposes: {originalMesh.bindposes?.Length}, boneWeights: {originalMesh.boneWeights?.Length}");

        // 3. 이미 스킨 정보가 있는지 확인
        bool hasBindPoses = (originalMesh.bindposes != null && originalMesh.bindposes.Length > 0);
        bool hasBoneWeights = (originalMesh.boneWeights != null && originalMesh.boneWeights.Length > 0);
        bool isAlreadyRigged = hasBindPoses && hasBoneWeights;

        if (isAlreadyRigged)
        {
            Debug.Log("이미 스킨 정보(리깅 데이터)가 포함된 메쉬입니다. 기존 메쉬에 덮어씌웁니다.");
            ConfigureRigging(meshObject, rootBone, boneObjects, renderer, originalMesh);
        }
        else
        {
            Debug.Log("스킨 정보가 없는 메쉬입니다. 새로운 메쉬를 생성하여 리깅 데이터를 적용합니다.");

            var newMesh = Object.Instantiate(originalMesh);
            newMesh.name = originalMesh.name + "_Rigged";

            // 4. MiniBoneUtility 에셋 경로를 기준으로 Mesh 저장 경로 생성
            string assetPath = AssetDatabase.GetAssetPath(originalMesh);
            string savePath = GetMeshSavePath(newMesh.name);

            if (string.IsNullOrEmpty(savePath))
            {
                Debug.LogError("Mesh 저장 경로를 찾을 수 없습니다.");
                return;
            }

            // 5. 새 메쉬를 에셋으로 생성
            AssetDatabase.CreateAsset(newMesh, savePath);
            AssetDatabase.SaveAssets();

            // SkinnedMeshRenderer에 새 메쉬 적용
            renderer.sharedMesh = newMesh;

            ConfigureRigging(meshObject, rootBone, boneObjects, renderer, newMesh);

            Debug.Log($"리깅 완료: '{savePath}' 에 새로운 리깅 메쉬가 저장되었습니다.");
        }
    }

    /// <summary>
    /// MiniBoneUtility 에셋 경로를 기준으로 Meshs 폴더 경로를 찾고 고유한 저장 경로를 반환
    /// </summary>
    private static string GetMeshSavePath(string meshName)
    {
        string utilityPath = GetAssetPath("MiniBoneUtility");


        if (string.IsNullOrEmpty(utilityPath))
        {
            Debug.LogError("MiniBoneUtility 에셋 경로를 찾을 수 없습니다.");
            return null;
        }

        // MiniBoneUtility 상위 폴더에서 Meshs 폴더 확인
        string directory = System.IO.Path.GetDirectoryName(utilityPath);

        string meshFolderPath = System.IO.Path.Combine(directory, "Meshs");

        meshFolderPath = meshFolderPath.Replace("\\Editor", "");

        // Meshs 폴더가 존재하지 않으면 생성
        if (!AssetDatabase.IsValidFolder(meshFolderPath))
        {
            AssetDatabase.CreateFolder(meshFolderPath.Replace("\\Meshs", ""), "Meshs");
        }

        // 고유한 파일 이름 생성 (001, 002 등 추가)
        string basePath = System.IO.Path.Combine(meshFolderPath, meshName);
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(basePath + ".asset");

        return uniquePath;
    }

    //프로젝트에서 해당 이름이 포함된 에셋을 모두 찾는다.
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
    /// 기존 MeshRenderer & MeshFilter를 SkinnedMeshRenderer로 변환
    /// </summary>
    private static void ConvertToSkinnedMeshRenderer(GameObject meshObject)
    {
        var meshRenderer = meshObject.GetComponent<MeshRenderer>();
        var meshFilter = meshObject.GetComponent<MeshFilter>();

        if (meshRenderer == null || meshFilter == null)
        {
            Debug.LogError("MeshRenderer 또는 MeshFilter가 없습니다.");
            return;
        }

        var originalMaterials = meshRenderer.sharedMaterials;
        var originalMesh = meshFilter.sharedMesh;

        // 기존 컴포넌트 삭제
        Object.DestroyImmediate(meshRenderer);
        Object.DestroyImmediate(meshFilter);

        // SkinnedMeshRenderer 추가
        var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = originalMesh;
        skinnedMeshRenderer.sharedMaterials = originalMaterials;

        Debug.Log("MeshRenderer가 SkinnedMeshRenderer로 변환되었습니다.");
    }

    /// <summary>
    /// 본 배열과 바인드포즈, 버텍스 BoneWeight 설정
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

        // 버텍스별 본 웨이트 계산
        AssignBoneWeights(meshObject, boneObjects, vertices, boneWeights);

        // 리깅 정보 반영
        mesh.boneWeights = boneWeights;
        mesh.bindposes = bindPoses;

        // 스키닝 정보 적용
        renderer.bones = bones;
        renderer.rootBone = rootBone;
    }

    /// <summary>
    /// 버텍스 배열을 순회하며 본 웨이트 계산
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
    /// 한 버텍스에 대해 본 웨이트 후보 목록 산출
    /// </summary>
    private static List<BoneWeight> CalculateBoneWeights(GameObject meshObject,
                                                         List<MiniBoneData> boneObjects,
                                                         Vector3 vertex)
    {
        Vector3 worldPosition = meshObject.transform.TransformPoint(vertex);
        List<BoneWeight> weights = new List<BoneWeight>();

        // rootBone은 boneIndex=0, child bone은 (j+1)
        for (int j = 0; j < boneObjects.Count; j++)
        {
            var boneData = boneObjects[j];
            float distance = Vector3.Distance(worldPosition, boneData.bone.position) * 0.99f;

            // 영향 범위 안이면 웨이트 계산
            if (distance <= boneData.influenceRadius)
            {
                float weight = Mathf.Clamp01(
                    boneData.influenceStrength * (1.0f - (distance / boneData.influenceRadius))
                );

                weights.Add(new BoneWeight
                {
                    boneIndex0 = j + 1, // 0은 RootBone
                    weight0 = weight
                });
            }
        }

        // 어느 본에도 영향을 받지 못하면 rootBone(0) 웨이트 = 1
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
    /// 최대 4개의 BoneWeight만 사용 & 정규화
    /// </summary>
    private static void NormalizeBoneWeights(List<BoneWeight> weights,
                                             BoneWeight[] boneWeights,
                                             int index)
    {
        // 웨이트 내림차순 정렬
        weights.Sort((a, b) => b.weight0.CompareTo(a.weight0));

        // 최대 4개까지만
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

        // 합이 1보다 작으면 남은 웨이트를 비어있는 곳에 채움
        if (totalWeight < 1.0f)
        {
            float remaining = 1.0f - totalWeight;
            if (finalWeight.weight0 == 0f) finalWeight.weight0 = remaining;
            else if (finalWeight.weight1 == 0f) finalWeight.weight1 = remaining;
            else if (finalWeight.weight2 == 0f) finalWeight.weight2 = remaining;
            else if (finalWeight.weight3 == 0f) finalWeight.weight3 = remaining;
        }

        // 최종 정규화
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

        // boneWeights 배열에 세팅
        boneWeights[index] = finalWeight;
    }
}
