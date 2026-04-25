using System;
using CorsacCosmetics.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace CorsacCosmetics.Components;

public class InventoryTabPaginationBehaviour(nint cppPtr) : MonoBehaviour(cppPtr)
{
    public TextMeshPro title;
    public PassiveButton backButton;
    public PassiveButton nextButton;
    public InventoryTab tab;

    public int CurrentTab { get; private set; }
    public int MaxTab { get; private set; }

    public Func<string> GetTextFunction { get; private set; }

    public void Setup(InventoryTab inventoryTab, int maxTab, Func<string> getTextFunction)
    {
        tab = inventoryTab;
        MaxTab = maxTab;
        GetTextFunction = getTextFunction;

        if (!title)
        {
            title = inventoryTab.transform.Find("Text").GetComponent<TextMeshPro>();
            title.GetComponent<TextTranslatorTMP>().DestroyImmediate();
            title.text = GetTextFunction();
            title.transform.localPosition += new Vector3(1f, 0f, 0f);
        }

        if (!nextButton)
        {
            nextButton = Instantiate(PlayerCustomizationMenu.Instance.BackButton, inventoryTab.transform).GetComponent<PassiveButton>();
            nextButton.GetComponent<AspectPosition>().DestroyImmediate();
            nextButton.GetComponent<CloseButtonConsoleBehaviour>().DestroyImmediate();
            nextButton.name = "nextButton";
            nextButton.transform.localPosition = title.transform.localPosition - new Vector3(2.7f, 0f, 0f);
            nextButton.transform.localScale = new Vector3(0.5f, 0.5f, 1);
            nextButton.OnClick = new Button.ButtonClickedEvent();
            nextButton.OnClick.AddListener((UnityAction)NextPage);
            nextButton.GetComponent<SpriteRenderer>().sprite = Assets.NextButton;
        }

        if (!backButton)
        {
            backButton = Instantiate(nextButton, inventoryTab.transform);
            backButton.name = "backButton";
            backButton.transform.localPosition -= new Vector3(0.5f, 0f, 0f);
            backButton.OnClick = new Button.ButtonClickedEvent();
            backButton.OnClick.AddListener((UnityAction)PreviousPage);
            backButton.GetComponent<SpriteRenderer>().flipX = true;
        }
    }

    private void Update()
    {
        if (title)
        {
            title.text = GetTextFunction();
        }
    }

    public void NextPage()
    {
        CurrentTab++;
        if (CurrentTab > MaxTab)
        {
            CurrentTab = 0;
        }

        if (tab)
        {
            tab.enabled = false;
            tab.enabled = true;
        }
    }

    public void PreviousPage()
    {
        CurrentTab--;
        if (CurrentTab < 0) CurrentTab = MaxTab;

        if (tab)
        {
            tab.enabled = false;
            tab.enabled = true;
        }
    }
}