using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;
using System.Collections.Generic;

namespace ObenseuerQualityOfLife.PatchList
{
    [HarmonyPatch]
    public static class FastDropButton
    {
        [HarmonyPatch(typeof(ItemData))]
        public static class ItemData_Click_Patch
        {
            [HarmonyPatch("OnPointerDown", new System.Type[] { typeof(UnityEngine.EventSystems.PointerEventData) })]
            [HarmonyPrefix]
            public static bool OnPointerDown_Prefix(ItemData __instance, UnityEngine.EventSystems.PointerEventData eventData)
            {
                if (UnityEngine.Input.GetKey(UnityEngine.KeyCode.LeftAlt))
                {
                    if (__instance.DoubleClick(false) || __instance.DoubleClick(true))
                    {
                        var clickedSlot = __instance.slotController;
                        if (clickedSlot == null || clickedSlot.itemStack == null || clickedSlot.itemStack.itemId == -1) return false;

                        int targetItemId = clickedSlot.itemStack.itemId;
                        bool movedAny = false;

                        if (clickedSlot.slot.playerSlot)
                        {
                            var playerSlots = Inventory.instance.AllSlots(false, true);
                            for (int i = playerSlots.Length - 1; i >= 0; i--)
                            {
                                var slot = playerSlots[i];
                                if (slot != null && slot.itemStack != null && slot.itemStack.itemId == targetItemId && slot.itemStack.itemAmount > 0)
                                {
                                    try
                                    {
                                        slot.MoveToOtherInventory(out int movedAmount, slot.itemStack.itemAmount);
                                        if (movedAmount > 0) movedAny = true;
                                    }
                                    catch (System.Exception ex)
                                    {
                                        Plugin.Log.LogError($"[ObenseuerQualityOfLife] Error moving item {slot.itemStack.itemId}: {ex.Message}");
                                    }
                                }
                            }
                        }
                        else
                        {
                            var foreignSlots = Inventory.instance.ForeignSlots;
                            if (foreignSlots != null)
                            {
                                for (int i = foreignSlots.Length - 1; i >= 0; i--)
                                {
                                    var slot = foreignSlots[i];
                                    if (slot != null && slot.itemStack != null && slot.itemStack.itemId == targetItemId && slot.itemStack.itemAmount > 0)
                                    {
                                        try
                                        {
                                            slot.MoveToOtherInventory(out int movedAmount, slot.itemStack.itemAmount);
                                            if (movedAmount > 0) movedAny = true;
                                        }
                                        catch (System.Exception ex)
                                        {
                                            Plugin.Log.LogError($"[ObenseuerQualityOfLife] Error moving item {slot.itemStack.itemId}: {ex.Message}");
                                        }
                                    }
                                }
                            }
                        }

                        if (movedAny)
                        {
                            __instance.item?.PlaySound();
                        }

                        return false; // Заменяем оригинальный Shift+DoubleClick
                    }
                }
                return true;
            }
        }
        [HarmonyPatch(typeof(Inventory), "Start")]
        public static class Inventory_Start_Patch
        {
            [HarmonyPostfix]
            public static void Postfix(Inventory __instance)
            {
                if (__instance.inventoryPanelUI != null && __instance.inventoryPanelUI.GetComponent<InventoryWatcher>() == null)
                {
                    __instance.inventoryPanelUI.gameObject.AddComponent<InventoryWatcher>();
                }
            }
        }

        public class InventoryWatcher : MonoBehaviour
        {
            private GameObject _dropButton;

            private void OnEnable()
            {
                if (_dropButton == null)
                {
                    CreateDropButton();
                }
            }

            private void Update()
            {
                if (_dropButton != null)
                {
                    bool isStorageOpen = Inventory.instance.storagePanelUI != null && Inventory.instance.storagePanelUI.gameObject.activeInHierarchy;
                    bool isBackpack = BackpackStorage.instance != null && BackpackStorage.instance.BackpackIsOpen();
                    bool isTrading = TradePanel.instance != null && TradePanel.instance.activeTrade != null;

                    bool shouldShow = isStorageOpen && !isBackpack && !isTrading;

                    if (_dropButton.activeSelf != shouldShow)
                    {
                        _dropButton.SetActive(shouldShow);
                    }
                }
            }

            private void CreateDropButton()
            {
                if (Inventory.instance == null || Inventory.instance.inventoryPanelUI == null) return;

                var buttons = Inventory.instance.inventoryPanelUI.transform.root.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                UnityEngine.UI.Button sortBtn = null;
                foreach (var b in buttons)
                {
                    bool isSort = b.name.ToLower().Contains("sort") || (b.GetComponentInChildren<TMPro.TMP_Text>() != null && b.GetComponentInChildren<TMPro.TMP_Text>().text.ToLower() == "sort");
                    if (isSort)
                    {
                        if (b.transform.parent != null && b.transform.parent.name.ToLower().Contains("storage")) continue;
                        if (!b.transform.IsChildOf(Inventory.instance.inventoryPanelUI.transform.root)) continue;

                        sortBtn = b;
                        break;
                    }
                }

                if (sortBtn != null)
                {
                    _dropButton = GameObject.Instantiate(sortBtn.gameObject, sortBtn.transform.parent);
                    _dropButton.name = "DropSimilarButton";

                    var txt = _dropButton.GetComponentInChildren<TMPro.TMP_Text>();
                    if (txt != null) txt.text = "Drop";

                    var rect = _dropButton.GetComponent<RectTransform>();
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x + rect.sizeDelta.x + 10, rect.anchoredPosition.y);

                    UnityEngine.UI.Button dropBtn = _dropButton.GetComponent<UnityEngine.UI.Button>();
                    dropBtn.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                    dropBtn.onClick.AddListener(() =>
                    {
                        FastDropButton.QuickStack();
                    });

                    Plugin.Log.LogInfo("[ObenseuerQualityOfLife] Drop (Quick Stack) button created via InventoryWatcher!");
                }
            }
        }

        public static void QuickStack()
        {
            if (BackpackStorage.instance != null && BackpackStorage.instance.BackpackIsOpen())
            {
                return;
            }

            var foreignSlots = Inventory.instance.ForeignSlots;
            if (foreignSlots == null || foreignSlots.Length == 0) return;

            System.Collections.Generic.HashSet<int> containerItemIds = new System.Collections.Generic.HashSet<int>();
            foreach (var slot in foreignSlots)
            {
                if (slot != null && slot.itemStack != null && slot.itemStack.itemId != -1 && slot.itemStack.itemAmount > 0)
                {
                    containerItemIds.Add(slot.itemStack.itemId);
                }
            }

            if (containerItemIds.Count == 0) return;

            var playerSlots = Inventory.instance.AllSlots(false, true);
            bool movedAny = false;

            for (int i = playerSlots.Length - 1; i >= 0; i--)
            {
                var slot = playerSlots[i];
                if (slot != null && slot.itemStack != null && slot.itemStack.itemId != -1 && slot.itemStack.itemAmount > 0)
                {
                    if (slot.itemStack.itemReference == null || slot.itemStack.itemReference.Item == null)
                    {
                        Plugin.Log.LogWarning($"[ObenseuerQualityOfLife] Ignored broken slot (ItemId: {slot.itemStack.itemId}).");
                        continue;
                    }

                    if (containerItemIds.Contains(slot.itemStack.itemId))
                    {
                        try
                        {
                            slot.MoveToForeignInventory(out int movedAmount, slot.itemStack.itemAmount);
                            if (movedAmount > 0)
                            {
                                movedAny = true;
                                slot.itemStack?.itemReference?.Item?.PlaySound();
                            }
                        }
                        catch (System.Exception ex)
                        {
                            Plugin.Log.LogError($"[ObenseuerQualityOfLife] Error moving item {slot.itemStack.itemId}: {ex.Message}");
                        }
                    }
                }
            }

            if (movedAny)
            {
                Plugin.Log.LogInfo("[ObenseuerQualityOfLife] Quick stacked items to storage!");
            }
        }
    }
}
