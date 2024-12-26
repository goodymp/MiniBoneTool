using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public class MiniBoneGizmoDrawer : MonoBehaviour
{
    public List<MiniBoneData> boneObjects;

    [HideInInspector]
    public float gizmoSize = 2.0f; // �⺻�� ����

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
            float adjustedGizmoSize = gizmoSize * (distanceToCamera / 10f); // �Ÿ� ��� ũ�� ����
            float adjustedBoneSize = boneSize * (distanceToCamera / 10f); // �Ÿ� ��� ũ�� ����

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldVertex = transform.TransformPoint(vertices[i]);
                float distance = Vector3.Distance(worldVertex, bonePosition);

                if (distance <= boneData.influenceRadius)
                {
                    float weight = Mathf.Clamp01(boneData.influenceStrength * (1.0f - (distance / boneData.influenceRadius)));
                    Gizmos.color = boneData.color * new Color(1f, 1f, 1f, weight); // �� �÷��� ���� ����
                    Gizmos.DrawSphere(worldVertex, adjustedGizmoSize * weight * 0.1f); // Weight�� ���� ũ�� ����
                }
            }

            // Bone ��ġ ǥ��
            Gizmos.color = Color.white; // ���� �׻� ������� ǥ��
            Gizmos.DrawCube(bonePosition, Vector3.one * (adjustedBoneSize * 0.1f)); // ť�� ũ�� ����

            // Bone �� ���ἱ
            foreach (Transform child in boneData.bone)
            {
                if (child != null)
                {
                    Gizmos.color = Color.white; // ���ἱ�� ������� ǥ��
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
            Debug.LogError("MeshRenderer �Ǵ� MeshFilter�� �����ϴ�.");
            return;
        }

        var originalMaterials = meshRenderer.sharedMaterials;
        var originalMesh = meshFilter.sharedMesh;

        DestroyImmediate(meshRenderer);
        DestroyImmediate(meshFilter);

        var skinnedMeshRenderer = gameObject.AddComponent<SkinnedMeshRenderer>();
        skinnedMeshRenderer.sharedMesh = originalMesh;
        skinnedMeshRenderer.sharedMaterials = originalMaterials;

        Debug.Log("MeshRenderer�� SkinnedMeshRenderer�� ��ȯ�Ǿ����ϴ�.");
    }
}
