using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;
using Object = UnityEngine.Object;


namespace War.VAT.Editor
{
    public class VertexAnimationTextureBaker : EditorWindow
    {
#pragma warning disable UDR0001
        private static int s_baseTex;
        private static int s_vatTex;
        private static int s_normalTex;
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
        private class AnimationClipData
        {
            public AnimationClip clip;

            public string keyword;
            public int startFrame;
            public int endFrame;
            public int samplingFPS;
        }

        private class SkinnedMeshRendererData
        {
            public SkinnedMeshRenderer Renderer;
            public Vector3 Min;
            public Vector3 Max;
            public string VatTexturePath;
            public string NormalTexturePath;
            public string VatMaterialPath;
        }


        private Shader vatShaderGraph;
        private Animator animator;
        private readonly List<AnimationClipData> animationClipDataList = new();
        private readonly List<SkinnedMeshRendererData> skinnedMeshRendererDataList = new();

        private int maxTexWidth;
        private int maxTexHeight;

        private int vatTexWidth;
        private int vatTexHeight;


        [MenuItem("Tools/Vertex Animation Texture Baker")]
        public static void ShowWindow()
        {
            VertexAnimationTextureBaker wnd = GetWindow<VertexAnimationTextureBaker>();
            wnd.titleContent = new GUIContent("Vertex Animation Texture Baker");
            wnd.minSize = new Vector2(600, 500);
        }

        private void OnValidate()
        {
            s_baseTex = Shader.PropertyToID("_BaseTex");
            s_vatTex = Shader.PropertyToID("_VATTex");
            s_normalTex = Shader.PropertyToID("_NormalTex");
            s_min = Shader.PropertyToID("_Min");
            s_max = Shader.PropertyToID("_Max");

            vatShaderGraph = Shader.Find("Shader Graphs/VAT");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;

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
                    LoadAnimationClips(animator);
                    LoadSkinnedMeshRenderers(animator);
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

            clipListView = new ListView
            {
                showFoldoutHeader = true,
                headerTitle = "Animation Clips",
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeItem = () =>
                {
                    VisualElement container = new() { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                    ObjectField clipField = new()
                    {
                        objectType = typeof(AnimationClip), allowSceneObjects = true,
                        style =
                        {
                            width = 250
                        }
                    };
                    clipField.SetEnabled(false);

                    TextField nameField = new()
                    {
                        style =
                        {
                            width = 120
                        },
                        textEdition =
                        {
                            placeholder = "Enter Clip Name"
                        }
                    };
                    Label fpsLabel = new("FPS:")
                    {
                        style =
                        {
                            width = 40
                        }
                    };
                    IntegerField fpsField = new()
                    {
                        style =
                        {
                            width = 60
                        }
                    };

                    container.Add(clipField);
                    container.Add(nameField);
                    container.Add(fpsLabel);
                    container.Add(fpsField);

                    return container;
                },
                bindItem = (element, i) =>
                {
                    AnimationClipData data = animationClipDataList[i];
                    TextField nameField = element.Q<TextField>();
                    ObjectField clipField = element.Q<ObjectField>();
                    IntegerField fpsField = element.Q<IntegerField>();

                    nameField.value = data.keyword;
                    clipField.value = data.clip;
                    fpsField.value = data.samplingFPS;
                    fpsField.MarkDirtyRepaint();

                    nameField.RegisterValueChangedCallback(evt =>
                    {
                        data.keyword = evt.newValue;
                        animationClipDataList[i] = data;
                    });

                    fpsField.RegisterValueChangedCallback(evt =>
                    {
                        data.samplingFPS = Mathf.Max(1, evt.newValue);
                        animationClipDataList[i] = data;
                    });
                }
            };

            root.Add(clipListView);

            smrListView = new ListView
            {
                showFoldoutHeader = true,
                headerTitle = "Skinned Meshes (ReadOnly)",
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                makeItem = () =>
                {
                    VisualElement container = new() { style = { flexDirection = FlexDirection.Row, marginBottom = 4 } };
                    ObjectField smrField = new()
                    {
                        objectType = typeof(SkinnedMeshRenderer), allowSceneObjects = true,
                        style =
                        {
                            width = 300
                        }
                    };
                    smrField.SetEnabled(false);
                    container.Add(smrField);

                    return container;
                },
                bindItem = (element, i) =>
                {
                    SkinnedMeshRendererData data = skinnedMeshRendererDataList[i];
                    ObjectField smrField = element.Q<ObjectField>();
                    smrField.value = data.Renderer;
                }
            };
            root.Add(smrListView);

            VisualElement pathRow = new()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 6
                }
            };
            savePathField = new TextField("Save Path")
            {
                value = "Assets/VATTexture_MultiSMR.exr",
                style =
                {
                    flexGrow = 1
                }
            };

            pathSelectButton = new Button(() =>
            {
                string path = EditorUtility.SaveFolderPanel("Save to...", "Assets", "VAT");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        path = "Assets" + path.Substring(Application.dataPath.Length);
                    }

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
            recommendButton = new Button(SetRecommendFPS)
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

        private void LoadAnimationClips(Animator anim)
        {
            animationClipDataList.Clear();
            foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
            {
                animationClipDataList.Add(new AnimationClipData
                {
                    keyword = clip.name,
                    clip = clip,
                    samplingFPS = 30
                });
            }

            clipListView.itemsSource = animationClipDataList;
            clipListView.Rebuild();
        }

        private void LoadSkinnedMeshRenderers(Animator anim)
        {
            skinnedMeshRendererDataList.Clear();
            foreach (SkinnedMeshRenderer meshRenderer in anim.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skinnedMeshRendererDataList.Add(new SkinnedMeshRendererData { Renderer = meshRenderer });
            }

            smrListView.itemsSource = skinnedMeshRendererDataList;
            smrListView.Rebuild();
        }

        private void SetRecommendFPS()
        {
            int maxVertexCount =
                skinnedMeshRendererDataList
                    .AsValueEnumerable()
                    .Where(data => data.Renderer)
                    .Aggregate(0, (current, data) => Mathf.Max(current, data.Renderer.sharedMesh.vertexCount));

            int texWidth = NextPowerOfTwo(maxVertexCount);
            if (texWidth > maxTexWidth)
            {
                EditorUtility.DisplayDialog("에러", "가로 크기 초과 → FPS 조정으로 해결 불가", "확인");
                resultLabel.text = "🚫 가로 제한 초과";
                return;
            }

            int totalFrames =
                animationClipDataList
                    .AsValueEnumerable()
                    .Where(data => data.clip)
                    .Sum(data => Mathf.CeilToInt(data.clip.length * data.samplingFPS));

            int texHeight = NextPowerOfTwo(totalFrames);
            if (texHeight > maxTexHeight)
            {
                float scaleFactor = (float)maxTexHeight / texHeight;
                for (int i = 0; i < animationClipDataList.Count; i++)
                {
                    animationClipDataList[i].samplingFPS = Mathf.Max(1, Mathf.FloorToInt(animationClipDataList[i].samplingFPS * scaleFactor));
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
            if (animationClipDataList.Count == 0 || skinnedMeshRendererDataList.Count == 0)
            {
                resultLabel.text = "클립과 SkinnedMeshRenderer가 필요합니다.";
                return;
            }

            animator.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            animator.transform.localScale = Vector3.one;

            int totalFrameCount = CalculateClipFrames();

            Mesh bakedMesh = new();

            foreach (SkinnedMeshRendererData smrData in skinnedMeshRendererDataList)
            {
                smrData.Renderer.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                smrData.Renderer.transform.localScale = Vector3.one;

                smrData.Min = Vector3.positiveInfinity;
                smrData.Max = Vector3.negativeInfinity;

                foreach (AnimationClipData clipData in animationClipDataList)
                {
                    CalculateGlobalBounds(bakedMesh, smrData.Renderer, clipData.clip, clipData.endFrame - clipData.startFrame, out Vector3 min, out Vector3 max);

                    smrData.Min = Vector3.Min(min, smrData.Min);
                    smrData.Max = Vector3.Max(max, smrData.Max);
                }

                BakeTexture(savePath, bakedMesh, smrData.Renderer, totalFrameCount, smrData.Min, smrData.Max, out smrData.VatTexturePath, out smrData.NormalTexturePath);
            }

            AssetDatabase.Refresh();

            foreach (SkinnedMeshRendererData smrData in skinnedMeshRendererDataList)
            {
                smrData.VatMaterialPath = CreateMaterial(savePath, smrData.VatTexturePath, smrData.NormalTexturePath, smrData.Renderer, smrData.Min, smrData.Max);
            }

            AssetDatabase.Refresh();

            CreateVatDataScriptableObject(savePath);

            AssetDatabase.Refresh();

            resultLabel.text = $"🎉 VAT 텍스처 최적화 저장 완료 → {savePath}";
            EditorUtility.DisplayDialog("완료", $"VAT 텍스처 최적화 저장 완료:\n{savePath}", "확인");
        }

        private int CalculateClipFrames()
        {
            int frameOffset = 0;

            foreach (AnimationClipData clipData in animationClipDataList)
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
                clip.SampleAnimation(animator.gameObject, currentFrame * frameTime);
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

        private void BakeTexture(string saveDirectory, Mesh tempMesh, SkinnedMeshRenderer smr, int totalFrameCount, Vector3 min, Vector3 max, out string vatTexturePath, out string normalTexturePath)
        {
            vatTexWidth = NextPowerOfTwo(smr.sharedMesh.vertexCount);
            vatTexHeight = NextPowerOfTwo(totalFrameCount);

            Texture2D vatTexture = new(vatTexWidth, vatTexHeight, TextureFormat.RGBAFloat, false);
            Texture2D normalTexture = new(vatTexWidth, vatTexHeight, TextureFormat.RGBAFloat, false);

            foreach (AnimationClipData clipData in animationClipDataList)
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

                    Color[] normalColors =
                        tempMesh.normals
                            .AsValueEnumerable()
                            .Select(normal => new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f))
                            .ToArray();

                    normalTexture.SetPixels(0, y, normalColors.Length, 1, normalColors, 0);
                }
            }

            vatTexture.Apply();
            normalTexture.Apply();

            string vatTextureAssetName = $"{smr.sharedMesh.name}_vat.exr";
            vatTexturePath = Path.Combine(saveDirectory, vatTextureAssetName);

            ImportOptimized(vatTexture, vatTexturePath);

            string normalTextureAssetName = $"{smr.sharedMesh.name}_normal.exr";
            normalTexturePath = Path.Combine(saveDirectory, normalTextureAssetName);

            ImportOptimized(normalTexture, normalTexturePath);

            return;

            static void ImportOptimized(Texture2D texture, string texturePath)
            {
                byte[] bytes = texture.EncodeToEXR(Texture2D.EXRFlags.CompressZIP | Texture2D.EXRFlags.OutputAsFloat);
                File.WriteAllBytes(texturePath, bytes);
                AssetDatabase.ImportAsset(texturePath);

                if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
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
                        format = TextureImporterFormat.RGBAFloat,
                        maxTextureSize = 8192,
                        textureCompression = TextureImporterCompression.Uncompressed
                    };

                    importer.SetPlatformTextureSettings(standAlonePlatformSettings);

                    TextureImporterPlatformSettings webPlatformSettings = new()
                    {
                        overridden = true,
                        name = UnityEditor.Build.NamedBuildTarget.WebGL.TargetName,
                        format = TextureImporterFormat.RGBAHalf,
                        maxTextureSize = 8192,
                        textureCompression = TextureImporterCompression.Uncompressed
                    };

                    importer.SetPlatformTextureSettings(webPlatformSettings);

                    importer.SaveAndReimport();
                }
            }
        }

        private string CreateMaterial(string saveDirectory, string vatTexturePath, string normalTexturePath, SkinnedMeshRenderer smr, Vector3 min, Vector3 max)
        {
            Material vatMaterial = new(vatShaderGraph);

            vatMaterial.SetTexture(s_baseTex, smr.sharedMaterial.mainTexture);
            vatMaterial.SetTexture(s_vatTex, AssetDatabase.LoadAssetAtPath<Texture2D>(vatTexturePath));
            vatMaterial.SetTexture(s_normalTex, AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexturePath));
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
                skinnedMeshRendererDataList
                    .AsValueEnumerable()
                    .Select(smrData => new VatMeshData
                    {
                        mesh = smrData.Renderer.sharedMesh,
                        material = AssetDatabase.LoadAssetAtPath<Material>(smrData.VatMaterialPath),
                    })
                    .ToList();

            vatData.clipData =
                animationClipDataList
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