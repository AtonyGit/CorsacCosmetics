using AmongUs.Data;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CorsacCosmetics.Patches;

public static class HatsTabPatches
{
    public static int CurrentTab { get; private set; } = 0;
    
    public static TextMeshPro Title { get; private set; }

    public static PassiveButton BackButton { get; private set; }

    public static PassiveButton NextButton { get; private set; }

    public static HatsTab HatsTab { get; private set; }

    private static string GetText()
    {
        if (CurrentTab == 0)
            return "Hats";

        return "Custom Hats";
    }

    public static void NextPage()
    {
        CurrentTab++;
        if (CurrentTab > 1) CurrentTab = 0;

        if (HatsTab)
        {
            HatsTab.enabled = false;
            HatsTab.enabled = true;
        }
    }

    public static void PrevPage()
    {
        CurrentTab--;
        if (CurrentTab < 0) CurrentTab = 1;

        if (HatsTab)
        {
            HatsTab.enabled = false;
            HatsTab.enabled = true;
        }
    }

    public static bool ShowOnPage(string id)
    {
        if (CurrentTab == 0) return !id.StartsWith("corsac");

        return id.StartsWith("corsac");
    }

    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.OnEnable))]
    public static class HatsTabOnEnablePatch
    {
        public static bool Prefix(HatsTab __instance)
        {
            // --------- Pagination ----------------
            HatsTab = __instance;

            if (!Title)
            {
                Title = __instance.transform.Find("Text").GetComponent<TextMeshPro>();
                Title.GetComponent<TextTranslatorTMP>().DestroyImmediate();
                Title.text = GetText();
                Title.transform.localPosition += new Vector3(1f, 0f, 0f);
            }

            if (!NextButton)
            {
                NextButton = Object.Instantiate(PlayerCustomizationMenu.Instance.BackButton, __instance.transform).GetComponent<PassiveButton>();
                NextButton.GetComponent<AspectPosition>().DestroyImmediate();
                NextButton.GetComponent<CloseButtonConsoleBehaviour>().DestroyImmediate();
                NextButton.name = "NextButton";
                NextButton.transform.localPosition = Title.transform.localPosition - new Vector3(2.7f, 0f, 0f);
                NextButton.transform.localScale = new Vector3(0.5f, 0.5f, 1);
                NextButton.OnClick = new Button.ButtonClickedEvent();
                NextButton.OnClick.AddListener((UnityAction)NextPage);
                NextButton.GetComponent<SpriteRenderer>().sprite = Assets.NextButton;
            }

            if (!BackButton)
            {
                BackButton = Object.Instantiate(NextButton, __instance.transform);
                BackButton.name = "BackButton";
                BackButton.transform.localPosition -= new Vector3(0.5f, 0f, 0f);
                BackButton.OnClick = new Button.ButtonClickedEvent();
                BackButton.OnClick.AddListener((UnityAction)PrevPage);
                BackButton.GetComponent<SpriteRenderer>().flipX = true;
            }

            // ---------- Original Game Code -----------
            InventoryTabReversePatch.OnEnable(__instance);

            HatData[] unlockedHats = DestroyableSingleton<HatManager>.Instance.GetUnlockedHats();
            __instance.currentHat =
                DestroyableSingleton<HatManager>.Instance.GetHatById(DataManager.Player.Customization.Hat);

            var num = 0;
            foreach (var hat in unlockedHats)
            {
                if (!ShowOnPage(hat.ProductId)) continue;

                var num2 = __instance.XRange.Lerp(num % __instance.NumPerRow / (__instance.NumPerRow - 1f));
                var num3 = __instance.YStart - num / __instance.NumPerRow * __instance.YOffset;
                var colorChip = Object.Instantiate(__instance.ColorTabPrefab, __instance.scroller.Inner);
                colorChip.transform.localPosition = new Vector3(num2, num3, -1f);
                if (ActiveInputManager.currentControlType == ActiveInputManager.InputType.Keyboard)
                {
                    var hat1 = hat;
                    colorChip.Button.OnMouseOver.AddListener((UnityAction)(()=>
                    {
                        __instance.SelectHat(hat1);
                    }));
                    colorChip.Button.OnMouseOut.AddListener((UnityAction)(()=>
                    {
                        __instance.SelectHat(DestroyableSingleton<HatManager>.Instance.GetHatById(DataManager.Player.Customization.Hat));
                    }));
                    colorChip.Button.OnClick.AddListener((UnityAction)(()=>
                    {
                        __instance.ClickEquip();
                    }));
                }
                else
                {
                    colorChip.Button.OnClick.AddListener((UnityAction)(()=>
                    {
                        __instance.SelectHat(hat);
                    }));
                }
                colorChip.Button.ClickMask = __instance.scroller.Hitbox;
                colorChip.Inner.SetMaskType(PlayerMaterial.MaskType.SimpleUI);
                __instance.UpdateMaterials(colorChip.Inner.FrontLayer, hat);
                hat.SetPreview(colorChip.Inner.FrontLayer, __instance.GetDisplayColor());
                colorChip.Tag = hat;
                colorChip.SelectionHighlight.gameObject.SetActive(false);
                __instance.ColorChips.Add(colorChip);
                num++;
                if (!DestroyableSingleton<HatManager>.Instance.CheckLongModeValidCosmetic(hat.ProdId, __instance.PlayerPreview.GetIgnoreLongMode()))
                {
                    colorChip.SetUnavailable();
                }
            }

            __instance.currentHatIsEquipped = true;
            __instance.SetScrollerBounds();
            return false;
        }
    }

    [HarmonyPatch(typeof(HatsTab), nameof(HatsTab.Update))]
    public static class HatsTabUpdatePatch
    {
        public static bool Prefix(HatsTab __instance)
        {
            if (Title) Title.text = GetText();
            return true;
        }
    }
}