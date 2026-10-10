using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class HarvestTileClickPatch
    {
        [HarmonyPatch(typeof(Growing), "Harvest", new System.Type[] { typeof(GameObject) })]
        [HarmonyPrefix]
        public static bool Harvest_Prefix(Growing __instance, GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            // Мы намеренно пропускаем проверку на ManualHarvest, чтобы клик по тайлу работал всегда!

            bool destroy = false;

            // ИСПРАВЛЕНИЕ 1: Оригинальная игра использует go.GetComponent<grow>(),
            // что НЕ РАБОТАЕТ для винограда, так как компонент висит на дочернем объекте!
            // Мы меняем это на GetComponentInChildren<grow>()
            grow g = go.GetComponentInChildren<grow>();

            if (g != null)
            {
                bool isVine = g.visibleGrownItems != null && g.visibleGrownItems.Length > 0;
                bool isSpoiled = g.growSpoil != null && g.growSpoil.spoiled;

                // ИСПРАВЛЕНИЕ 3: Игра выкорчевывает лианы, если на них в данный момент нет плодов.
                // Мы перехватываем это: здоровые лианы только отдают плоды, но не выкорчевываются!
                if (isVine && !isSpoiled)
                {
                    int harvestedFruits = 0;
                    foreach (var gi in g.visibleGrownItems)
                    {
                        if (gi.gameObject.activeInHierarchy && gi.readyToPick)
                        {
                            harvestedFruits++;
                            gi.GetItem(); // Само добавит в инвентарь
                            gi.ToggleVisibility(false);
                            gi.ResetGrow();
                        }
                    }

                    if (harvestedFruits > 0 && g.HarvestSound != null)
                    {
                        g.HarvestSound.PlayMisc(g.gameObject);
                    }
                    destroy = false;
                }
                else
                {
                    // Для обычной картошки/капусты или испорченных лиан — вызываем стандартный сбор с выкорчевыванием
                    List<ItemStack> itemStacks = g.HarvestExternal(out destroy);

                    // ИСПРАВЛЕНИЕ 2: Кладем урожай напрямую в инвентарь (или на пол, если нет места), 
                    // чтобы он не исчезал из-за бага со Storage
                    foreach (ItemStack itemStack in itemStacks)
                    {
                        ItemOperations.AddItems(itemStack);
                        if (itemStack.itemReference != null && itemStack.itemReference.Item != null)
                        {
                            itemStack.itemReference.Item.PlaySound();
                        }
                    }
                }
            }
            else
            {
                CollectibleItem componentInChildren = go.GetComponentInChildren<CollectibleItem>();
                if (componentInChildren != null)
                {
                    for (int i = 0; i < componentInChildren.amount; i++)
                    {
                        ItemOperations.AddItems(componentInChildren.Item.Item.ID, __instance.owner != null ? __instance.owner.ID : -1, 1, componentInChildren.meta);
                        if (componentInChildren.Item != null && componentInChildren.Item.Item != null)
                        {
                            componentInChildren.Item.Item.PlaySound();
                        }
                    }
                    destroy = true;
                }
            }

            if (destroy)
            {
                GrowingSpot spot = go.GetComponentInParent<GrowingSpot>();
                if (spot != null)
                {
                    spot.HarvestPlant(true);
                }
                UnityEngine.Object.Destroy(go);
            }

            // Обновляем UI грядки
            __instance.StartCoroutine("UpdateItemsInProgressDelayed");

            return false; // Возвращаем false, чтобы оригинальный багованный метод НЕ выполнялся
        }
    }

    [HarmonyPatch]
    public static class GrowingPanelManualHarvestPatch
    {
        [HarmonyPatch(typeof(GrowingPanel), "UpdateItemsInProgress")]
        [HarmonyPostfix]
        public static void UpdateItemsInProgress_Postfix(GrowingPanel __instance)
        {
            if (__instance == null || __instance.ActiveManu == null) return;
            if (!__instance.ActiveManu.ManualHarvest) return; // Если ManualHarvest false, игра сама все биндит, нам не нужно

            // Получаем список задействованных растений
            List<GameObject> list = new List<GameObject>();
            foreach (GrowingSpot growingSlot in __instance.ActiveManu.GrowingSlots)
            {
                if (growingSlot.Plant != null)
                {
                    list.Add(growingSlot.Plant);
                }
            }

            // Находим все созданные элементы UI для слотов
            var progressingItems = __instance.growingPanelUI.processSlotsUI.GetProgressingItems();

            // Пробегаемся по ним и принудительно биндим растение к слоту (чтобы по нему можно было кликать)
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && i < progressingItems.Count)
                {
                    ProgressSlot component = progressingItems[i].GetComponent<ProgressSlot>();
                    if (component == null)
                    {
                        component = progressingItems[i].GetComponentInChildren<ProgressSlot>();
                    }

                    if (component != null)
                    {
                        // Принудительно возвращаем кликабельность тайлу!
                        component.AddActiveGrowing(__instance.ActiveManu, list[i]);
                    }
                }
            }
        }
    }
}
