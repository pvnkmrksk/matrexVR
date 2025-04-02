/*           INFINITY CODE          */
/*     https://infinity-code.com    */

using System;
using UnityEditor;
using UnityEngine;

namespace InfinityCode.MeshToTerrain
{
    [CustomEditor(typeof(MeshToTerrainBoundsHelper))]
    public class MeshToTerrainBoundsHelperEditor : Editor
    {
        private BoxEditor boxEditor;

        private MeshToTerrainBoundsHelper helper;

        private void OnDisable()
        {
            if (helper)
            {
                DestroyImmediate(helper.gameObject);
                if (helper.OnDestroyed != null) helper.OnDestroyed();
            }
        }

        private void OnEnable()
        {
            try
            {
                helper = target as MeshToTerrainBoundsHelper;

                if (!target || !helper)
                {
                    return;
                }

                if (helper.bounds == default)
                {
                    DestroyImmediate(helper);
                    return;
                }

                boxEditor = new BoxEditor(true, -1);
            }
            catch
            {
            }
        }

        public void OnSceneGUI()
        {
            Color color = new Color32(145, 244, 139, 210);

            Vector3 center = helper.bounds.center;
            Vector3 size = helper.bounds.size;

            if (boxEditor.OnSceneGUI(helper.transform, color, ref center, ref size))
            {
                helper.bounds.center = center;
                helper.bounds.size = size;
                if (helper.OnBoundChanged != null) helper.OnBoundChanged();
            }
        }

        internal class BoxEditor
        {
            private int m_ControlIdHint;
            private bool m_DisableZaxis;
            private bool m_UseLossyScale;
            private static float s_ScaleSnap = float.MinValue;

            private static float SnapSettingsScale
            {
                get
                {
                    if (Math.Abs(s_ScaleSnap - float.MinValue) < float.Epsilon)
                    {
                        s_ScaleSnap = EditorPrefs.GetFloat("ScaleSnap", 0.1f);
                    }

                    return s_ScaleSnap;
                }
            }

            public BoxEditor(bool useLossyScale, int controlIdHint)
            {
                m_UseLossyScale = useLossyScale;
                m_ControlIdHint = controlIdHint;
            }

            private void AdjustMidpointHandleColor(Vector3 localPos, Vector3 localTangent, Vector3 localBinormal, Matrix4x4 transform, float alphaFactor)
            {
                float num;
                Vector3 vector = transform.MultiplyPoint(localPos);
                Vector3 lhs = transform.MultiplyVector(localTangent);
                Vector3 rhs = transform.MultiplyVector(localBinormal);
                Vector3 normalized = Vector3.Cross(lhs, rhs).normalized;

                if (Camera.current.orthographic)
                {
                    num = Vector3.Dot(-Camera.current.transform.forward, normalized);
                }
                else
                {
                    Vector3 vector6 = Camera.current.transform.position - vector;
                    num = Vector3.Dot(vector6.normalized, normalized);
                }

                if (num < -0.0001f)
                {
                    alphaFactor *= 0.2f;
                }

                if (alphaFactor < 1f)
                {
                    Handles.color = new Color(Handles.color.r, Handles.color.g, Handles.color.b, Handles.color.a * alphaFactor);
                }
            }

            public void DrawWireframeBox(Vector3 center, Vector3 siz)
            {
                Vector3 vector = siz * 0.5f;
                Vector3[] points = {center + new Vector3(-vector.x, -vector.y, -vector.z), center + new Vector3(-vector.x, vector.y, -vector.z), center + new Vector3(vector.x, vector.y, -vector.z), center + new Vector3(vector.x, -vector.y, -vector.z), center + new Vector3(-vector.x, -vector.y, -vector.z), center + new Vector3(-vector.x, -vector.y, vector.z), center + new Vector3(-vector.x, vector.y, vector.z), center + new Vector3(vector.x, vector.y, vector.z), center + new Vector3(vector.x, -vector.y, vector.z), center + new Vector3(-vector.x, -vector.y, vector.z)};
                Handles.DrawPolyLine(points);
                Handles.DrawLine(points[1], points[6]);
                Handles.DrawLine(points[2], points[7]);
                Handles.DrawLine(points[3], points[8]);
            }

            private Vector3 MidpointHandle(Vector3 localPos, Vector3 localTangent, Vector3 localBinormal, Matrix4x4 transform)
            {
                Color color = Handles.color;
                float alphaFactor = 1f;
                AdjustMidpointHandleColor(localPos, localTangent, localBinormal, transform, alphaFactor);
                int controlID = GUIUtility.GetControlID(m_ControlIdHint, FocusType.Keyboard);
                if (alphaFactor > 0f)
                {
                    Vector3 normalized = Vector3.Cross(localTangent, localBinormal).normalized;
                    localPos = Slider1D.Do(
                        controlID, 
                        localPos, 
                        normalized, 
                        HandleUtility.GetHandleSize(localPos) * 0.03f,
                        Handles.DotHandleCap,
                        SnapSettingsScale);
                }

                Handles.color = color;
                return localPos;
            }

            private void MidpointHandles(ref Vector3 minPos, ref Vector3 maxPos, Matrix4x4 transform)
            {
                Vector3 localTangent = new Vector3(1f, 0f, 0f);
                Vector3 vector2 = new Vector3(0f, 1f, 0f);
                Vector3 localBinormal = new Vector3(0f, 0f, 1f);
                Vector3 vector4 = (maxPos + minPos) * 0.5f;
                Vector3 localPos = new Vector3(maxPos.x, vector4.y, vector4.z);
                Vector3 vector6 = MidpointHandle(localPos, vector2, localBinormal, transform);
                maxPos.x = vector6.x;
                localPos = new Vector3(minPos.x, vector4.y, vector4.z);
                vector6 = MidpointHandle(localPos, vector2, -localBinormal, transform);
                minPos.x = vector6.x;
                localPos = new Vector3(vector4.x, maxPos.y, vector4.z);
                vector6 = MidpointHandle(localPos, localTangent, -localBinormal, transform);
                maxPos.y = vector6.y;
                localPos = new Vector3(vector4.x, minPos.y, vector4.z);
                vector6 = MidpointHandle(localPos, localTangent, localBinormal, transform);
                minPos.y = vector6.y;
                if (!m_DisableZaxis)
                {
                    localPos = new Vector3(vector4.x, vector4.y, maxPos.z);
                    vector6 = MidpointHandle(localPos, vector2, -localTangent, transform);
                    maxPos.z = vector6.z;
                    localPos = new Vector3(vector4.x, vector4.y, minPos.z);
                    vector6 = MidpointHandle(localPos, vector2, localTangent, transform);
                    minPos.z = vector6.z;
                }
            }

            public bool OnSceneGUI(Matrix4x4 transform, Color color, ref Vector3 center, ref Vector3 size)
            {
                Color color2 = Handles.color;
                Handles.color = color;
                Vector3 minPos = center - size * 0.5f;
                Vector3 maxPos = center + size * 0.5f;
                Matrix4x4 matrix = Handles.matrix;
                Handles.matrix = transform;
                DrawWireframeBox((maxPos - minPos) * 0.5f + minPos, maxPos - minPos);
                MidpointHandles(ref minPos, ref maxPos, Handles.matrix);

                bool changed = GUI.changed;
                if (changed)
                {
                    center = (maxPos + minPos) * 0.5f;
                    size = maxPos - minPos;
                }

                Handles.color = color2;
                Handles.matrix = matrix;
                return changed;
            }

            public bool OnSceneGUI(Transform transform, Color color, ref Vector3 center, ref Vector3 size)
            {
                if (m_UseLossyScale)
                {
                    Matrix4x4 matrixx = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                    size.Scale(transform.lossyScale);
                    center = transform.TransformPoint(center);
                    center = matrixx.inverse.MultiplyPoint(center);
                    bool flag = OnSceneGUI(matrixx, color, ref center, ref size);
                    center = matrixx.MultiplyPoint(center);
                    center = transform.InverseTransformPoint(center);
                    size.Scale(new Vector3(1f / transform.lossyScale.x, 1f / transform.lossyScale.y, 1f / transform.lossyScale.z));
                    return flag;
                }

                return OnSceneGUI(transform.localToWorldMatrix, color, ref center, ref size);
            }
        }

        internal class Slider1D
        {
            private static Vector2 s_CurrentMousePosition;
            private static Vector2 s_StartMousePosition;
            private static Vector3 s_StartPosition;

            internal static Vector3 Do(int id, Vector3 position, Vector3 direction, float size, Handles.CapFunction drawFunc, float snap)
            {
                return Do(id, position, direction, direction, size, drawFunc, snap);
            }

            internal static Vector3 Do(int id, Vector3 position, Vector3 handleDirection, Vector3 slideDirection, float size, Handles.CapFunction drawFunc, float snap)
            {
                Event current = Event.current;
                switch (current.GetTypeForControl(id))
                {
                    case EventType.MouseDown:
                        if ((HandleUtility.nearestControl == id && current.button == 0 || GUIUtility.keyboardControl == id && current.button == 2) && GUIUtility.hotControl == 0)
                        {
                            int num2 = id;
                            GUIUtility.keyboardControl = num2;
                            GUIUtility.hotControl = num2;
                            s_CurrentMousePosition = s_StartMousePosition = current.mousePosition;
                            s_StartPosition = position;
                            current.Use();
                            EditorGUIUtility.SetWantsMouseJumping(1);
                        }

                        return position;

                    case EventType.MouseUp:
                        if (GUIUtility.hotControl == id && (current.button == 0 || current.button == 2))
                        {
                            GUIUtility.hotControl = 0;
                            current.Use();
                            EditorGUIUtility.SetWantsMouseJumping(0);
                        }

                        return position;

                    case EventType.MouseMove:
                    case EventType.KeyDown:
                    case EventType.KeyUp:
                    case EventType.ScrollWheel:
                        return position;

                    case EventType.MouseDrag:
                        if (GUIUtility.hotControl == id)
                        {
                            s_CurrentMousePosition += current.delta;
                            float num = Handles.SnapValue(HandleUtility.CalcLineTranslation(s_StartMousePosition, s_CurrentMousePosition, s_StartPosition, slideDirection), snap);
                            Vector3 vector = Handles.matrix.MultiplyVector(slideDirection);
                            Vector3 v = Handles.matrix.MultiplyPoint(s_StartPosition) + vector * num;
                            position = Handles.matrix.inverse.MultiplyPoint(v);
                            GUI.changed = true;
                            current.Use();
                        }

                        return position;

                    case EventType.Repaint:
                    {
                        Color white = Color.white;
                        if (id == GUIUtility.keyboardControl && GUI.enabled)
                        {
                            white = Handles.color;
                            Handles.color = Color.green;
                        }

                        drawFunc(id, position, Quaternion.LookRotation(handleDirection), size, current.type);
                        
                        if (id == GUIUtility.keyboardControl) Handles.color = white;

                        return position;
                    }
                    case EventType.Layout:
                        if (drawFunc != Handles.ArrowHandleCap)
                        {
                            HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position, size * 0.2f));
                            return position;
                        }

                        HandleUtility.AddControl(id, HandleUtility.DistanceToLine(position, position + slideDirection * size));
                        HandleUtility.AddControl(id, HandleUtility.DistanceToCircle(position + slideDirection * size, size * 0.2f));
                        return position;
                }

                return position;
            }
        }
    }
}