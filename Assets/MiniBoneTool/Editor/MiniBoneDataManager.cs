using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class MiniBoneDataManager
{
    public static void LoadBoneData(Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (rootBone == null) return;

        var saveData = rootBone.GetComponent<MiniBoneSaveData>();
        if (saveData == null)
        {
            Debug.LogWarning("RootBone�� RiggingSaveData�� �����ϴ�. �ҷ����� �Ұ�");
            return;
        }

        boneObjects.Clear();

        string pattern = @"BoneName\s*=\s*(.*?),\s*Radius\s*=\s*(.*?),\s*Strength\s*=\s*(.*?),\s*Position\s*=\s*(.*)";
        Regex regex = new Regex(pattern);

        foreach (var line in saveData.boneInfoList)
        {
            var match = regex.Match(line);
            if (!match.Success)
            {
                Debug.LogWarning($"�Ľ� ����: {line}");
                continue;
            }

            string boneName = match.Groups[1].Value.Trim();
            string radiusStr = match.Groups[2].Value.Trim();
            string strengthStr = match.Groups[3].Value.Trim();
            string posStr = match.Groups[4].Value.Trim();

            float.TryParse(radiusStr, out float radius);
            float.TryParse(strengthStr, out float strength);

            Vector3 localPos = Vector3.zero;
            var posArr = posStr.Split(',');
            if (posArr.Length == 3)
            {
                float.TryParse(posArr[0], out localPos.x);
                float.TryParse(posArr[1], out localPos.y);
                float.TryParse(posArr[2], out localPos.z);
            }

            Transform foundTransform = FindDeepChild(rootBone, boneName);
            if (foundTransform == null)
            {
                Debug.LogWarning($"'{boneName}'��(��) RootBone �ڽĿ��� ã�� �� �����ϴ�.");
                continue;
            }

            var newBoneData = new MiniBoneData
            {
                bone = foundTransform,
                influenceRadius = radius,
                influenceStrength = strength,
                color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.8f, 1f)
            };
            boneObjects.Add(newBoneData);

            // ���� ��ġ ����
            foundTransform.localPosition = localPos;
        }

        Debug.Log($"�� ������ �ҷ����� �Ϸ�! �� {boneObjects.Count}�� �����.");
    }

    public static void SaveBoneData(Transform rootBone, List<MiniBoneData> boneObjects)
    {
        if (rootBone == null) return;

        var saveData = rootBone.GetComponent<MiniBoneSaveData>();
        if (saveData == null)
        {
            saveData = rootBone.gameObject.AddComponent<MiniBoneSaveData>();
        }

        saveData.boneInfoList.Clear();
        foreach (var bd in boneObjects)
        {
            if (bd.bone == null) continue;

            string boneName = bd.bone.name;
            float radius = bd.influenceRadius;
            float strength = bd.influenceStrength;
            Vector3 pos = bd.bone.localPosition;

            string infoStr = $"BoneName={boneName}, Radius={radius}, Strength={strength}, Position={pos.x},{pos.y},{pos.z}";
            saveData.boneInfoList.Add(infoStr);
        }

        Debug.Log($"�� {boneObjects.Count}���� Bone ������ RootBone�� ����Ǿ����ϴ�. (��ġ ����)");
    }

    private static Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName) return child;
            var result = FindDeepChild(child, childName);
            if (result != null) return result;
        }
        return null;
    }
}
