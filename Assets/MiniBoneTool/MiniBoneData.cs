using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class MiniBoneData
{
    public Transform bone;
    public float influenceRadius = 1.0f;
    public float influenceStrength = 1.0f;
    public Color color = Color.white;

    // 오르막(0~1) 커브 유지
    public AnimationCurve falloffCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    // 헬퍼 노드가 공유할 설정값
    public float helperRadius = 0.5f;
    public float helperStrength = 1.0f;

    [HideInInspector]
    public bool showHelperUI = false;

    // --- 리깅을 적용했던 '원래 위치' 기억용 ---
    [HideInInspector] public Vector3 savedLocalPosition;
    [HideInInspector] public Quaternion savedLocalRotation = Quaternion.identity;

    [HideInInspector] public List<Vector3> helperLocalPositions = new List<Vector3>();
    [HideInInspector] public List<Quaternion> helperLocalRotations = new List<Quaternion>();
}