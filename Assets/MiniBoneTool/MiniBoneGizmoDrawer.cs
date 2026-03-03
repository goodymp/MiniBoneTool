using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class MiniBoneGizmoDrawer : MonoBehaviour
{
    public List<MiniBoneData> boneObjects;

    [HideInInspector] public float gizmoSize = 2.0f;
    [HideInInspector] public float boneSize = 2.0f;

    private Mesh bakedMesh;

    private void OnDrawGizmos()
    {
        if (boneObjects == null || boneObjects.Count == 0) return;

        Mesh originalMesh = null;
        var smr = GetComponent<SkinnedMeshRenderer>();
        bool isRigged = false;

        if (smr != null)
        {
            originalMesh = smr.sharedMesh;
            if (originalMesh != null && originalMesh.boneWeights != null && originalMesh.boneWeights.Length > 0)
            {
                isRigged = true;
            }
        }
        else
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null) originalMesh = mf.sharedMesh;
        }

        if (originalMesh == null) return;

        Vector3[] drawVertices = originalMesh.vertices;

        if (isRigged)
        {
            if (bakedMesh == null) bakedMesh = new Mesh();
            smr.BakeMesh(bakedMesh);
            drawVertices = bakedMesh.vertices;
        }

        Camera sceneCamera = Camera.current;
        if (sceneCamera == null) return;

        GameObject selectedTarget = Selection.activeGameObject;

        int[] boneIndices = new int[boneObjects.Count];
        if (isRigged)
        {
            for (int j = 0; j < boneObjects.Count; j++)
            {
                boneIndices[j] = System.Array.IndexOf(smr.bones, boneObjects[j].bone);
            }
        }

        for (int i = 0; i < originalMesh.vertices.Length; i++)
        {
            Vector3 worldVertex = transform.TransformPoint(drawVertices[i]);

            if (isRigged)
            {
                BoneWeight bw = originalMesh.boneWeights[i];

                for (int j = 0; j < boneObjects.Count; j++)
                {
                    int bIndex = boneIndices[j];
                    if (bIndex == -1) continue;

                    float finalWeight = 0f;
                    if (bw.boneIndex0 == bIndex) finalWeight += bw.weight0;
                    if (bw.boneIndex1 == bIndex) finalWeight += bw.weight1;
                    if (bw.boneIndex2 == bIndex) finalWeight += bw.weight2;
                    if (bw.boneIndex3 == bIndex) finalWeight += bw.weight3;

                    if (finalWeight > 0.001f)
                    {
                        DrawGizmoSphere(worldVertex, boneObjects[j], finalWeight, selectedTarget, sceneCamera);
                    }
                }
            }
            else
            {
                Vector3 originalWorldVertex = transform.TransformPoint(originalMesh.vertices[i]);

                float[] rawWeights = new float[boneObjects.Count];
                float maxRaw = 0f;

                for (int j = 0; j < boneObjects.Count; j++)
                {
                    var bd = boneObjects[j];
                    if (bd.bone == null) continue;

                    float totalBoneWeight = 0f;

                    float dist = Vector3.Distance(originalWorldVertex, bd.bone.position);
                    if (dist <= bd.influenceRadius && bd.influenceRadius > 0.0001f)
                    {
                        float t = dist / bd.influenceRadius;
                        float evalX = 1.0f - t;
                        float falloff = bd.falloffCurve != null ? bd.falloffCurve.Evaluate(evalX) : 1.0f;
                        totalBoneWeight += falloff * bd.influenceStrength;
                    }

                    foreach (Transform child in bd.bone)
                    {
                        if (child.name.StartsWith("HelperNode"))
                        {
                            // [기능 추가] 실시간 미리보기(기즈모)에서도 Scale값을 곱해서 계산
                            float finalHRadius = bd.helperRadius * Mathf.Abs(child.lossyScale.x);
                            float finalHStrength = bd.helperStrength * Mathf.Abs(child.lossyScale.y);

                            float hDist = Vector3.Distance(originalWorldVertex, child.position);
                            if (hDist <= finalHRadius && finalHRadius > 0.0001f)
                            {
                                float ht = hDist / finalHRadius;
                                float hEvalX = 1.0f - ht;
                                float hFalloff = bd.falloffCurve != null ? bd.falloffCurve.Evaluate(hEvalX) : 1.0f;
                                totalBoneWeight += hFalloff * finalHStrength;
                            }
                        }
                    }

                    rawWeights[j] = totalBoneWeight;
                    if (rawWeights[j] > maxRaw) maxRaw = rawWeights[j];
                }

                float excess = Mathf.Max(0f, maxRaw - 1.0f);
                float sum = 0f;

                for (int j = 0; j < boneObjects.Count; j++)
                {
                    if (rawWeights[j] > 0f)
                    {
                        rawWeights[j] = Mathf.Max(0f, rawWeights[j] - excess);
                        sum += rawWeights[j];
                    }
                }

                for (int j = 0; j < boneObjects.Count; j++)
                {
                    float w = rawWeights[j];
                    if (w > 0.001f)
                    {
                        float finalWeight = (sum > 1.0f) ? (w / sum) : w;
                        DrawGizmoSphere(worldVertex, boneObjects[j], finalWeight, selectedTarget, sceneCamera);
                    }
                }
            }
        }

        foreach (var boneData in boneObjects)
        {
            if (boneData.bone == null) continue;

            bool isSelected = (selectedTarget == boneData.bone.gameObject);
            Vector3 bonePosition = boneData.bone.position;
            float distCam = Vector3.Distance(sceneCamera.transform.position, bonePosition);
            float adjBone = boneSize * (distCam / 10f);

            Gizmos.color = isSelected ? Color.yellow : Color.white;
            Gizmos.DrawCube(bonePosition, Vector3.one * (adjBone * 0.12f));

            foreach (Transform child in boneData.bone)
            {
                if (child != null)
                {
                    if (child.name.StartsWith("HelperNode"))
                    {
                        bool isHelperSelected = (selectedTarget == child.gameObject);
                        Gizmos.color = isHelperSelected ? Color.green : Color.cyan;
                        Gizmos.DrawWireSphere(child.position, adjBone * 0.08f);
                    }
                    else
                    {
                        Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
                        Gizmos.DrawLine(bonePosition, child.position);
                    }
                }
            }
        }
    }

    private void DrawGizmoSphere(Vector3 worldPos, MiniBoneData bd, float weight, GameObject selectedTarget, Camera sceneCamera)
    {
        bool isSelected = (selectedTarget == bd.bone.gameObject);
        foreach (Transform child in bd.bone)
        {
            if (child.gameObject == selectedTarget) isSelected = true;
        }

        Color finalGizmoColor = (weight >= 0.95f) ? Color.yellow : bd.color;

        if (isSelected)
        {
            Color.RGBToHSV(finalGizmoColor, out float H, out float S, out float V);
            finalGizmoColor = Color.HSVToRGB(H, S * 0.5f, 1.0f);
        }

        float displayAlpha = Mathf.Lerp(0.3f, 1.0f, weight);
        float distCam = Vector3.Distance(sceneCamera.transform.position, bd.bone.position);
        float adjGizmo = gizmoSize * (distCam / 10f);
        float displaySize = adjGizmo * Mathf.Lerp(0.04f, 0.12f, weight);

        Gizmos.color = new Color(finalGizmoColor.r, finalGizmoColor.g, finalGizmoColor.b, displayAlpha);
        Gizmos.DrawSphere(worldPos, displaySize);
    }
}