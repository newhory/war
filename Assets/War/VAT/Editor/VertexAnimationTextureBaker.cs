using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.UIElements;
using ZLinq;
using Object = UnityEngine.Object;


namespace War.VAT.Editor
{
    public class VertexAnimationTextureBaker : EditorWindow
    {
#pragma warning disable UDR0001
        private static int s_baseTex;
        private static int s_vatTex;
        private static int s_min;
        private static int s_max;
#pragma warning restore UDR0001

        private ObjectField vatShaderGraphField;
        private ObjectField animatorField;
        private Vector2IntField maxTextureSizeField;
        private ListView clipListView;
        private ListView smrListView;
        private TextField savePathField;
        private Button pathSelectButton;
        private Button recommendButton;
        private Button bakeButton;
        private Label resultLabel;


        [Serializable]
        private class ClipFPSData
        {
            public string keyword;
            public AnimationClip clip;
            public int startFrame;
            public int endFrame;
            public int samplingFPS;
        }

        private class SMRData
        {
            public SkinnedMeshRenderer smr;
            public Vector3 min;
            public Vector3 max;
            public string vatTexturePath;
            public string vatMaterialPath;
        }


        private Shader vatShaderGraph;
        private Animator animator;
        private List<ClipFPSData> clipFPSList = new List<ClipFPSData>();
        private List<SMRData> smrList = new List<SMRData>();

        private int maxTexWidth;
        private int maxTexHeight;

        private int vatTexWidth;
        private int vatTexHeight;


        [MenuItem("Tools/VAT Baker (Optimized UI Toolkit)")]
        public static void ShowWindow()
        {
            var wnd = GetWindow<VertexAnimationTextureBaker>();
            wnd.titleContent = new GUIContent("VAT Baker (Optimized)");
            wnd.minSize = new Vector2(600, 500);
        }

        private void OnValidate()
        {
            s_baseTex = Shader.PropertyToID("_BaseTex");
            s_vatTex = Shader.PropertyToID("_VATTex");
            s_min = Shader.PropertyToID("_Min");
            s_max = Shader.PropertyToID("_Max");

            vatShaderGraph = Shader.Find("Shader Graphs/VAT");
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;

            vatShaderGraphField = new ObjectField("Shader Graph")
            {
                objectType = typeof(Shader),
                allowSceneObjects = false,
                value = vatShaderGraph
            };
            vatShaderGraphField.SetEnabled(false);
            root.Add(vatShaderGraphField);

            animatorField = new ObjectField("Animator")
            {
                objectType = typeof(Animator),
                allowSceneObjects = true
            };
            animatorField.RegisterCallback<ChangeEvent<Object>>(evt =>
            {
                animator = evt.newValue as Animator;
                if (animator != null)
                {
                    LoadClips(animator);
                    LoadSMRs(animator);
                    clipListView.Rebuild();
                    smrListView.Rebuild();
                }
            });
            root.Add(animatorField);

            maxTexWidth = maxTexHeight = 1024;
            maxTextureSizeField = new Vector2IntField("Max Texture Size")
            {
                value = new Vector2Int(maxTexWidth, maxTexHeight)
            };
            maxTextureSizeField.RegisterValueChangedCallback(evt =>
            {
                maxTexWidth = evt.newValue.x;
                maxTexHeight = evt.newValue.y;
            });
            root.Add(maxTextureSizeField);

            // AnimationClip 리스트 (읽기 전용)
            clipListView = new ListView
            {
                showFoldoutHeader = true,
                headerTitle = "Animation Clips",
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };
            clipListView.makeItem = () =>
            {
                var container = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                var clipField = new ObjectField { objectType = typeof(AnimationClip), allowSceneObjects = true };
                clipField.style.width = 250;
                clipField.SetEnabled(false);

                var nameField = new TextField();
                nameField.style.width = 120;
                nameField.textEdition.placeholder = "Enter Clip Name";
                var fpsLabel = new Label("FPS:");
                fpsLabel.style.width = 40;
                var fpsField = new IntegerField();
                fpsField.style.width = 60;

                container.Add(clipField);
                container.Add(nameField);
                container.Add(fpsLabel);
                container.Add(fpsField);
                return container;
            };

            clipListView.bindItem = (element, i) =>
            {
                var data = clipFPSList[i];
                var nameField = element.Q<TextField>();
                var clipField = element.Q<ObjectField>();
                var fpsField = element.Q<IntegerField>();

                nameField.value = data.keyword;
                clipField.value = data.clip;
                fpsField.value = data.samplingFPS;
                fpsField.MarkDirtyRepaint();

                nameField.RegisterValueChangedCallback(evt =>
                {
                    data.keyword = evt.newValue;
                    clipFPSList[i] = data;
                });

                fpsField.RegisterValueChangedCallback(evt =>
                {
                    data.samplingFPS = Mathf.Max(1, evt.newValue);
                    clipFPSList[i] = data;
                });
            };
            root.Add(clipListView);

            // SkinnedMeshRenderer 리스트 (읽기 전용)
            smrListView = new ListView
            {
                showFoldoutHeader = true,
                headerTitle = "Skinned Meshes (ReadOnly)",
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
            };
            smrListView.makeItem = () =>
            {
                var container = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                var smrField = new ObjectField { objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true };
                smrField.style.width = 300;
                smrField.SetEnabled(false);
                container.Add(smrField);
                return container;
            };
            smrListView.bindItem = (element, i) =>
            {
                var data = smrList[i];
                var smrField = element.Q<ObjectField>();
                smrField.value = data.smr;
            };
            root.Add(smrListView);

            // 저장 경로 입력 필드 + 버튼
            var pathRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            savePathField = new TextField("Save Path")
            {
                value = "Assets/VATTexture_MultiSMR.exr"
            };
            savePathField.style.flexGrow = 1;

            pathSelectButton = new Button(() =>
            {
                string path = EditorUtility.SaveFolderPanel("Save to...", "Assets", "VAT");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    savePathField.value = path;
                }
            })
            {
                text = "Browse..."
            };

            pathRow.Add(savePathField);
            pathRow.Add(pathSelectButton);
            root.Add(pathRow);

            // 권장 FPS 계산 버튼
            recommendButton = new Button(() => { RecommendFPS(); })
            {
                text = "권장 FPS 계산"
            };
            root.Add(recommendButton);

            bakeButton = new Button(() =>
            {
                if (animator == null)
                {
                    resultLabel.text = "Animator를 지정하세요.";
                    return;
                }

                Bake(savePathField.value);
            })
            {
                text = "Bake VAT Texture"
            };
            root.Add(bakeButton);

            resultLabel = new Label("Animator를 지정하면 클립과 SkinnedMesh 리스트가 표시됩니다.");
            root.Add(resultLabel);
        }

        void LoadClips(Animator anim)
        {
            clipFPSList.Clear();
            foreach (var clip in anim.runtimeAnimatorController.animationClips)
            {
                clipFPSList.Add(new ClipFPSData
                {
                    clip = clip,
                    samplingFPS = 30
                });
            }

            clipListView.itemsSource = clipFPSList;
            clipListView.Rebuild();
        }

        void LoadSMRs(Animator anim)
        {
            smrList.Clear();
            foreach (var smr in anim.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smrList.Add(new SMRData { smr = smr });
            }

            smrListView.itemsSource = smrList;
            smrListView.Rebuild();
        }

        private void RecommendFPS()
        {
            int maxVertexCount =
                smrList
                    .AsValueEnumerable()
                    .Where(data => data.smr)
                    .Aggregate(0, (current, data) => Mathf.Max(current, data.smr.sharedMesh.vertexCount));

            int texWidth = NextPowerOfTwo(maxVertexCount);
            if (texWidth > maxTexWidth)
            {
                EditorUtility.DisplayDialog("에러", "가로 크기 초과 → FPS 조정으로 해결 불가", "확인");
                resultLabel.text = "🚫 가로 제한 초과";
                return;
            }

            int totalFrames =
                clipFPSList
                    .AsValueEnumerable()
                    .Where(data => data.clip)
                    .Sum(data => Mathf.CeilToInt(data.clip.length * data.samplingFPS));

            int texHeight = NextPowerOfTwo(totalFrames);
            if (texHeight > maxTexHeight)
            {
                float scaleFactor = (float)maxTexHeight / texHeight;
                for (int i = 0; i < clipFPSList.Count; i++)
                {
                    clipFPSList[i].samplingFPS = Mathf.Max(1, Mathf.FloorToInt(clipFPSList[i].samplingFPS * scaleFactor));
                }

                clipListView.Rebuild();
                resultLabel.text = $"⚠️ 권장 FPS로 자동 조정 완료 (ScaleFactor: {scaleFactor:F2})";
            }
            else
            {
                resultLabel.text = "✅ 현재 FPS 설정으로 가능";
            }
        }

        private void Bake(string savePath)
        {
            if (clipFPSList.Count == 0 || smrList.Count == 0)
            {
                resultLabel.text = "클립과 SkinnedMeshRenderer가 필요합니다.";
                return;
            }

            int totalFrameCount = CalculateClipFrames();

            Mesh bakedMesh = new();

            foreach (SMRData smrData in smrList)
            {
                smrData.min = Vector3.positiveInfinity;
                smrData.max = Vector3.negativeInfinity;

                foreach (ClipFPSData clipData in clipFPSList)
                {
                    CalculateGlobalBounds(bakedMesh, smrData.smr, clipData.clip, clipData.endFrame - clipData.startFrame, out Vector3 min, out Vector3 max);

                    smrData.min = Vector3.Min(min, smrData.min);
                    smrData.max = Vector3.Max(max, smrData.max);
                }

                smrData.vatTexturePath = BakeTexture(savePath, bakedMesh, smrData.smr, totalFrameCount, smrData.min, smrData.max);
            }

            AssetDatabase.Refresh();

            foreach (SMRData smrData in smrList)
            {
                smrData.vatMaterialPath = CreateMaterial(savePath, smrData.vatTexturePath, smrData.smr, smrData.min, smrData.max);
            }

            AssetDatabase.Refresh();

            CreateVatDataScriptableObject(savePath);

            AssetDatabase.Refresh();

            /*
        #region debug

            foreach (SMRData smrData in smrList)
            {
                var loadedVatTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(smrData.vatTexturePath);
                var smr = smrData.smr;
                var min = smrData.min;
                var max = smrData.max;

                foreach (ClipFPSData clipData in clipFPSList)
                {
                    int frameCount = clipData.endFrame - clipData.startFrame;

                    for (int currentFrame = 0; currentFrame <= frameCount; currentFrame++)
                    {
                        int y = clipData.startFrame + currentFrame;

                        Color[] colors = loadedVatTexture.GetPixels(0, y, smr.sharedMesh.vertexCount, 1, 0);

                        Vector3[] restored =
                            colors
                                .AsValueEnumerable()
                                .Select(color =>
                                    new Vector3(
                                        Mathf.Lerp(min.x, max.x, color.r),
                                        Mathf.Lerp(min.y, max.y, color.g),
                                        Mathf.Lerp(min.z, max.z, color.b)))
                                .ToArray();

                        Mesh debugMesh = new Mesh();
                        smr.BakeMesh(debugMesh);
                        debugMesh.vertices = restored;

                        GameObject go = new($"{smr.name}_{clipData.keyword}_{currentFrame}", typeof(MeshFilter), typeof(MeshRenderer));

                        go.GetComponent<MeshFilter>().mesh = debugMesh;
                        go.GetComponent<MeshRenderer>().material = smr.sharedMaterial;
                    }
                }
            }

        #endregion
            */

            resultLabel.text = $"🎉 VAT 텍스처 최적화 저장 완료 → {savePath}";
            EditorUtility.DisplayDialog("완료", $"VAT 텍스처 최적화 저장 완료:\n{savePath}", "확인");
        }

        private int CalculateClipFrames()
        {
            int frameOffset = 0;

            foreach (ClipFPSData clipData in clipFPSList)
            {
                int frameCount = Mathf.CeilToInt(clipData.clip.length * clipData.samplingFPS);

                clipData.startFrame = frameOffset;

                frameOffset += frameCount;

                clipData.endFrame = frameOffset - 1;
            }

            return frameOffset;
        }

        private void CalculateGlobalBounds(Mesh tempMesh, SkinnedMeshRenderer smr, AnimationClip clip, int frameCount, out Vector3 min, out Vector3 max)
        {
            tempMesh ??= new Mesh();
            tempMesh.Clear();

            min = Vector3.positiveInfinity;
            max = Vector3.negativeInfinity;

            float frameTime = clip.length / frameCount;

            for (int currentFrame = 0; currentFrame <= frameCount; currentFrame++)
            {
                float time = currentFrame * frameTime;

                // 특정 시간의 메쉬 상태를 굽기 (애니메이터/타임라인 샘플링 필요)
                clip.SampleAnimation(animator.gameObject, time);
                smr.BakeMesh(tempMesh);

                Vector3[] vertices = tempMesh.vertices;

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vertex = tempMesh.vertices[i];

                    min = Vector3.Min(min, vertex);
                    max = Vector3.Max(max, vertex);
                }
            }
        }

        private string BakeTexture(string saveDirectory, Mesh tempMesh, SkinnedMeshRenderer smr, int totalFrameCount, Vector3 min, Vector3 max)
        {
            vatTexWidth = NextPowerOfTwo(smr.sharedMesh.vertexCount);
            vatTexHeight = NextPowerOfTwo(totalFrameCount);

            Texture2D vatTexture = new(vatTexWidth, vatTexHeight, TextureFormat.RGBAFloat, false);

            foreach (ClipFPSData clipData in clipFPSList)
            {
                int frameCount = clipData.endFrame - clipData.startFrame;
                float frameTime = clipData.clip.length / frameCount;

                for (int currentFrame = 0; currentFrame <= frameCount; currentFrame++)
                {
                    clipData.clip.SampleAnimation(animator.gameObject, currentFrame * frameTime);

                    tempMesh.Clear();
                    smr.BakeMesh(tempMesh);

                    int y = clipData.startFrame + currentFrame;

                    Color[] colors =
                        tempMesh.vertices
                            .AsValueEnumerable()
                            .Select(vertex =>
                            {
                                Vector3 normalizePosition = NormalizePosition(vertex, min, max);

                                return new Color(normalizePosition.x, normalizePosition.y, normalizePosition.z, 1.0f);
                            })
                            .ToArray();

                    vatTexture.SetPixels(0, y, colors.Length, 1, colors, 0);
                }
            }

            vatTexture.Apply();

            string vatTextureAssetName = $"{smr.sharedMesh.name}_vat.exr";
            string vatTexturePath = Path.Combine(saveDirectory, vatTextureAssetName);

            // 최적화된 EXR 저장
            byte[] bytes = vatTexture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP | Texture2D.EXRFlags.OutputAsFloat);
            File.WriteAllBytes(vatTexturePath, bytes);
            AssetDatabase.ImportAsset(vatTexturePath);

            // Importer 최적화
            if (AssetImporter.GetAtPath(vatTexturePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;

                TextureImporterPlatformSettings standAlonePlatformSettings = new()
                {
                    overridden = true,
                    name = UnityEditor.Build.NamedBuildTarget.Standalone.TargetName,
                    format = TextureImporterFormat.RGBAFloat, // 정밀도 유지
                    maxTextureSize = 8192,
                    textureCompression = TextureImporterCompression.Uncompressed
                };

                importer.SetPlatformTextureSettings(standAlonePlatformSettings);

                TextureImporterPlatformSettings webPlatformSettings = new()
                {
                    overridden = true,
                    name = UnityEditor.Build.NamedBuildTarget.WebGL.TargetName,
                    format = TextureImporterFormat.RGBAHalf, // 정밀도 유지
                    maxTextureSize = 8192,
                    textureCompression = TextureImporterCompression.Uncompressed
                };

                importer.SetPlatformTextureSettings(webPlatformSettings);

                importer.isReadable = true;
                importer.SaveAndReimport();
            }

            return vatTexturePath;
        }

        private string CreateMaterial(string saveDirectory, string vatTexturePath, SkinnedMeshRenderer smr, Vector3 min, Vector3 max)
        {
            Material vatMaterial = new(vatShaderGraph);

            vatMaterial.SetTexture(s_baseTex, smr.sharedMaterial.mainTexture);
            vatMaterial.SetTexture(s_vatTex, AssetDatabase.LoadAssetAtPath<Texture2D>(vatTexturePath));
            vatMaterial.SetVector(s_min, min);
            vatMaterial.SetVector(s_max, max);
            vatMaterial.enableInstancing = true;

            string vatMaterialAssetName = $"{smr.sharedMesh.name}_material.mat";
            string vatMaterialPath = Path.Combine(saveDirectory, vatMaterialAssetName);

            AssetDatabase.CreateAsset(vatMaterial, vatMaterialPath);

            return vatMaterialPath;
        }

        private void CreateVatDataScriptableObject(string saveDirectory)
        {
            VatData vatData = CreateInstance<VatData>();

            vatData.meshData =
                smrList
                    .AsValueEnumerable()
                    .Select(smrData => new VatMeshData
                    {
                        mesh = smrData.smr.sharedMesh,
                        material = AssetDatabase.LoadAssetAtPath<Material>(smrData.vatMaterialPath),
                    })
                    .ToList();

            vatData.clipData =
                clipFPSList
                    .AsValueEnumerable()
                    .Select(clipData => new VatClipData
                    {
                        keyword = clipData.keyword,
                        startFrame = clipData.startFrame,
                        endFrame = clipData.endFrame,
                        fps = clipData.samplingFPS
                    })
                    .ToList();

            string vatScriptableObjectAssetName = $"{animator.name}.asset";
            string vatScriptableObjectAssetPath = Path.Combine(saveDirectory, vatScriptableObjectAssetName);

            AssetDatabase.CreateAsset(vatData, vatScriptableObjectAssetPath);
        }

        private static int NextPowerOfTwo(int x)
        {
            int p = 1;
            while (p < x)
            {
                p <<= 1;
            }

            return p;
        }

        private static Vector3 NormalizePosition(Vector3 vertex, Vector3 min, Vector3 max)
        {
            float x = (vertex.x - min.x) / (max.x - min.x);
            float y = (vertex.y - min.y) / (max.y - min.y);
            float z = (vertex.z - min.z) / (max.z - min.z);

            return new Vector3(x, y, z);
        }
    }
}