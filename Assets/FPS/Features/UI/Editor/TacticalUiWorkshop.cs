using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FPS.UI.Editor
{
    /// <summary>Explicit, repeatable UI authoring. Existing controllers and references are retained.</summary>
    public static class TacticalUiWorkshop
    {
        const string UiPath = "Assets/FPS/Features/UI/Content/";
        static TMP_FontAsset Heading => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiPath + "Fonts/Teko-Bold SDF.asset");
        static TMP_FontAsset Body => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        static TMP_FontAsset Narrow => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiPath + "Fonts/Teko-Regular SDF.asset");
        static readonly Vector2 TL = new Vector2(0, 1);
        static readonly Vector2 TR = Vector2.one;
        static readonly Vector2 BL = Vector2.zero;
        static readonly Vector2 BR = new Vector2(1, 0);
        static readonly Vector2 Center = new Vector2(.5f, .5f);

        static T Ensure<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }
        static RectTransform Find(Transform root, string name) => root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
        static RectTransform Node(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }
        static void At(RectTransform r, Vector2 anchor, float x, float y, float w, float h)
        {
            if (r == null) return;
            r.anchorMin = r.anchorMax = anchor; r.pivot = anchor;
            r.anchoredPosition = new Vector2(x, y); r.sizeDelta = new Vector2(w, h);
            r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        }
        static void Fill(RectTransform r, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.pivot = Center;
            r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
            r.localScale = Vector3.one; r.localRotation = Quaternion.identity;
        }
        static Image Plate(RectTransform r, Color color, bool raycast = false)
        {
            var i = Ensure<Image>(r.gameObject); i.sprite = null; i.type = Image.Type.Simple;
            i.color = color; i.raycastTarget = raycast;
            if (r.name == "TopRule" || r.name == "AccentRule") i.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "Sprites/Survival/WornRule.png");
            if (SurvivalUiArt.IsSurface(r.name))
            {
                SurvivalUiArt.Assign(i, "Surfaces/Panel", Color.white, true);
                var wear = r.Find("SurfaceWear");
                if (wear != null) wear.gameObject.SetActive(false);
            }
            return i;
        }
        static TMP_Text Text(RectTransform r, string value, float size = 22, bool heading = false, Color? color = null)
        {
            var t = Ensure<TextMeshProUGUI>(r.gameObject);
            t.font = heading ? Heading : Body; t.fontSharedMaterial = t.font.material;
            t.text = value; t.fontSize = heading ? Mathf.Min(size, r.rect.height / (1.46f * Mathf.Max(1, value.Split('\n').Length))) : size; t.fontStyle = FontStyles.Normal;
            t.color = color ?? TacticalUiTheme.Text; t.raycastTarget = false;
            t.enableAutoSizing = false; t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Ellipsis; t.alignment = TextAlignmentOptions.MidlineLeft;
            t.margin = Vector4.zero; t.characterSpacing = 0; t.lineSpacing = 0;
            foreach (var effect in t.GetComponents<BaseMeshEffect>()) effect.enabled = false;
            return t;
        }
        static TMP_Text Label(Transform p, string n, string value, float x, float y, float w, float h, float size = 22, bool heading = false, Color? color = null)
        {
            var r = Node(p, n); At(r, TL, x, -y, w, h); return Text(r, value, size, heading, color);
        }
        static void Rule(Transform p, string n, float x, float y, float w, Color? color = null)
        {
            var r = Node(p, n); At(r, TL, x, -y, w, 2); Plate(r, color ?? TacticalUiTheme.Border);
        }
        static void Button(RectTransform r, string label = null, bool primary = false, float fontSize = 24)
        {
            if (r == null) return;
            var b = r.GetComponent<Button>(); if (b == null) return;
            var image = Plate(r, Color.white, true); b.targetGraphic = image;
            SurvivalUiArt.Assign(image, "Controls/Control", Color.white, true);
            b.transition = Selectable.Transition.ColorTint; b.colors = TacticalUiTheme.ButtonColors(primary);
            b.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            foreach (var effect in r.GetComponents<BaseMeshEffect>()) effect.enabled = false;
            var t = r.GetComponentInChildren<TMP_Text>(true);
            if (t == null) t = Ensure<TextMeshProUGUI>(Node(r, "Text").gameObject);
            Text(t.rectTransform, label ?? t.text, fontSize, false, TacticalUiTheme.Text);
            Fill(t.rectTransform, 20, 6, 20, 6); t.alignment = TextAlignmentOptions.Center;
        }
        static void MenuButton(RectTransform r, string value, bool primary = false)
        {
            Button(r, value, false, 28);
            var button = r.GetComponent<Button>(); button.transition = Selectable.Transition.None;
            var background = r.GetComponent<Image>(); background.color = Color.clear;
            SurvivalUiArt.Assign(background, "Controls/MenuFocus", new Color(.6f,.5f,.45f, primary ? .2f : 0), true);
            var label = r.GetComponentInChildren<TMP_Text>(true);
            Fill(label.rectTransform, 28, 6, 28, 6); label.alignment = TextAlignmentOptions.MidlineLeft;
            label.characterSpacing = 2; label.color = primary ? TacticalUiTheme.Accent : TacticalUiTheme.Text;
            var marker = Node(r, "FocusMarker"); At(marker, new Vector2(0,.5f), 0, 0, 3, 24);
            var markerImage = Plate(marker, primary ? TacticalUiTheme.Accent : Color.clear);
            markerImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "Sprites/Survival/WornRule.png");
            var view = Ensure<SurvivalMenuItem>(r.gameObject);
            Bind(view, "label", label); Bind(view, "marker", markerImage); Bind(view, "background", background);
            var so = new SerializedObject(view); so.FindProperty("primary").boolValue = primary; so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ButtonIcon(RectTransform button, string key)
        {
            var icon = Node(button, "ActionIcon"); At(icon, new Vector2(0, .5f), 16, 0, 22, 22);
            var image = Plate(icon, TacticalUiTheme.Text);
            SurvivalUiArt.Assign(image, "Icons/" + key, TacticalUiTheme.Text);
            image.preserveAspect = true;
            var label = button.GetComponentInChildren<TMP_Text>(true);
            Fill(label.rectTransform, 43, 6, 8, 6);
        }

        static Image HudPlate(RectTransform r, bool mirror = false)
        {
            var image = Plate(r, Color.white);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "Sprites/Survival/HudShade.png");
            r.localScale = new Vector3(mirror ? -1 : 1, 1, 1);
            return image;
        }

        static void StyleCommon(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                Text(t.rectTransform, t.text, 22);
            foreach (var b in root.GetComponentsInChildren<Button>(true)) Button((RectTransform)b.transform);
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                SurvivalUiArt.Assign(Plate((RectTransform)input.transform, TacticalUiTheme.Background, true), "Controls/Control", TacticalUiTheme.Control, true);
                input.customCaretColor = true; input.caretColor = TacticalUiTheme.Accent;
                input.selectionColor = new Color(1, .65f, .3f, .3f);
                if (input.textViewport != null) Fill(input.textViewport, 18, 8, 18, 8);
                if (input.textComponent != null) { Text(input.textComponent.rectTransform, input.textComponent.text, 24); Fill(input.textComponent.rectTransform); }
                if (input.placeholder is TMP_Text placeholder) { Text(placeholder.rectTransform, placeholder.text, 22, false, TacticalUiTheme.Muted); Fill(placeholder.rectTransform); }
            }
            foreach (var d in root.GetComponentsInChildren<TMP_Dropdown>(true)) StyleDropdown(d);
        }
        static void StyleDropdown(TMP_Dropdown d)
        {
            SurvivalUiArt.Assign(Plate((RectTransform)d.transform, Color.white, true), "Controls/Control", Color.white, true); d.colors = TacticalUiTheme.ButtonColors();
            if (d.captionText != null) { Text(d.captionText.rectTransform, d.captionText.text, 22); Fill(d.captionText.rectTransform, 18, 6, 44, 6); }
            if (d.template != null)
            {
                SurvivalUiArt.Assign(Plate(d.template, TacticalUiTheme.Surface, true), "Surfaces/Panel", Color.white, true);
                d.template.sizeDelta = new Vector2(0, 220);
                foreach (var t in d.template.GetComponentsInChildren<TMP_Text>(true)) Text(t.rectTransform, t.text, 22);
                foreach (var toggle in d.template.GetComponentsInChildren<Toggle>(true))
                {
                    toggle.colors = TacticalUiTheme.ButtonColors();
                    if (toggle.targetGraphic is Image image) SurvivalUiArt.Assign(image, "Controls/Control", Color.white, true);
                    if (toggle.graphic != null) toggle.graphic.color = TacticalUiTheme.Accent;
                    if (toggle.graphic is Image check) SurvivalUiArt.Assign(check, "Icons/Check", TacticalUiTheme.Accent);
                }
            }
            DropdownTemplateUtility.Normalize(d);
        }
        static void ConfigureCanvas(Canvas c)
        {
            foreach (string path in new[] { "SurfaceWear", "WornRule", "NavigationShade", "SolidFill", "HudShade" })
            {
                var importer = AssetImporter.GetAtPath(UiPath + "Sprites/Survival/" + path + ".png") as TextureImporter;
                if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single)) { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.SaveAndReimport(); }
            }
            var s = Ensure<CanvasScaler>(c.gameObject);
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = TacticalUiTheme.ReferenceResolution;
            // Expand preserves the full minimum design area at 4:3 and ultrawide.
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = TacticalUiTheme.HudOrder;
        }
        static void Modal(RectTransform root)
        {
            Fill(root); Plate(root, new Color(.035f, .045f, .035f, .96f), true);
            var c = Ensure<Canvas>(root.gameObject); c.overrideSorting = true; c.sortingOrder = TacticalUiTheme.MenuOrder;
            Ensure<GraphicRaycaster>(root.gameObject);
        }
        static void Bind(Object controller, string field, Object value)
        {
            var s = new SerializedObject(controller); var property = s.FindProperty(field);
            if (property == null) throw new InvalidOperationException(controller.name + "." + field);
            property.objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Backdrop(Canvas canvas)
        {
            var image = Find(canvas.transform, "Background")?.GetComponent<Image>();
            if (image != null) { image.color = new Color(.66f, .67f, .65f, 1); image.raycastTarget = false; }
            var dim = Find(canvas.transform, "SceneDimming");
            if (dim != null) Plate(dim, new Color(.02f, .025f, .025f, .32f));
            var lines = Find(canvas.transform, "TacticalLines"); if (lines != null) lines.gameObject.SetActive(false);
        }

        public static void MainMenu(Canvas c)
        {
            ConfigureCanvas(c); StyleCommon(c.transform); Backdrop(c);
            foreach (var ruleImage in c.GetComponentsInChildren<Image>(true))
                if (ruleImage.name.Contains("Rule")) ruleImage.color = TacticalUiTheme.Accent;
            var main = Find(c.transform, "MainPanel"); Fill(main);
            Plate(main, Color.clear); Ensure<CanvasGroup>(main.gameObject); Ensure<UiSafeArea>(main.gameObject);
            var shade = Node(main, "NavigationShade"); shade.SetAsFirstSibling();
            shade.anchorMin = Vector2.zero; shade.anchorMax = new Vector2(.64f, 1); shade.offsetMin = shade.offsetMax = Vector2.zero;
            var shadeImage = Plate(shade, Color.white); shadeImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "Sprites/Survival/NavigationShade.png");
            var grain = Node(main, "MenuDistress"); Fill(grain); grain.SetSiblingIndex(1);
            SurvivalUiArt.Assign(Plate(grain, Color.white), "Surfaces/Distress", new Color(.35f,.32f,.29f,.055f));
            var oldRule = Find(main, "TopRule"); if (oldRule != null) oldRule.gameObject.SetActive(false);
            // The in-game menu is intentionally quiet: identity comes from the title alone.
            var eyebrow = Find(main, "BrandEyebrow"); if (eyebrow != null) eyebrow.gameObject.SetActive(false);
            var title = Find(main, "GameTitle"); At(title, TL, 116, -226, 720, 168);
            Text(title, "OUTBREAK", 112, true).characterSpacing = 3;
            var secondLine = Label(main, "GameTitleSecondLine", "PROTOCOL", 120, 361, 720, 98, 66, true);
            secondLine.font = Narrow; secondLine.fontSharedMaterial = Narrow.material; secondLine.characterSpacing = 13;
            var tagline = Find(main, "Tagline"); if (tagline != null) tagline.gameObject.SetActive(false);
            var play = Find(main, "PlayBtn"); At(play, TL, 120, -546, 430, 64); MenuButton(play, "PLAY ONLINE", true);
            var settings = Find(main, "SettingsBtn"); At(settings, TL, 120, -622, 430, 64); MenuButton(settings, "SETTINGS");
            var quit = Find(main, "QuitBtn"); At(quit, TL, 120, -698, 430, 64); MenuButton(quit, "QUIT TO DESKTOP");
            var footer = Find(main, "MenuFooter"); if (footer != null) footer.gameObject.SetActive(false);
            var card = Find(main, "OperationCard"); if (card != null) card.gameObject.SetActive(false);

            var popup = Find(c.transform, "PlayPopup"); Modal(popup);
            var shell = Find(popup, "PlayOnlineModal"); At(shell, Center, 0, 0, 1100, 700); Plate(shell, TacticalUiTheme.Surface, true);
            StyleCommon(shell);
            At(Find(shell, "TopRule"), TL, 0, 0, 1100, 4); Plate(Find(shell, "TopRule"), TacticalUiTheme.Accent);
            At(Find(shell, "Title"), TL, 48, -42, 760, 62); Text(Find(shell, "Title"), "ASSEMBLE YOUR SQUAD", 54, true);
            At(Find(shell, "Subtitle"), TL, 48, -110, 830, 32); Text(Find(shell, "Subtitle"), "Create a room or join your teammates with a room code.", 22, false, TacticalUiTheme.Muted);
            At(Find(shell, "ClosePlayBtn"), TR, -32, -36, 140, 48); Button(Find(shell, "ClosePlayBtn"), "BACK", false, 20);
            ButtonIcon(Find(shell, "ClosePlayBtn"), "ArrowBack");
            At(Find(shell, "CreateTitle"), TL, 48, -176, 800, 28); Text(Find(shell, "CreateTitle"), "YOUR CALLSIGN", 18, false, TacticalUiTheme.Muted);
            var callsign = Find(shell, "CallsignInput"); At(callsign, TL, 48, -220, 850, 60); callsign.GetComponent<TMP_InputField>().characterLimit = 24;
            if (callsign.GetComponent<TMP_InputField>().placeholder is TMP_Text callsignHint) callsignHint.text = "Your callsign";
            At(Find(shell, "SaveNameBtn"), TL, 914, -220, 138, 60); Button(Find(shell, "SaveNameBtn"), "SAVE", false, 22);
            Rule(shell, "SectionRule", 48, 316, 1004);
            Label(shell, "HostHeading", "LEAD A NEW SQUAD", 48, 344, 450, 44, 34, true);
            Label(shell, "HostDescription", "Create a room, then share the code.", 48, 404, 456, 56, 21, false, TacticalUiTheme.Muted);
            At(Find(shell, "HostRoomBtn"), TL, 48, -500, 460, 64); Button(Find(shell, "HostRoomBtn"), "CREATE ROOM", true, 24);
            At(Find(shell, "JoinTitle"), TL, 566, -344, 450, 44); Text(Find(shell, "JoinTitle"), "JOIN YOUR TEAM", 34, true);
            Label(shell, "JoinDescription", "Enter the code shared by your squad leader.", 566, 404, 468, 56, 21, false, TacticalUiTheme.Muted);
            At(Find(shell, "JoinCodeInput"), TL, 566, -500, 268, 64);
            At(Find(shell, "JoinRoomBtn"), TL, 850, -500, 202, 64); Button(Find(shell, "JoinRoomBtn"), "JOIN ROOM", false, 22);
            var status = Label(shell, "ConnectionStatus", "Ready to connect.", 48, 610, 1004, 58, 21, false, TacticalUiTheme.Accent);
            var lobby = c.GetComponentInChildren<LobbyUI>(true); Bind(lobby, "statusText", status);
            main.gameObject.SetActive(true); popup.gameObject.SetActive(false);
            Settings(Find(c.transform, "SettingsPanel"));
        }

        public static void Settings(RectTransform root)
        {
            if (root == null) return;
            var shell = root.Find("SettingsShell") as RectTransform;
            if (shell == null)
            {
                var children = root.Cast<Transform>().ToArray();
                shell = Node(root, "SettingsShell");
                foreach (var child in children) child.SetParent(shell, false);
            }
            Modal(root); At(shell, Center, 0, 0, 1280, 900); Plate(shell, TacticalUiTheme.Surface, true); StyleCommon(shell);
            At(Find(shell, "TopRule"), TL, 0, 0, 1280, 4); Plate(Find(shell, "TopRule"), TacticalUiTheme.Accent);
            At(Find(shell, "Title"), TL, 44, -32, 960, 64); Text(Find(shell, "Title"), "SETTINGS", 58, true);
            At(Find(shell, "Subtitle"), TL, 46, -102, 1010, 32); Text(Find(shell, "Subtitle"), "Changes apply instantly.", 21, false, TacticalUiTheme.Muted);
            At(Find(shell, "CloseBtn"), TR, -36, -36, 140, 48); Button(Find(shell, "CloseBtn"), "BACK", false, 20);
            ButtonIcon(Find(shell, "CloseBtn"), "ArrowBack");
            var tabs = Find(shell, "Tabs"); At(tabs, TL, 44, -164, 1192, 60);
            string[] names = { "AudioTabBtn", "GraphicsTabBtn", "InputTabBtn" };
            string[] labels = { "AUDIO", "GRAPHICS", "CONTROLS" };
            for (int i = 0; i < 3; i++)
            {
                var tab = Find(tabs, names[i]); At(tab, TL, i * 244, 0, 228, 56); Button(tab, labels[i], false, 21);
                ButtonIcon(tab, i == 0 ? "Audio" : i == 1 ? "Settings" : "Controls");
                var rule = Node(tab, "SelectionRule"); At(rule, BL, 16, 0, 196, 3); Plate(rule, TacticalUiTheme.Accent); rule.gameObject.SetActive(i == 0);
            }
            var content = Find(shell, "Content"); Fill(content, 44, 104, 44, 252); Plate(content, Color.clear);
            foreach (var name in new[] { "AudioPanel", "GraphicsPanel", "InputPanel" }) Fill(Find(content, name));
            var audio = Find(content, "AudioPanel"); var graphics = Find(content, "GraphicsPanel"); var input = Find(content, "InputPanel");
            foreach (var page in new[] { audio, graphics, input })
            {
                var title = Find(page, "PageTitle"); At(title, TL, 28, -20, 1100, 44); Text(title, page == input ? "MOUSE & KEYBOARD" : page == audio ? "AUDIO MIX" : "DISPLAY QUALITY", 36, true);
            }
            SliderRow(Find(audio, "MasterSliderRow"), 94); SliderRow(Find(audio, "MusicSliderRow"), 182); SliderRow(Find(audio, "SFXSliderRow"), 270);
            At(Find(audio, "InstantStatus"), TL, 28, -382, 1050, 40); Text(Find(audio, "InstantStatus"), "Balance game audio to hear threats and your surroundings clearly.", 21, false, TacticalUiTheme.Muted);
            At(Find(graphics, "QualityLabel"), TL, 28, -110, 400, 48); Text(Find(graphics, "QualityLabel"), "Quality preset", 23);
            At(Find(graphics, "QualityDropdown"), TL, 500, -110, 580, 56);
            Label(graphics, "QualityHelp", "Choose a lower preset for a steadier frame rate.\nThe change takes effect immediately.", 28, 212, 1040, 84, 22, false, TacticalUiTheme.Muted);
            SliderRow(Find(input, "SensitivitySliderRow"), 80);
            At(Find(input, "KeybindTitle"), TL, 28, -176, 700, 38); Text(Find(input, "KeybindTitle"), "KEY BINDINGS", 30, true);
            At(Find(input, "RebindStatusText"), TL, 28, -220, 1100, 34); Text(Find(input, "RebindStatusText"), "Select a binding to change it. Esc cancels.", 19, false, TacticalUiTheme.Muted);
            string[] rows = { "FireRow", "AimRow", "ReloadRow", "Weapon1Row", "Weapon2Row", "InteractRow", "GrenadeRow" };
            for (int i = 0; i < rows.Length; i++)
            {
                var row = Find(input, rows[i]); At(row, TL, 28 + (i % 2) * 578, -(274 + (i / 2) * 62), 554, 52);
                Plate(row, Color.clear);
                Rule(row, "RowDivider", 0, 51, 554, new Color(.44f,.42f,.38f,.25f));
                var label = Find(row, "ActionLabel"); At(label, TL, 16, -4, 234, 44); Text(label, label.GetComponent<TMP_Text>().text, 21);
                var button = row.GetComponentInChildren<Button>(true); At((RectTransform)button.transform, TL, 262, -2, 276, 48); Button((RectTransform)button.transform, null, false, 21);
            }
            var reset = Find(shell, "ResetDefaultsBtn"); At(reset, BR, -44, 28, 248, 52); Button(reset, "RESET DEFAULTS", false, 19);
            var footer = Label(shell, "FooterHint", "ESC  /  BACK", 44, 822, 600, 48, 18, false, TacticalUiTheme.Muted);
            Label(shell, "ArtCredits", "UI art: SunGraphica / lotus_garden", 260, 828, 590, 36, 16, false, TacticalUiTheme.Muted);
            audio.gameObject.SetActive(true); graphics.gameObject.SetActive(false); input.gameObject.SetActive(false);
            root.gameObject.SetActive(false);
        }
        static void SliderRow(RectTransform row, float y)
        {
            if (row == null) return;
            At(row, TL, 28, -y, 1136, 68); Plate(row, Color.clear);
            Rule(row, "RowDivider", 0, 67, 1136, new Color(.44f,.42f,.38f,.25f));
            var label = Find(row, "Label"); At(label, TL, 18, -10, 332, 48); Text(label, label.GetComponent<TMP_Text>().text, 23);
            var value = Find(row, "ValueText"); At(value, TR, -18, -10, 108, 48); Text(value, value.GetComponent<TMP_Text>().text, 23, false, TacticalUiTheme.Accent).alignment = TextAlignmentOptions.MidlineRight;
            var slider = row.GetComponentInChildren<Slider>(true); var r = (RectTransform)slider.transform;
            At(r, TL, 400, -12, 590, 44);
            var bg = Find(r, "Background"); Fill(bg, 0, 17, 0, 17); SurvivalUiArt.Assign(Plate(bg, TacticalUiTheme.Border), "Bars/HealthTrack", TacticalUiTheme.Border, true);
            var fillArea = Find(r, "Fill Area"); Fill(fillArea, 8, 18, 8, 18);
            if (slider.fillRect != null) { slider.fillRect.offsetMin = slider.fillRect.offsetMax = Vector2.zero; SurvivalUiArt.Assign(Plate(slider.fillRect, TacticalUiTheme.Accent), "Bars/HealthFill", TacticalUiTheme.Accent); }
            var handleArea = Find(r, "Handle Slide Area"); Fill(handleArea, 8, 0, 8, 0);
            if (slider.handleRect != null) { slider.handleRect.sizeDelta = new Vector2(16, 0); slider.handleRect.offsetMin = new Vector2(-8, 8); slider.handleRect.offsetMax = new Vector2(8, -8); Plate(slider.handleRect, TacticalUiTheme.Text, true); }
        }

        public static void Lobby(Canvas c)
        {
            ConfigureCanvas(c); StyleCommon(c.transform); Backdrop(c);
            foreach (var ruleImage in c.GetComponentsInChildren<Image>(true))
                if (ruleImage.name.Contains("Rule")) ruleImage.color = TacticalUiTheme.Accent;
            var header = Find(c.transform, "LobbyHeader"); header.anchorMin = new Vector2(0, 1); header.anchorMax = Vector2.one; header.pivot = TL;
            header.offsetMin = new Vector2(64, -148); header.offsetMax = new Vector2(-64, -48); Plate(header, TacticalUiTheme.Surface);
            At(Find(header, "Title"), TL, 28, -20, 820, 62); Text(Find(header, "Title"), "SAFEHOUSE / SQUAD", 52, true);
            At(Find(header, "RoomCodeText"), TR, -180, -26, 380, 48); Text(Find(header, "RoomCodeText"), "ROOM CODE: --", 28, true, TacticalUiTheme.Accent).alignment = TextAlignmentOptions.MidlineRight;
            At(Find(header, "CopyCodeBtn"), TR, -24, -26, 130, 48); Button(Find(header, "CopyCodeBtn"), "COPY", false, 21);
            var roster = Find(c.transform, "RosterPanel"); roster.anchorMin = new Vector2(0, 0); roster.anchorMax = new Vector2(.65f, 1); roster.offsetMin = new Vector2(64, 64); roster.offsetMax = new Vector2(-20, -180); Plate(roster, TacticalUiTheme.Surface);
            At(Find(roster, "RosterTitle"), TL, 32, -28, 650, 52); Text(Find(roster, "RosterTitle"), "YOUR OPERATORS", 40, true);
            At(Find(roster, "PlayerCountText"), TR, -32, -36, 250, 38); Text(Find(roster, "PlayerCountText"), "0/4 Players", 22, false, TacticalUiTheme.Muted).alignment = TextAlignmentOptions.MidlineRight;
            var room = c.GetComponentInChildren<WaitingRoomUI>(true); var so = new SerializedObject(room);
            var list = (Transform)so.FindProperty("playerListContainer").objectReferenceValue;
            var listRect = (RectTransform)list; Fill(listRect, 28, 142, 28, 114);
            var layout = Ensure<VerticalLayoutGroup>(list.gameObject); layout.padding = new RectOffset(); layout.spacing = 16; layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var fitter = list.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            var tip = Node(roster, "RosterHint"); At(tip, BL, 32, 32, 900, 68); Text(tip, "Share your code. Choose an operator. Mark yourself ready.\nThe squad leader launches when everyone is ready.", 22, false, TacticalUiTheme.Muted);
            var mission = Find(c.transform, "MissionPanel"); mission.anchorMin = new Vector2(.65f, 0); mission.anchorMax = Vector2.one; mission.offsetMin = new Vector2(12, 64); mission.offsetMax = new Vector2(-64, -180); Plate(mission, TacticalUiTheme.Surface);
            At(Find(mission, "MissionTitle"), TL, 28, -28, 470, 52); Text(Find(mission, "MissionTitle"), "DEPLOYMENT", 40, true);
            At(Find(mission, "DifficultyLabel"), TL, 28, -110, 460, 32); Text(Find(mission, "DifficultyLabel"), "DIFFICULTY  /  LEADER ONLY", 18, false, TacticalUiTheme.Muted);
            var leaderLock = Node(mission, "LeaderLock"); At(leaderLock, TL, 28, -116, 18, 18);
            SurvivalUiArt.Assign(Plate(leaderLock, TacticalUiTheme.Muted), "Icons/Lock", TacticalUiTheme.Muted);
            At(Find(mission, "DifficultyLabel"), TL, 58, -110, 430, 32);
            At(Find(mission, "DifficultyDropdown"), TL, 28, -158, 510, 56);
            Label(mission, "CharacterLabel", "YOUR OPERATOR", 28, 242, 480, 32, 18, false, TacticalUiTheme.Muted);
            At(Find(mission, "CharacterDropdown"), TL, 28, -286, 510, 56);
            var characters = Find(mission, "CharacterDropdown").GetComponent<TMP_Dropdown>();
            characters.ClearOptions(); characters.AddOptions(new System.Collections.Generic.List<string> { "Clove", "Brimstone", "Sage", "Gekko" }); characters.SetValueWithoutNotify(0);
            At(Find(mission, "StatusText"), TL, 28, -382, 510, 100); Text(Find(mission, "StatusText"), "Waiting for your squad...", 23, false, TacticalUiTheme.Muted);
            At(Find(mission, "ReadyBtn"), BL, 28, 218, 510, 64); Button(Find(mission, "ReadyBtn"), "READY", true, 24);
            At(Find(mission, "StartGameBtn"), BL, 28, 138, 510, 64); Button(Find(mission, "StartGameBtn"), "DEPLOY SQUAD", false, 24);
            At(Find(mission, "LeaveBtn"), BL, 28, 40, 510, 56); Button(Find(mission, "LeaveBtn"), "LEAVE ROOM", false, 21);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UiPath + "Prefabs/PlayerEntry_Tactical.prefab"); Bind(room, "playerEntryPrefab", prefab);
        }

        public static void Gameplay(Canvas c)
        {
            ConfigureCanvas(c);
            var hud = Find(c.transform, "MinimalHUDRoot"); StyleCommon(hud);
            var health = Find(hud, "HealthPanel"); At(health, BL, 56, 44, 290, 88);
            foreach (var name in new[] { "HealthBackground", "HealthReadabilityPlate" }) { var r = Find(health, name); if (r != null) { Fill(r); if(name == "HealthBackground") HudPlate(r); else Plate(r, Color.clear); } }
            var healthText = Find(health, "HealthText"); At(healthText, TL, 18, -2, 250, 58); Text(healthText, "100 / 100", 42, true);
            var track = Node(health,"HealthTrack"); At(track, BL, 18, 17, 240, 5); Plate(track,TacticalUiTheme.Border); track.SetSiblingIndex(2);
            var bar = Node(health, "HealthBar"); At(bar, BL, 18, 17, 240, 5); var fill = Plate(bar, TacticalUiTheme.Text);
            ConfigureFill(fill);
            var manager = c.gameObject.scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<HUDManager>(true)).First(); Bind(manager, "healthFill", fill);
            var infection = Find(hud, "InfectionPanel"); At(infection, BL, 56, 142, 290, 78); Plate(infection, Color.clear);
            At(Find(infection, "InfectionStageText"), TL, 18, -4, 252, 30); Text(Find(infection, "InfectionStageText"), "INFECTION 0%", 18, false, TacticalUiTheme.Muted);
            At(Find(infection, "InfectionIcon"), TL, 16, -16, 24, 24);
            At(Find(infection, "InfectionFill"), BL, 18, 32, 240, 4);
            At(Find(infection, "TreatmentProgressFill"), BL, 18, 25, 240, 3);
            Find(infection, "InfectionFill").GetComponent<Image>().color = TacticalUiTheme.Accent;
            Find(infection, "InfectionFill").GetComponent<Image>().fillAmount = 0;
            Find(infection, "TreatmentProgressFill").GetComponent<Image>().color = TacticalUiTheme.Success;
            Find(infection, "TreatmentProgressFill").GetComponent<Image>().fillAmount = 0;
            ConfigureFill(Find(infection, "InfectionFill").GetComponent<Image>());
            ConfigureFill(Find(infection, "TreatmentProgressFill").GetComponent<Image>());
            Find(infection, "InfectionIcon").GetComponent<Image>().enabled = false;
            At(Find(infection, "SepsisWarning"), BL, 16, 3, 268, 22); Text(Find(infection, "SepsisWarning"), "SEPSIS - TREAT NOW", 17, false, TacticalUiTheme.Danger);
            var weapon = Find(hud, "WeaponPanel"); At(weapon, BR, -56, 44, 420, 148);
            foreach (var name in new[] { "WeaponFrame", "WeaponReadabilityPlate" }) { var r = Find(weapon, name); if (r != null) { Fill(r); if(name == "WeaponFrame") HudPlate(r,true); else Plate(r,Color.clear); } }
            At(Find(weapon,"CurrentAmmoText"),TR,-82,-12,104,86); Text(Find(weapon, "CurrentAmmoText"), "24", 70, true).alignment = TextAlignmentOptions.MidlineRight;
            At(Find(weapon,"ReserveAmmoText"),TR,-8,-42,64,42); Text(Find(weapon, "ReserveAmmoText"), "120", 30, true, TacticalUiTheme.Muted).alignment = TextAlignmentOptions.MidlineRight;
            At(Find(weapon,"AmmoSeparator"),TR,-76,-35,1,40); Plate(Find(weapon,"AmmoSeparator"),TacticalUiTheme.Muted);
            At(Find(weapon,"WeaponNameText"),TL,20,-65,205,32); Text(Find(weapon, "WeaponNameText"), "AKM", 25, true);
            At(Find(weapon,"WeaponIcon"),TL,44,-22,150,44);
            At(Find(weapon,"PrimaryWeaponKeyText"),TL,18,-22,24,28);
            var ammoIcon = Find(weapon, "AmmoIconImg");
            if (ammoIcon != null) ammoIcon.gameObject.SetActive(false);
            foreach (var name in new[] { "WeaponIconPlate", "SecondaryWeaponSlot", "FragSlot" }) { var r = Find(weapon, name); if (r != null) Plate(r, Color.clear); }
            foreach (var name in new[] { "UnusedWeaponNameText", "FragLabel", "GrenadeKeyText", "SlotKey", "PrimaryWeaponKeyText" }) { var r = Find(weapon, name); if (r != null) Text(r, r.GetComponent<TMP_Text>().text, 16); }
            foreach (var slotName in new[] { "SecondaryWeaponSlot", "FragSlot" })
            {
                var slot = Find(weapon, slotName);
                foreach (var background in slot.GetComponentsInChildren<Image>(true))
                    if (background.name.Contains("Frame") || background.name.Contains("Background")) Plate((RectTransform)background.transform, Color.clear);
            }
            var secondary = Find(weapon,"SecondaryWeaponSlot"); At(secondary,BL,18,4,172,36);
            At(Find(secondary,"SlotKey"),TL,0,-4,24,24);
            At(Find(secondary,"UnusedWeaponIcon"),TL,26,-4,58,28);
            var secondaryLabel=Find(secondary,"UnusedWeaponNameText"); At(secondaryLabel,TL,88,-4,84,24); Text(secondaryLabel,"EMPTY",16,false,TacticalUiTheme.Muted);
            var frag = Find(weapon, "FragSlot");
            At(frag,BR,-4,4,148,36);
            At(Find(frag,"GrenadeKeyText"),TL,0,-4,20,24);
            At(Find(frag,"FragIcon"),TL,27,-4,13,22); Plate(Find(frag,"FragIcon"),TacticalUiTheme.Muted);
            At(Find(frag,"FragCap"),TL,29,0,9,4); Plate(Find(frag,"FragCap"),TacticalUiTheme.Muted);
            At(Find(frag,"FragLabel"),TL,52,-4,52,24);
            At(Find(frag, "GrenadeCountText"), TR, -4, -2, 32, 28);
            Text(Find(frag, "GrenadeCountText"), "2", 22).alignment = TextAlignmentOptions.MidlineRight;
            var crosshair = Find(hud, "Crosshair");
            foreach (var line in crosshair.GetComponentsInChildren<Image>())
                line.color = line.name.Contains("Shadow") ? Color.black : TacticalUiTheme.Text;
            var match = Find(hud, "MatchStateText"); At(match, new Vector2(.5f, 1), 0, -40, 580, 64); Text(match, "", 40, true).alignment = TextAlignmentOptions.Center;
            match.gameObject.SetActive(false);
            var prompt = Find(hud, "InteractionPrompt"); At(prompt, Center, 0, -116, 640, 48); Text(prompt, "[F] Interact", 23).alignment = TextAlignmentOptions.Center;
            var respawn = Find(hud, "RespawnCountdownText"); At(respawn, Center, 0, 70, 640, 64); Text(respawn, "", 36, true).alignment = TextAlignmentOptions.Center;
            ApplyHudArt(hud, manager);
            foreach (var graphic in hud.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var pause = Find(c.transform, "PausePanel"); Modal(pause);
            var shell = Find(pause, "PauseShell"); At(shell, Center, 0, 0, 600, 590); Plate(shell, TacticalUiTheme.Surface, true);
            StyleCommon(shell); At(Find(shell, "Title"), TL, 48, -44, 504, 70); Text(Find(shell, "Title"), "MENU", 62, true);
            Label(shell, "OnlineNotice", "Online play continues while this menu is open.", 48, 124, 504, 66, 22, false, TacticalUiTheme.Muted);
            At(Find(shell, "ResumeButton"), TL, 48, -226, 504, 68); MenuButton(Find(shell, "ResumeButton"), "RESUME", true);
            At(Find(shell, "SettingsButton"), TL, 48, -314, 504, 64); MenuButton(Find(shell, "SettingsButton"), "SETTINGS");
            At(Find(shell, "LeaveMatchButton"), TL, 48, -408, 504, 64); MenuButton(Find(shell, "LeaveMatchButton"), "LEAVE MATCH");
            pause.gameObject.SetActive(false); Settings(Find(c.transform, "SettingsPanel"));
        }

        static void ConfigureFill(Image image)
        {
            // UGUI bypasses filled geometry when no sprite is assigned.
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(UiPath + "Sprites/Survival/SolidFill.png");
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0; image.raycastTarget = false;
        }

        static void ApplyHudArt(RectTransform hud, HUDManager manager)
        {
            var health = Find(hud, "HealthPanel"); At(health, BL, 56, 48, 326, 106);
            var label = Find(health, "HealthText"); At(label, TL, 55, -8, 250, 58);
            var icon = Node(health, "HealthIcon"); At(icon, TL, 18, -25, 27, 24);
            SurvivalUiArt.Assign(Plate(icon, TacticalUiTheme.Text), "Icons/Health", TacticalUiTheme.Text);
            var frame = Node(health, "HealthFrame"); At(frame, BL, 18, 18, 280, 19);
            SurvivalUiArt.Assign(Plate(frame, TacticalUiTheme.Text), "Bars/HealthFrame", new Color(.7f,.68f,.64f));
            var track = Find(health, "HealthTrack"); At(track, BL, 24, 22, 265, 10);
            SurvivalUiArt.Assign(Plate(track, TacticalUiTheme.Control), "Bars/HealthTrack", TacticalUiTheme.Control, true);
            var bar = Find(health, "HealthBar"); At(bar, BL, 24, 22, 265, 10);
            var fill = bar.GetComponent<Image>(); SurvivalUiArt.Assign(fill, "Bars/HealthFill", TacticalUiTheme.Text);
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = 1;
            // The separate track/fill crops remove transparent gutters before fillAmount clips geometry.
            track.SetAsLastSibling(); bar.SetAsLastSibling(); frame.SetAsLastSibling();

            var infection = Find(hud, "InfectionPanel"); At(infection, BL, 56, 165, 326, 72);
            At(Find(infection, "InfectionIcon"), TL, 18, -7, 22, 22);
            var infectionIcon = Find(infection, "InfectionIcon").GetComponent<Image>();
            SurvivalUiArt.Assign(infectionIcon, "Icons/Biohazard", TacticalUiTheme.Accent);
            At(Find(infection, "InfectionStageText"), TL, 52, -4, 250, 30);
            foreach (string name in new[] { "InfectionFill", "TreatmentProgressFill" })
            {
                var image = Find(infection, name).GetComponent<Image>();
                image.sprite = SurvivalUiArt.Get("Bars/HealthFill");
            }

            var weapon = Find(hud, "WeaponPanel");
            var frag = Find(weapon, "FragSlot");
            var grenade = Find(frag, "FragIcon"); At(grenade, TL, 26, -5, 36, 24);
            var grenadeImage = Plate(grenade, TacticalUiTheme.Text);
            SurvivalUiArt.Assign(grenadeImage, "Icons/Grenade", TacticalUiTheme.Text); grenadeImage.preserveAspect = true;
            Find(frag, "FragCap").gameObject.SetActive(false);
            Find(frag, "FragLabel").gameObject.SetActive(false);
            var reserve = Find(weapon, "ReserveAmmoText"); At(reserve, TR, -8, -42, 82, 42);
            Text(reserve, "/ 120", 28, true, TacticalUiTheme.Muted).alignment = TextAlignmentOptions.MidlineRight;
            At(Find(weapon, "CurrentAmmoText"), TR, -100, -12, 104, 86);
            Find(weapon, "AmmoSeparator").gameObject.SetActive(false);

            var blood = Node(hud, "LowHealthBlood");
            blood.anchorMin = Vector2.zero; blood.anchorMax = new Vector2(1, 0); blood.pivot = new Vector2(.5f, 0);
            blood.anchoredPosition = Vector2.zero; blood.sizeDelta = new Vector2(0, 115); blood.SetAsFirstSibling();
            var bloodImage = Plate(blood, Color.clear);
            SurvivalUiArt.Assign(bloodImage, "Effects/BloodEdge", Color.clear); bloodImage.enabled = false;
            Bind(manager, "healthDangerBackground", bloodImage);
        }

        static void RosterPrefab()
        {
            string path = UiPath + "Prefabs/PlayerEntry_Tactical.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var r = (RectTransform)root.transform; r.sizeDelta = new Vector2(1000, 112);
                SurvivalUiArt.Assign(Plate(r, TacticalUiTheme.Control), "Controls/Control", TacticalUiTheme.Control, true);
                var element = Ensure<LayoutElement>(root); element.preferredHeight = 112; element.minHeight = 112; element.flexibleHeight = 0;
                foreach (var old in root.GetComponentsInChildren<TMP_Text>(true)) old.gameObject.SetActive(false);
                var name = Label(r, "OperatorName", "Operator", 28, 14, 670, 40, 29);
                name.richText = false;
                var detail = Label(r, "OperatorDetail", "CHOOSE YOUR OPERATOR", 28, 62, 650, 30, 18, false, TacticalUiTheme.Muted);
                var readiness = Node(r, "Readiness"); At(readiness, TR, -24, -34, 252, 44); var status = Text(readiness, "NOT READY", 19, false, TacticalUiTheme.Muted); status.alignment = TextAlignmentOptions.MidlineRight;
                // Leave room for long names at every roster width.
                name.rectTransform.anchorMax = new Vector2(1, 1); name.rectTransform.offsetMin = new Vector2(28, -54); name.rectTransform.offsetMax = new Vector2(-300, -14);
                detail.rectTransform.anchorMax = new Vector2(1, 1); detail.rectTransform.offsetMin = new Vector2(28, -94); detail.rectTransform.offsetMax = new Vector2(-300, -62);
                name.gameObject.SetActive(true); detail.gameObject.SetActive(true); status.gameObject.SetActive(true);
                var view = Ensure<PlayerRosterEntryView>(root); Bind(view, "playerName", name); Bind(view, "detail", detail); Bind(view, "readiness", status);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Tools/FPS/UI/Apply Survival Horror Design")]
        public static void ApplyAll()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before authoring UI.");
            EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            SurvivalUiArt.ConfigureImports();
            var active = SceneManager.GetActiveScene();
            try
            {
                RosterPrefab();
                var prefab = PrefabUtility.LoadPrefabContents(UiPath + "Prefabs/SettingsPanel.prefab");
                try { Settings((RectTransform)prefab.transform); PrefabUtility.SaveAsPrefabAsset(prefab, UiPath + "Prefabs/SettingsPanel.prefab"); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
                foreach (var name in new[] { "MainMenu", "LobbyScene", "GameScene" })
                {
                    string path = "Assets/FPS/Scenes/" + name + ".unity";
                    var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded;
                    if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    try
                    {
                        var canvas = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
                        Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Apply tactical UI");
                        if (name == "MainMenu") MainMenu(canvas); else if (name == "LobbyScene") Lobby(canvas); else Gameplay(canvas);
                        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                    }
                    finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
                }
            }
            finally
            {
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                EditorSceneManager.SaveOpenScenes(); AssetDatabase.SaveAssets();
            }
            Debug.Log("Tactical UI applied to MainMenu, LobbyScene, GameScene and shared prefabs.");
        }
    }
}
