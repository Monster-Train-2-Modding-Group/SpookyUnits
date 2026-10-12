using BepInEx;
using BepInEx.Logging;
using Conductor.Data.Registers;
using Conductor.Interfaces;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json;
using RotaryHeart.Lib.SerializableDictionary;
using TrainworksReloaded.Base;
using TrainworksReloaded.Base.Extensions;
using TrainworksReloaded.Base.Prefab;
using TrainworksReloaded.Core;
using TrainworksReloaded.Core.Extensions;
using TrainworksReloaded.Core.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpookyUnits.Plugin
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger = new(MyPluginInfo.PLUGIN_GUID);
        public static GameObject? hiddenRoot;
        public static Dictionary<string, GameObject> gameObjects = [];
        /// <summary>
        /// Out of run seed. Generated at startup.
        /// </summary>
        public static int Seed;
        public struct PumpkinInfo
        {
            public int tickets;
            public List<string> pumpkins;
        }

        public static Dictionary<CollectableRarity, PumpkinInfo> pumpkinInfo = new()
        {
            {
                CollectableRarity.Common,
                new PumpkinInfo()
                {
                    tickets = 32768,
                    pumpkins = [
                        "Pumpkin1",
                        "Pumpkin5",
                        "Pumpkin6",
                        "Pumpkin8",
                        "Pumpkin9",
                        "Pumpkin10",
                        "Pumpkin11",
                        "Pumpkin12",
                        "Pumpkin17",
                        "Pumpkin18",
                        "Pumpkin19",
                        "Pumpkin20",
                        "Pumpkin21",
                        "Pumpkin22",
                        "Pumpkin23",
                        "Pumpkin24",
                        "Pumpkin25",
                        "Pumpkin35",
                        "Pumpkin36",
                        "Pumpkin37",
                        "Pumpkin38",
                        "Pumpkin39",
                        "Pumpkin40",
                        "Pumpkin41",
                        "Pumpkin42",
                        "Pumpkin43",
                        "Pumpkin51",
                        "Pumpkin52",
                    ]
                }
            },
            {
                CollectableRarity.Uncommon,
                new PumpkinInfo
                {
                    tickets = 19660,
                    pumpkins = [
                        "Pumpkin7",
                        "Pumpkin2",
                        "Pumpkin3",
                        "Pumpkin4",
                        "Pumpkin44",
                        "Pumpkin45",
                        "Pumpkin46",
                        "Pumpkin47",
                        "Pumpkin48",
                        "Pumpkin49",
                        "Pumpkin50",
                        "Pumpkin53",
                        "Pumpkin55",
                        "Pumpkin56",
                        "Pumpkin57",
                        "Pumpkin58",
                        "Pumpkin60",
                        "Pumpkin61",
                        "Pumpkin64",
                        "Pumpkin68",
                        "Pumpkin70",
                    ]
                }
            },
            {
                CollectableRarity.Rare,
                new PumpkinInfo
                {
                    tickets = 9830,
                    pumpkins = [
                        "Pumpkin13",
                        "Pumpkin14",
                        "Pumpkin15",
                        "Pumpkin16",
                        // golds
                        "Pumpkin26",
                        "Pumpkin27",
                        "Pumpkin28",
                        
                        "Pumpkin54",
                        "Pumpkin59",

                        "Pumpkin62",
                        "Pumpkin63",
                        "Pumpkin66",
                        "Pumpkin67",
                        "Pumpkin69",
                        

                    ]
                }
            },
            {
                CollectableRarity.Champion,
                new PumpkinInfo
                {
                    tickets = 3278,
                    pumpkins = [
                        "Pumpkin29",
                        "Pumpkin30",
                        "Pumpkin31",
                        "Pumpkin32",
                        "Pumpkin33",
                        "Pumpkin34",

                        "Pumpkin65"
                    ]
                }
            }
        };

        [Serializable]
        public class PumpkinUsageInfo
        {
            [JsonProperty("id")]
            public string Id { get; set; } = "";
            [JsonProperty("seen")]
            public int Seen { get; set; } = 0;
            [JsonProperty("first_seen")]
            public string FirstSeen { get; set; } = "";
        }

        [Serializable]
        public class PumpkinSaveMetaData
        {
            [JsonProperty("data")]
            public SerializableDictionaryBase<string, PumpkinUsageInfo> Data { get; set; } = [];
        }

        public class PumpkinActiveRunSaveData : ISaveData
        {
            public string Key => GetType().FullName;

            public HashSet<string> charactersSeen = [];

            [Serializable]
            public struct DTO 
            {
                [JsonProperty("data")]
                public List<string> Data { get; set; }
            }

            public void Deserialize(string data)
            {
                DTO dto;
                dto = JsonConvert.DeserializeObject<DTO>(data);
                charactersSeen = [.. dto.Data];
            }

            public void Reset()
            {
                charactersSeen.Clear();
            }

            public string Serialize()
            {
                DTO dto = new()
                {
                    Data = [.. charactersSeen]
                };
                return JsonConvert.SerializeObject(dto, Formatting.Indented);
            }

            public bool ShouldSerialize() => true;

            public bool HasCharacterBeenSeenThisRun(string id)
            {
                return charactersSeen.Contains(id);
            }

            public void MarkCharacterAsSeen(string id)
            {
                charactersSeen.Add(id);
            }
        }

        public static PumpkinSaveMetaData SaveMetaData = new();
        public static PumpkinActiveRunSaveData SaveRunData = new();
        public static string SaveFilePath = string.Empty;
        public static string ModConfigDir = string.Empty;

        public void Awake()
        {
            Logger = base.Logger;

            ModConfigDir = Path.Combine(Paths.ConfigPath, MyPluginInfo.PLUGIN_GUID);
            SaveFilePath = Path.Combine(ModConfigDir, "pumpkinSave.json");
            ReadSaveData();

            SaveDataRegistry.Register(MyPluginInfo.PLUGIN_GUID, SaveRunData);

            var builder = Railhead.GetBuilder();
            builder.Configure(
                MyPluginInfo.PLUGIN_GUID,
                c =>
                {
                    c.AddMergedJsonFile(
                        "json/plugin.json"
                    );
                }
            );

            Roundhouse.WhenReady(MyPluginInfo.PLUGIN_GUID, ctx =>
            {
                hiddenRoot = new GameObject { name = "PumpkinPrefabs" };
                GameObject.DontDestroyOnLoad(hiddenRoot);
                hiddenRoot.AddComponent<TransformOnStart>();
                hiddenRoot.SetActive(false);

                Seed = Convert.ToInt32((System.DateTime.Now - System.DateTime.UnixEpoch).TotalSeconds);
                foreach (var (rarity, pumpkinData) in pumpkinInfo)
                {
                    foreach (var sprite in pumpkinData.pumpkins)
                    {
                        var item = ctx.GetSprite(sprite);
                        if (item == null) continue;
                        gameObjects[sprite] = CreateFaceObject(0, sprite, item, rarity);
                    }
                }
            });

            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            
            var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
            harmony.PatchAll();
        }

        public static void ReadSaveData()
        {
            CreateSaveDir();
            if (!File.Exists(SaveFilePath))
            {
                Logger.LogWarning($"No save data found at {SaveFilePath}");
                WriteSaveData();
                return;
            }
            var json = File.ReadAllText(SaveFilePath);
            var data = JsonConvert.DeserializeObject<PumpkinSaveMetaData>(json);
            if (data == null)
            {
                Logger.LogWarning($"Failed to read save data at {SaveFilePath}");
                data = new PumpkinSaveMetaData();
            }
            SaveMetaData = data;
        }

        public static void CreateSaveDir()
        {
            if (!Directory.Exists(ModConfigDir))
            {
                Directory.CreateDirectory(ModConfigDir);
            }
        }

        public static void WriteSaveData()
        {
            CreateSaveDir();
            string json = JsonConvert.SerializeObject(Plugin.SaveMetaData, Formatting.Indented);
            Logger.LogDebug($"Writing to {SaveFilePath}");
            File.WriteAllText(Plugin.SaveFilePath, json);
        }

        public static void LogPumpkin(string pumpkin, CharacterState character)
        {
            var id = character.GetSourceCharacterData().GetID();
            if (id == null || SaveRunData.HasCharacterBeenSeenThisRun(id))
                return;

            SaveRunData.MarkCharacterAsSeen(id);

            if (!SaveMetaData.Data.ContainsKey(pumpkin))
            {
                SaveMetaData.Data[pumpkin] = new PumpkinUsageInfo
                {
                    Id = pumpkin,
                    Seen = 1,
                    FirstSeen = character.GetSourceCharacterData().GetID()
                };
                return;
            }
            SaveMetaData.Data[pumpkin].Seen += 1;
        }

        private static GameObject CreateFaceObject(int sortingLayerID, string name, Sprite sprite, CollectableRarity rarity)
        {
            GameObject gameObject = new("Face");
            gameObject.transform.parent = hiddenRoot!.transform;
            GameObject gameObject2 = new(name);
            gameObject2.transform.SetParent(gameObject.transform);
            CharacterOverlayImage characterOverlayImage = gameObject.AddComponent<CharacterOverlayImage>();
            SpriteRenderer spriteRenderer = gameObject2.AddComponent<SpriteRenderer>();
            spriteRenderer.transform.localPosition += new Vector3(0, 0, -0.1f);
            spriteRenderer.transform.localScale = new Vector3(0.456f, 0.456f);
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingLayerID = sortingLayerID;
            spriteRenderer.sortingOrder = 1;
            characterOverlayImage.SetSpriteRenderer(spriteRenderer);
            return gameObject;
        }

        public static GameObject? GetOverlayImage(uint seed, uint hash)
        {
            // Called before we was initialized. This can happen for Setting up Prefab skeletons.
            if (gameObjects.Count == 0)  return null;
            uint value = seed * hash;

            ulong x = (ulong)seed;
            x ^= x >> 12;
            x ^= (x << 25) & (UInt64.MaxValue);
            x ^= x >> 27;
            seed = (uint)(x * 0x24F947D1);


            int ticket = (int)(value & 0xFFFF);
            var selectedRarity = CollectableRarity.Unset;
            int runningCount = 0;
            foreach (var (rarity, pumpkinData) in pumpkinInfo)
            {
                runningCount += pumpkinData.tickets;
                if (ticket < runningCount)
                {
                    selectedRarity = rarity;
                    break;
                }
            }

            // Shouldn't happen.
            if (selectedRarity == CollectableRarity.Unset)
                return null;

            var pumpkinsOfRarity = pumpkinInfo[selectedRarity];
            uint value2 = seed * hash;
            int id = (int)(value2 % pumpkinsOfRarity.pumpkins.Count);
            var selectedPumpkin = pumpkinsOfRarity.pumpkins[id];
            return GameObject.Instantiate(gameObjects[selectedPumpkin]);
        }
    }

    public class CharacterOverlayImage : MonoBehaviour
    {
        private SpriteRenderer? spriteBase;
        private int baseInitialSortingOrder;

        public void SetSpriteRenderer(SpriteRenderer spriteRenderer)
        {
            this.spriteBase = spriteRenderer;
            this.baseInitialSortingOrder = spriteBase.sortingOrder;
        }

        public void SetSortingOrder(int sortingLayer, int sortingOrderOffset)
        {
            spriteBase!.sortingLayerID = sortingLayer;
            spriteBase.sortingOrder = baseInitialSortingOrder + sortingOrderOffset;
        }
    }
}
