using System;
using System.IO;
using BakeryShop.Core;
using BakeryShop.Player;
using BakeryShop.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BakeryShop.EditorTools
{
    /// <summary>씬 빌더가 UI를 만들고 받는 참조들.</summary>
    internal readonly struct ShopUi
    {
        public readonly GameObject Root;
        public readonly PanelHost Panels;

        public ShopUi(GameObject root, PanelHost panels)
        {
            Root = root;
            Panels = panels;
        }
    }

    /// <summary>
    /// 매장 씬의 UI(HUD, 안내, 이벤트 로그)를 코드로 만든다.
    /// 캔버스를 손으로 배치하지 않는 이유는 씬과 같다: 컴포넌트 연결을 빠뜨리지 않고, 다시 만들어도 같은 결과가 나온다.
    /// 기준 해상도는 1920×1080이고 CanvasScaler가 다른 해상도에 맞춘다.
    /// </summary>
    internal static class ShopUiBuilder
    {
        public const string FontFolder = "Assets/_Project/Art/Fonts";
        public const string UiFontPath = FontFolder + "/UiFont.asset";

        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        private static readonly Color TextColor = Color.white;
        private static readonly Color BackingColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BarBackColor = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color BarFillColor = new Color(0.45f, 0.75f, 1f);

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        /// <summary>
        /// UI를 만들 준비가 되었는지 본다. 준비되지 않았으면 무엇을 해야 하는지 돌려주고, 되었으면 null.
        /// </summary>
        public static string CheckPrerequisites()
        {
            if (!File.Exists(TmpSettingsPath))
            {
                return "TextMeshPro의 기본 리소스가 없습니다.\n"
                    + "메뉴 Window > TextMeshPro > Import TMP Essential Resources를 실행한 뒤 다시 시도하세요.";
            }

            return null;
        }

        /// <summary>HUD 전체: 날짜·시계, 돈·평판·레벨, 조준점과 안내, 이벤트 로그.</summary>
        public static ShopUi Build(Interactor interactor)
        {
            UiFont uiFont = EnsureUiFont();

            var root = new GameObject("[UI]");
            var panels = root.AddComponent<PanelHost>();

            RectTransform hud = CreateCanvas("HUD", root.transform, 0);
            BuildPrompt(hud, interactor, uiFont);
            BuildStatus(hud, uiFont);

            RectTransform debug = CreateCanvas("DebugOverlay", root.transform, 100);
            BuildEventLog(debug, uiFont);

            return new ShopUi(root, panels);
        }

        /// <summary>조준점과 "[E] 행동" 안내만 있는 화면 (1주차 그레이박스 씬용).</summary>
        public static void BuildPromptOnly(Interactor interactor)
        {
            UiFont uiFont = EnsureUiFont();
            RectTransform hud = CreateCanvas("HUD", null, 0);
            BuildPrompt(hud, interactor, uiFont);
        }

        /// <summary>
        /// UI 글꼴 에셋(UiFont.asset)을 찾거나 만든다. 글꼴은 Art/Fonts 폴더에서 찾는다:
        /// TMP 글꼴 에셋이 있으면 그것을 쓰고, 없고 글꼴 파일(.otf/.ttf)만 있으면 그 파일로 TMP 글꼴 에셋을 만든다.
        /// </summary>
        public static UiFont EnsureUiFont()
        {
            SceneBuildUtility.EnsureFolder(FontFolder);

            var uiFont = AssetDatabase.LoadAssetAtPath<UiFont>(UiFontPath);
            if (uiFont == null)
            {
                uiFont = ScriptableObject.CreateInstance<UiFont>();
                AssetDatabase.CreateAsset(uiFont, UiFontPath);
            }

            if (uiFont.Font != null)
            {
                return uiFont;
            }

            TMP_FontAsset font = FindPreferred<TMP_FontAsset>("t:TMP_FontAsset");
            if (font == null)
            {
                font = CreateFontAssetFromFontFile();
            }

            if (font != null)
            {
                SceneBuildUtility.SetReference(uiFont, "_font", font);
                EditorUtility.SetDirty(uiFont);
                Debug.Log("[UI 빌더] UI 글꼴: " + AssetDatabase.GetAssetPath(font), uiFont);
            }
            else
            {
                Debug.LogWarning(
                    "[UI 빌더] " + FontFolder + "에 글꼴이 없습니다. TMP의 기본 글꼴을 쓰므로 한글이 □로 보입니다.\n"
                    + "Pretendard-Regular.otf를 이 폴더에 넣고 메뉴 BakeryShop > 매장 씬 만들기를 다시 실행하세요.",
                    uiFont);
            }

            return uiFont;
        }

        // 이름에 Regular가 들어간 것을 먼저 고른다 (Bold만 본문에 쓰이지 않게).
        private static T FindPreferred<T>(string filter) where T : UnityEngine.Object
        {
            T fallback = null;
            foreach (string guid in AssetDatabase.FindAssets(filter, new[] { FontFolder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                {
                    continue;
                }

                if (asset.name.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return asset;
                }

                if (fallback == null)
                {
                    fallback = asset;
                }
            }

            return fallback;
        }

        // 글꼴 파일에서 동적(Dynamic) TMP 글꼴 에셋을 만든다. 동적 에셋은 글자가 처음 쓰일 때 아틀라스에 그려 넣으므로
        // 한글 11,172자를 미리 굽지 않아도 된다.
        private static TMP_FontAsset CreateFontAssetFromFontFile()
        {
            Font source = FindPreferred<Font>("t:Font");
            if (source == null)
            {
                return null;
            }

            try
            {
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source);
                if (asset == null)
                {
                    return null;
                }

                asset.name = source.name + " SDF";
                AssetDatabase.CreateAsset(asset, FontFolder + "/" + asset.name + ".asset");

                // 재질과 아틀라스 텍스처를 글꼴 에셋 안에 함께 저장한다. 따로 두지 않으면 씬을 다시 열 때 참조가 끊긴다.
                if (asset.material != null)
                {
                    asset.material.name = asset.name + " Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                }

                if (asset.atlasTexture != null)
                {
                    asset.atlasTexture.name = asset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
                }

                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                return asset;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[UI 빌더] 글꼴 파일에서 TMP 글꼴 에셋을 만들지 못했습니다: " + exception.Message + "\n"
                    + "글꼴 파일을 우클릭 > Create > TextMeshPro > Font Asset으로 직접 만든 뒤 다시 실행하세요.",
                    source);
                return null;
            }
        }

        private static void BuildPrompt(RectTransform hud, Interactor interactor, UiFont uiFont)
        {
            Image crosshair = CreateImage("Crosshair", hud, Center, Center, Vector2.zero, new Vector2(6f, 6f), Color.white);
            crosshair.raycastTarget = false;

            TMP_Text prompt = CreateText("InteractionPrompt", hud, uiFont, Center, Center, new Vector2(0f, -80f), new Vector2(900f, 60f), 32f, TextAlignmentOptions.Center, "");

            var promptHud = hud.gameObject.AddComponent<InteractionPromptHUD>();
            SceneBuildUtility.SetReference(promptHud, "_interactor", interactor);
            SceneBuildUtility.SetReference(promptHud, "_label", prompt);
        }

        private static void BuildStatus(RectTransform hud, UiFont uiFont)
        {
            // 왼쪽 위: 날짜와 단계, 시계
            CreateImage("Backing_TopLeft", hud, TopLeft, TopLeft, new Vector2(20f, -20f), new Vector2(430f, 130f), BackingColor);
            TMP_Text dayLabel = CreateText("DayLabel", hud, uiFont, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(400f, 46f), 34f, TextAlignmentOptions.TopLeft, "1일차 · 준비 중");
            TMP_Text clockLabel = CreateText("ClockLabel", hud, uiFont, TopLeft, TopLeft, new Vector2(40f, -80f), new Vector2(400f, 60f), 48f, TextAlignmentOptions.TopLeft, "09:00");

            // 오른쪽 위: 돈, 변화량, 평판, 레벨
            CreateImage("Backing_TopRight", hud, TopRight, TopRight, new Vector2(-20f, -20f), new Vector2(430f, 200f), BackingColor);
            TMP_Text moneyLabel = CreateText("MoneyLabel", hud, uiFont, TopRight, TopRight, new Vector2(-40f, -30f), new Vector2(390f, 50f), 40f, TextAlignmentOptions.TopRight, "0원");
            TMP_Text moneyDeltaLabel = CreateText("MoneyDeltaLabel", hud, uiFont, TopRight, TopRight, new Vector2(-40f, -80f), new Vector2(390f, 36f), 26f, TextAlignmentOptions.TopRight, "+0");

            // 평판: 별 하나를 반 칸 두 개로 나눈 막대 10개. 글꼴에 반쪽 별 글자가 없어도 반 개 단위를 그릴 수 있다.
            CreateText("ReputationCaption", hud, uiFont, TopRight, TopRight, new Vector2(-262f, -122f), new Vector2(150f, 34f), 24f, TextAlignmentOptions.TopRight, "평판");
            const float pipStart = -252f;
            const float halfWidth = 17f;
            const float starPitch = 44f;
            var pips = new Image[HudFormat.HalfPipCount];
            for (int i = 0; i < pips.Length; i++)
            {
                float x = pipStart + (i / 2) * starPitch + (i % 2) * (halfWidth + 1f);
                pips[i] = CreateImage("ReputationPip_" + i, hud, TopRight, TopLeft, new Vector2(x, -126f), new Vector2(halfWidth, 24f), Color.white);
            }

            // 레벨: "Lv.1" [막대] "0/40"
            TMP_Text levelLabel = CreateText("LevelLabel", hud, uiFont, TopRight, TopRight, new Vector2(-322f, -166f), new Vector2(90f, 34f), 24f, TextAlignmentOptions.TopRight, "Lv.1");
            Image barBack = CreateImage("XpBar", hud, TopRight, TopLeft, new Vector2(-312f, -172f), new Vector2(150f, 20f), BarBackColor);
            Image barFill = CreateImage("Fill", barBack.rectTransform, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, BarFillColor);
            RectTransform fillRect = barFill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            TMP_Text xpLabel = CreateText("XpLabel", hud, uiFont, TopRight, TopRight, new Vector2(-40f, -166f), new Vector2(115f, 34f), 22f, TextAlignmentOptions.TopRight, "0/0");

            var view = hud.gameObject.AddComponent<HudView>();
            SceneBuildUtility.SetReference(view, "_dayLabel", dayLabel);
            SceneBuildUtility.SetReference(view, "_clockLabel", clockLabel);
            SceneBuildUtility.SetReference(view, "_moneyLabel", moneyLabel);
            SceneBuildUtility.SetReference(view, "_moneyDeltaLabel", moneyDeltaLabel);
            SceneBuildUtility.SetReference(view, "_levelLabel", levelLabel);
            SceneBuildUtility.SetReference(view, "_xpFill", fillRect);
            SceneBuildUtility.SetReference(view, "_xpLabel", xpLabel);
            SceneBuildUtility.SetReferenceArray(view, "_reputationHalfPips", pips);
        }

        private static void BuildEventLog(RectTransform canvas, UiFont uiFont)
        {
            Image panel = CreateImage("EventLogPanel", canvas, BottomLeft, BottomLeft, new Vector2(20f, 20f), new Vector2(1000f, 560f), new Color(0f, 0f, 0f, 0.7f));
            TMP_Text text = CreateText("EventLogText", panel.rectTransform, uiFont, BottomLeft, BottomLeft, new Vector2(14f, 10f), new Vector2(972f, 540f), 18f, TextAlignmentOptions.TopLeft, "");
            text.richText = false;

            var overlay = canvas.gameObject.AddComponent<EventLogOverlay>();
            SceneBuildUtility.SetReference(overlay, "_panel", panel.gameObject);
            SceneBuildUtility.SetReference(overlay, "_text", text);
            panel.gameObject.SetActive(false);
        }

        private static RectTransform CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, false);
            }

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return (RectTransform)canvasObject.transform;
        }

        /// <param name="anchor">부모의 어느 점에 붙는지 (0,1 = 왼쪽 위).</param>
        /// <param name="pivot">자신의 어느 점이 그 자리에 오는지.</param>
        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rectObject = new GameObject(name, typeof(RectTransform));
            rectObject.transform.SetParent(parent, false);

            var rect = (RectTransform)rectObject.transform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image CreateImage(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            RectTransform rect = CreateRect(name, parent, anchor, pivot, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            UiFont uiFont,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment,
            string text)
        {
            RectTransform rect = CreateRect(name, parent, anchor, pivot, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (uiFont != null && uiFont.Font != null)
            {
                label.font = uiFont.Font;
            }

            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = TextColor;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }
    }
}
