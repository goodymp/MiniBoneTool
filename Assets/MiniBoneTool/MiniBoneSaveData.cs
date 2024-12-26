#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections.Generic;
using UnityEngine;

public class MiniBoneSaveData : MonoBehaviour
{
#if UNITY_EDITOR
    public List<string> boneInfoList = new List<string>();

    private void Awake()
    {
        // �����Ϳ����� �����ϵ���
        if (!Application.isEditor)
            return;

        Debug.Log("Awake - Editor Only");
    }
#endif
}
