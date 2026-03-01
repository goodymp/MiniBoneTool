using System.Collections.Generic;
using UnityEngine;

public static class MiniBoneDataManager
{
    public static void LoadBoneData(Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (rootBone == null) return;

        var saveData = rootBone.GetComponent<MiniBoneSaveData>();
        if (saveData == null)
        {
            Debug.LogWarning("RootBone에 MiniBoneSaveData가 없습니다. 불러오기 불가");
            return;
        }

        boneObjects.Clear();

        if (saveData.savedBoneData != null)
        {
            foreach (var data in saveData.savedBoneData)
            {
                if (data.bone == null) continue;

                var newData = new MiniBoneData
                {
                    bone = data.bone,
                    influenceRadius = data.influenceRadius,
                    influenceStrength = data.influenceStrength,
                    color = data.color,
                    falloffCurve = data.falloffCurve != null ? new AnimationCurve(data.falloffCurve.keys) : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f),
                    helperRadius = data.helperRadius,
                    helperStrength = data.helperStrength,
                    showHelperUI = data.showHelperUI,
                    savedLocalPosition = data.savedLocalPosition,
                    savedLocalRotation = data.savedLocalRotation,
                    helperLocalPositions = data.helperLocalPositions != null ? new List<Vector3>(data.helperLocalPositions) : new List<Vector3>(),
                    helperLocalRotations = data.helperLocalRotations != null ? new List<Quaternion>(data.helperLocalRotations) : new List<Quaternion>()
                };
                boneObjects.Add(newData);

                // --- 1. 본 위치 복구 ---
                newData.bone.localPosition = newData.savedLocalPosition;
                newData.bone.localRotation = newData.savedLocalRotation;

                // --- 2. 헬퍼 노드 완벽 복구 (기존 것 삭제 후 저장된 스냅샷으로 재생성) ---
                List<GameObject> oldHelpers = new List<GameObject>();
                foreach (Transform child in newData.bone)
                {
                    if (child.name.StartsWith("HelperNode"))
                    {
                        oldHelpers.Add(child.gameObject);
                    }
                }
                foreach (var go in oldHelpers)
                {
                    Object.DestroyImmediate(go);
                }

                for (int h = 0; h < newData.helperLocalPositions.Count; h++)
                {
                    GameObject helper = new GameObject($"HelperNode_{h + 1:D2}");
                    helper.transform.parent = newData.bone;
                    helper.transform.localPosition = newData.helperLocalPositions[h];
                    helper.transform.localRotation = newData.helperLocalRotations[h];
                }
            }
        }

        Debug.Log($"본 설정값 불러오기 완료! 총 {boneObjects.Count}개 적용됨.");
    }

    public static void SaveBoneData(Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (rootBone == null) return;

        var saveData = rootBone.GetComponent<MiniBoneSaveData>();
        if (saveData == null)
        {
            saveData = rootBone.gameObject.AddComponent<MiniBoneSaveData>();
        }

        saveData.savedBoneData.Clear();

        foreach (var bd in boneObjects)
        {
            if (bd.bone == null) continue;

            saveData.savedBoneData.Add(new MiniBoneData
            {
                bone = bd.bone,
                influenceRadius = bd.influenceRadius,
                influenceStrength = bd.influenceStrength,
                color = bd.color,
                falloffCurve = bd.falloffCurve != null ? new AnimationCurve(bd.falloffCurve.keys) : new AnimationCurve(),
                helperRadius = bd.helperRadius,
                helperStrength = bd.helperStrength,
                showHelperUI = bd.showHelperUI,
                savedLocalPosition = bd.savedLocalPosition,
                savedLocalRotation = bd.savedLocalRotation,
                helperLocalPositions = new List<Vector3>(bd.helperLocalPositions),
                helperLocalRotations = new List<Quaternion>(bd.helperLocalRotations)
            });
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(saveData);
#endif

        Debug.Log($"총 {boneObjects.Count}개의 Bone 정보가 RootBone에 저장되었습니다. (위치 스냅샷 포함)");
    }
}