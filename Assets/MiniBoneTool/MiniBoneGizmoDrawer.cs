using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public class MiniBoneGizmoDrawer : MonoBehaviour
{
    public List<MiniBoneData> boneObjects;

    [HideInInspector]
    public float gizmoSize = 2.0f; // 기본값 변경

    [HideInInspector]
    public float boneSize = 2.0f;

    private void OnDrawGizmos()
    {
        if (boneObjects == null || boneObjects.Count == 0)
            return;

        var skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer == null)
        {
            ConvertToSkinnedMeshRenderer();
            skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();
        }

        Mesh mesh = skinnedMeshRenderer?.sharedMesh;
        if (mesh == null)
            return;

        Vector3[] vertices = mesh.vertices;
        Camera sceneCamera = Camera.current;
        if (sceneCamera == null) return;

        foreach (var boneData in boneObjects)
        {
            if (boneData.bone == null)
                continue;

            Vector3 bonePosition = boneData.bone.position;
            float distanceToCamera = Vector3.Distance(sceneCamera.transform.position, bonePosition);
            float adjustedGizmoSize = gizmoSize * (distanceToCamera / 10f); // 거리 기반 크기 조정
            float adjustedBoneSize = boneSize * (distanceToCamera / 10f); // 거리 기반 크기 조정

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldVertex = transform.TransformPoint(vertices[i]);
                float distance = Vector3.Distance(worldVertex, bonePosition);

                if (distance <= boneData.influenceRadius)
                {
                    float weight = Mathf.Clamp01(boneData.influenceStrength * (1.0f - (distance / boneData.influenceRadius)));
                    Gizmos.color = boneData.color * new Color(1f, 1f, 1f, weight); // 본 컬러와 투명도 적용
                    Gizmos.DrawSphere(worldVertex, adjustedGizmoSize * weight * 0.1f); // Weight에 따라 크기 조정
                }
            }

            // Bone 위치 표시
            Gizmos.color = Color.white; // 본은 항상 흰색으로 표시
            Gizmos.DrawCube(bonePosition, Vector3.one * (adjustedBoneSize * 0.1f)); // 큐브 크기 조정

            // Bone 간 연결선
            foreach (Transform child in boneData.bone)
            {
                if (child != null)
                {
                    Gizmos.color = Color.white; // 연결선도 흰색으로 표시
                    Gizmos.DrawLine(bonePosition, child.position);
                }
            }
        }
    }

    private void ConvertToSkinnedMeshRenderer()
    {
        var meshRenderer = GetComponent<MeshRenderer>();
        var meshFilter = GetComponent<MeshFilter>();

        if (meshRenderer == null || meshFilter == null)
        {
            Debug.LogError("MeshRenderer 또는 MeshFilter가 없습니다.");
            return;
        }

        var originalMaterials = meshRenderer.sharedMaterials;
        var originalMesh = meshFilter.sharedMesh;

        DestroyImmediate(meshRenderer);
        DestroyImmediate(meshFilter);

        var skinnedMeshRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = originalMesh;
        skinnedMeshRenderer.sharedMaterials = originalMaterials;

        Debug.Log("MeshRenderer가 SkinnedMeshRenderer로 변환되었습니다.");
    }
}
