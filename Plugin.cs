using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;

namespace ObenseuerQualityOfLife
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.Pokka.ObenseuerQualityOfLife";
        public const string PLUGIN_NAME = "ObenseuerQualityOfLife";
        public const string PLUGIN_VERSION = "1.0.12";
        
        public static bool showHighlight = false;
        public static bool showShopsList = false;
        public static BepInEx.Logging.ManualLogSource Log;
        public static Plugin Instance;

        public static ConfigEntry<float> MinGreenPrice;
        public static ConfigEntry<float> MinBluePrice;
        public static ConfigEntry<float> MinRedPrice;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            
            MinGreenPrice = Config.Bind("Prices", "MinGreenPrice", 150f, "Minimum price for green color");
            MinBluePrice = Config.Bind("Prices", "MinBluePrice", 500f, "Minimum price for blue color");
            MinRedPrice = Config.Bind("Prices", "MinRedPrice", 1000f, "Minimum price for red color");

            Logger.LogInfo($"Plugin {PLUGIN_GUID} is loaded!");
            Harmony.CreateAndPatchAll(typeof(Patches));
            Harmony.CreateAndPatchAll(typeof(TooltipUIPatches));
            Harmony.CreateAndPatchAll(typeof(ToolTipPatches));
            Harmony.CreateAndPatchAll(typeof(StoragePanelUIPatches));
            Harmony.CreateAndPatchAll(typeof(FurnitureShopPanelUIPatches));
            Harmony.CreateAndPatchAll(typeof(ToolTip_ConstructDataString_Patch));
            Harmony.CreateAndPatchAll(typeof(ItemData_Click_Patch));
            Harmony.CreateAndPatchAll(typeof(CategoryTitle_Init_Patch));
            Harmony.CreateAndPatchAll(typeof(MouseLook_Patch));

            GameObject menuObj = new GameObject("ObenseuerQualityOfLifeMenu");
            menuObj.AddComponent<MenuComponent>();
            DontDestroyOnLoad(menuObj);

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private bool _watcherAdded = false;

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene Scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            Patches.ScanForShops();
            Patches.ScanForScheduledDoors();
            _watcherAdded = false;
        }

        public static bool showTechnicalTooltip = false;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                showHighlight = !showHighlight;
                
                // Моментально обновляем все активные слоты в инвентаре
                if (Inventory.instance != null && Inventory.instance.inventoryPanelUI != null)
                {
                    ItemData[] allItems = Inventory.instance.inventoryPanelUI.transform.root.GetComponentsInChildren<ItemData>(false);
                    foreach (ItemData item in allItems)
                    {
                        if (item.gameObject.activeInHierarchy)
                        {
                            item.UpdateItem();
                        }
                    }
                }
            }

            if (Input.GetKeyDown(KeyCode.F3))
            {
                showShopsList = !showShopsList;
                
                if (ShowTime.instance != null)
                {
                    ShowTime.instance.UpdateTime();
                }
            }

            if (!_watcherAdded && Inventory.instance != null && Inventory.instance.inventoryPanelUI != null)
            {
                if (Inventory.instance.inventoryPanelUI.GetComponent<InventoryWatcher>() == null)
                {
                    Inventory.instance.inventoryPanelUI.gameObject.AddComponent<InventoryWatcher>();
                }
                _watcherAdded = true;
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
                    Plugin.QuickStack();
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
                        Log.LogWarning($"[ObenseuerQualityOfLife] Ignored broken slot (ItemId: {slot.itemStack.itemId}).");
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
                            Log.LogError($"[ObenseuerQualityOfLife] Error moving item {slot.itemStack.itemId}: {ex.Message}");
                        }
                    }
                }
            }
            
            if (movedAny)
            {
                Log.LogInfo("[ObenseuerQualityOfLife] Quick stacked items to storage!");
            }
        }
    }
}
