#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

public class MiniBoneSaveData : MonoBehaviour
{
#if UNITY_EDITOR
    // 커브 데이터를 안전하게 저장하기 위해 구조체를 통째로 저장
    public List<MiniBoneData> savedBoneData = new List<MiniBoneData>();

    private void Awake()
    {
        if (!Application.isEditor)
            return;

        Debug.Log("Awake - Editor Only");
    }
#endif
}