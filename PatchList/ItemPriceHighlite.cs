using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch(typeof(ItemData))]
    public static class ItemPriceHighlite
    {
        private static System.Runtime.CompilerServices.ConditionalWeakTable<ItemData, Image> _cachedHighlights = new System.Runtime.CompilerServices.ConditionalWeakTable<ItemData, Image>();

        [HarmonyPatch("UpdateItem")]
        [HarmonyPostfix]
        public static void UpdateItem_Postfix(ItemData __instance)
        {
            if (__instance.item == null) return;

            Image priceHighlightImg = null;

            if (!_cachedHighlights.TryGetValue(__instance, out priceHighlightImg) || priceHighlightImg == null)
            {
                Transform priceHighlightTransform = __instance.transform.Find("PriceHighlight");
                GameObject priceHighlightObj;

                if (priceHighlightTransform == null)
                {
                    priceHighlightObj = new GameObject("PriceHighlight");
                    priceHighlightObj.transform.SetParent(__instance.transform, false);

                    priceHighlightImg = priceHighlightObj.AddComponent<Image>();

                    // ����������� RectTransform ����� ��� ���� ������� �����
                    RectTransform rect = priceHighlightObj.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(0.1f, 0); // ����� ������ ����
                    rect.anchorMax = new Vector2(0.9f, 0); // ������ ������ ����
                    rect.pivot = new Vector2(0.5f, 0);
                    rect.sizeDelta = new Vector2(0, 2); // ������ ������� 2 �������
                    rect.anchoredPosition = new Vector2(0, 1); // ���� �����������
                }
                else
                {
                    priceHighlightObj = priceHighlightTransform.gameObject;
                    priceHighlightImg = priceHighlightObj.GetComponent<Image>();
                }

                // ��������� ���
                _cachedHighlights.Remove(__instance);
                _cachedHighlights.Add(__instance, priceHighlightImg);
            }

            GameObject imgObj = priceHighlightImg.gameObject;

            if (!Plugin.showHighlight)
            {
                if (imgObj.activeSelf) imgObj.SetActive(false);
                return;
            }

            float price = __instance.item.GetItemSalePriceUnrounded(__instance.meta) * __instance.amount;

            if (price >= Plugin.MinRedPrice.Value)
            {
                if (!imgObj.activeSelf) imgObj.SetActive(true);
                priceHighlightImg.color = Color.red;
            }
            else if (price >= Plugin.MinBluePrice.Value && price < Plugin.MinRedPrice.Value)
            {
                if (!imgObj.activeSelf) imgObj.SetActive(true);
                priceHighlightImg.color = Color.blue;
            }
            else if (price >= Plugin.MinGreenPrice.Value && price < Plugin.MinBluePrice.Value)
            {
                if (!imgObj.activeSelf) imgObj.SetActive(true);
                priceHighlightImg.color = Color.green;
            }
            else
            {
                if (imgObj.activeSelf) imgObj.SetActive(false);
            }
        }
    }
}
