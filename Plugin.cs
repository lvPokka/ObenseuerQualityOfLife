using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.Mono;
using HarmonyLib;
using ObenseuerQualityOfLife.PatchList;
using UnityEngine;

namespace ObenseuerQualityOfLife
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PLUGIN_GUID = "com.Pokka.ObenseuerQualityOfLife";
        public const string PLUGIN_NAME = "ObenseuerQualityOfLife";
        public const string PLUGIN_VERSION = "1.0.13";

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
            Harmony.CreateAndPatchAll(System.Reflection.Assembly.GetExecutingAssembly(), PLUGIN_GUID);

            GameObject menuObj = new GameObject("ObenseuerQualityOfLifeMenu");
            menuObj.AddComponent<MenuComponent>();
            DontDestroyOnLoad(menuObj);

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene Scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            ShopWorktimeUI.ScanForShops();
            ShopWorktimeUI.ScanForScheduledDoors();
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

            if (Input.GetKeyDown(KeyCode.F4))
            {
                if (MenuComponent.Instance != null)
                {
                    MenuComponent.Instance.ToggleMenu();
                }
            }
        }
    }
}
