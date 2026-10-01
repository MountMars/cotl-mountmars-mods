using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace COTLTarotCardReceiver
{
    public class NeptunesCurseFishStorm : MonoBehaviour
    {
        private static NeptunesCurseFishStorm _instance;
        public static NeptunesCurseFishStorm Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("NeptunesCurseFishStormHost");
                    DontDestroyOnLoad(host);
                    _instance = host.AddComponent<NeptunesCurseFishStorm>();
                }
                return _instance;
            }
        }

        private static float _lastTriggerTime = -10f;
        private Canvas _canvas;
        private RectTransform _canvasRect;
        private CanvasGroup _canvasGroup;

        // Cached sprites and fallback icons
        private static readonly List<Sprite> CachedFishSprites = new List<Sprite>();
        private static bool _spritesCached = false;

        private static readonly string[] TmpFishSpriteTags = new string[]
        {
            "<sprite name=\"icon_Fish\">",
            "<sprite name=\"icon_FishSmall\">",
            "<sprite name=\"icon_FishBig\">",
            "<sprite name=\"icon_FishBlowfish\">",
            "<sprite name=\"icon_FishSwordfish\">",
            "<sprite name=\"icon_FishOctopus\">",
            "<sprite name=\"icon_FishSquid\">",
            "<sprite name=\"icon_FishCrab\">",
            "<sprite name=\"icon_FishLobster\">"
        };

        private static readonly string[] FishEmojis = new string[]
        {
            "🐟", "🐠", "🐡", "🦈", "🐙", "🦑", "🦀", "🦞"
        };

        private static TMP_FontAsset _cachedFont;

        public static void Trigger()
        {
            // Deduplicate within 2.5 seconds
            if (Time.unscaledTime - _lastTriggerTime < 2.5f)
            {
                TarotCardReceiverPlugin.Log?.LogInfo("[NeptunesCurseFishStorm] Ignored trigger within 2.5s cooldown.");
                return;
            }

            _lastTriggerTime = Time.unscaledTime;
            TarotCardReceiverPlugin.Log?.LogInfo("[NeptunesCurseFishStorm] Triggering Neptune's Curse Fish Storm screen animation!");

            Instance.StartStormCoroutine();
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void EnsureCanvas()
        {
            if (_canvas != null && _canvas.gameObject != null) return;

            var canvasObj = new GameObject("NeptuneFishStormCanvas");
            canvasObj.transform.SetParent(transform, false);

            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32760; // Topmost overlay on the monitor

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGroup = canvasObj.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false; // Never block player input!
            _canvasGroup.interactable = false;

            _canvasRect = canvasObj.GetComponent<RectTransform>();
        }

        private static void CacheFishSprites()
        {
            if (_spritesCached && CachedFishSprites.Count > 0) return;
            _spritesCached = true;
            CachedFishSprites.Clear();

            try
            {
                // 1. Try extracting through InventoryIconMapping
                var mappings = Resources.FindObjectsOfTypeAll<Lamb.UI.Assets.InventoryIconMapping>();
                if (mappings != null && mappings.Length > 0)
                {
                    var m = mappings[0];
                    InventoryItem.ITEM_TYPE[] fishTypes = new InventoryItem.ITEM_TYPE[]
                    {
                        InventoryItem.ITEM_TYPE.FISH,
                        InventoryItem.ITEM_TYPE.FISH_BIG,
                        InventoryItem.ITEM_TYPE.FISH_SMALL,
                        InventoryItem.ITEM_TYPE.FISH_BLOWFISH,
                        InventoryItem.ITEM_TYPE.FISH_SWORDFISH,
                        InventoryItem.ITEM_TYPE.FISH_OCTOPUS,
                        InventoryItem.ITEM_TYPE.FISH_SQUID,
                        InventoryItem.ITEM_TYPE.FISH_CRAB,
                        InventoryItem.ITEM_TYPE.FISH_LOBSTER,
                        InventoryItem.ITEM_TYPE.FISH_COD,
                        InventoryItem.ITEM_TYPE.FISH_PIKE,
                        InventoryItem.ITEM_TYPE.FISH_CATFISH
                    };

                    foreach (var type in fishTypes)
                    {
                        try
                        {
                            var spr = m.GetImage(type);
                            if (spr != null && !CachedFishSprites.Contains(spr))
                            {
                                CachedFishSprites.Add(spr);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                TarotCardReceiverPlugin.Log?.LogWarning($"[NeptunesCurseFishStorm] Note searching InventoryIconMapping: {ex.Message}");
            }

            try
            {
                // 2. Fallback: Search all loaded sprites for fish names
                if (CachedFishSprites.Count < 3)
                {
                    var allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
                    foreach (var s in allSprites)
                    {
                        if (s == null) continue;
                        string sname = s.name.ToLowerInvariant();
                        if (sname.StartsWith("icon_fish") || sname.StartsWith("fish_") || sname.Contains("blowfish") || sname.Contains("swordfish"))
                        {
                            if (!CachedFishSprites.Contains(s))
                            {
                                CachedFishSprites.Add(s);
                            }
                        }
                    }
                }
            }
            catch { }

            TarotCardReceiverPlugin.Log?.LogInfo($"[NeptunesCurseFishStorm] Cached {CachedFishSprites.Count} native fish sprites.");
        }

        private static TMP_FontAsset GetFont()
        {
            if (_cachedFont != null) return _cachedFont;
            try
            {
                var tmp = FindObjectOfType<TextMeshProUGUI>();
                if (tmp != null && tmp.font != null)
                {
                    _cachedFont = tmp.font;
                    return _cachedFont;
                }

                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (fonts != null && fonts.Length > 0)
                {
                    _cachedFont = fonts[0];
                    return _cachedFont;
                }
            }
            catch { }
            return null;
        }

        public void StartStormCoroutine()
        {
            StartCoroutine(FishStormRoutine());
        }

        private IEnumerator FishStormRoutine()
        {
            EnsureCanvas();
            CacheFishSprites();

            // Play celebratory sound if available
            try
            {
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayOneShot("event:/tarot/tarot_card_pull");
                }
            }
            catch { }

            // Parent container for this storm instance
            var stormContainer = new GameObject("ActiveFishStorm");
            stormContainer.transform.SetParent(_canvasRect, false);
            var stormRect = stormContainer.AddComponent<RectTransform>();
            stormRect.anchorMin = Vector2.zero;
            stormRect.anchorMax = Vector2.one;
            stormRect.offsetMin = Vector2.zero;
            stormRect.offsetMax = Vector2.zero;

            // 1. Spawn Oceanic Banner
            StartCoroutine(OceanicBannerRoutine(stormContainer.transform));

            // 2. Spawn Rising Bubbles
            for (int b = 0; b < 25; b++)
            {
                StartCoroutine(BubbleRoutine(stormContainer.transform, UnityEngine.Random.Range(0f, 1.5f)));
            }

            // 3. Staggered waves of fish
            int totalFish = 65;
            for (int i = 0; i < totalFish; i++)
            {
                int pattern = UnityEngine.Random.Range(0, 3); // 0 = Geyser Fountain, 1 = Horizontal Swimmer, 2 = Falling Flopper
                switch (pattern)
                {
                    case 0:
                        StartCoroutine(GeyserFishRoutine(stormContainer.transform));
                        break;
                    case 1:
                        StartCoroutine(SwimmerFishRoutine(stormContainer.transform));
                        break;
                    case 2:
                        StartCoroutine(FallingFishRoutine(stormContainer.transform));
                        break;
                }

                // Stagger spawns across 2.5 seconds
                yield return new WaitForSecondsRealtime(UnityEngine.Random.Range(0.02f, 0.06f));
            }

            // Wait for all fish to finish animating
            yield return new WaitForSecondsRealtime(4.5f);

            // Clean destruction
            if (stormContainer != null)
            {
                Destroy(stormContainer);
            }
        }

        // --- Visual Element Setup Helpers ---

        private GameObject CreateFishVisual(Transform parent, out RectTransform rect)
        {
            var fishGo = new GameObject("FishVisual");
            fishGo.transform.SetParent(parent, false);
            rect = fishGo.AddComponent<RectTransform>();

            // Prefer high-res Unity Sprite if cached
            if (CachedFishSprites.Count > 0)
            {
                var img = fishGo.AddComponent<Image>();
                img.sprite = CachedFishSprites[UnityEngine.Random.Range(0, CachedFishSprites.Count)];
                img.preserveAspect = true;
                img.raycastTarget = false;
                rect.sizeDelta = new Vector2(90f, 90f);
            }
            else
            {
                // Fallback to TextMeshPro with game sprite tag or emoji
                var tmp = fishGo.AddComponent<TextMeshProUGUI>();
                var font = GetFont();
                if (font != null) tmp.font = font;

                string fishTag = UnityEngine.Random.value < 0.65f
                    ? TmpFishSpriteTags[UnityEngine.Random.Range(0, TmpFishSpriteTags.Length)]
                    : FishEmojis[UnityEngine.Random.Range(0, FishEmojis.Length)];

                tmp.text = fishTag;
                tmp.fontSize = 80;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                rect.sizeDelta = new Vector2(100f, 100f);
            }

            return fishGo;
        }

        // --- Animation Coroutines ---

        /// <summary>
        /// Geyser / Fountain: Fish bursts upward from the bottom in a parabolic arc with flopping rotation.
        /// </summary>
        private IEnumerator GeyserFishRoutine(Transform parent)
        {
            var fish = CreateFishVisual(parent, out var rect);
            float startX = UnityEngine.Random.Range(-800f, 800f);
            float startY = -580f; // Just below screen

            rect.anchoredPosition = new Vector2(startX, startY);

            float vx = UnityEngine.Random.Range(-350f, 350f);
            float vy = UnityEngine.Random.Range(950f, 1550f);
            float gravity = UnityEngine.Random.Range(1700f, 2200f);
            float rotSpeed = UnityEngine.Random.Range(-360f, 360f);
            float currentRot = UnityEngine.Random.Range(0f, 360f);

            float baseScale = UnityEngine.Random.Range(0.85f, 1.45f);
            rect.localScale = Vector3.one * baseScale;

            float curX = startX;
            float curY = startY;

            while (curY > -650f || vy > 0f)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.1f) dt = 0.016f;

                curX += vx * dt;
                curY += vy * dt;
                vy -= gravity * dt;

                currentRot += rotSpeed * dt;

                // Flopping squish on scale
                float flop = 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.25f;
                rect.localScale = new Vector3(baseScale * Mathf.Sign(vx == 0 ? 1 : vx) * flop, baseScale * (2f - flop), 1f);

                rect.anchoredPosition = new Vector2(curX, curY);
                rect.localRotation = Quaternion.Euler(0f, 0f, currentRot);

                yield return null;
            }

            Destroy(fish);
        }

        /// <summary>
        /// Aquarium Swimmer: Schools of fish swimming horizontally with sinusoidal tail wiggles.
        /// </summary>
        private IEnumerator SwimmerFishRoutine(Transform parent)
        {
            var fish = CreateFishVisual(parent, out var rect);
            bool swimRight = UnityEngine.Random.value > 0.5f;

            float startX = swimRight ? -1050f : 1050f;
            float targetX = swimRight ? 1050f : -1050f;
            float baseY = UnityEngine.Random.Range(-400f, 420f);

            float speed = UnityEngine.Random.Range(650f, 1150f);
            float waveFreq = UnityEngine.Random.Range(4f, 8f);
            float waveAmp = UnityEngine.Random.Range(25f, 65f);
            float phaseOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            float baseScale = UnityEngine.Random.Range(0.75f, 1.35f);
            float facingDir = swimRight ? 1f : -1f;

            float curX = startX;
            float elapsed = 0f;

            while (swimRight ? (curX < targetX) : (curX > targetX))
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.1f) dt = 0.016f;
                elapsed += dt;

                curX += (swimRight ? speed : -speed) * dt;
                float curY = baseY + Mathf.Sin(elapsed * waveFreq + phaseOffset) * waveAmp;

                // Tail wiggle and facing direction
                float wiggle = 1f + Mathf.Sin(elapsed * 22f) * 0.2f;
                rect.localScale = new Vector3(baseScale * facingDir * wiggle, baseScale, 1f);

                // Slight pitching angle based on sine wave slope
                float pitch = Mathf.Cos(elapsed * waveFreq + phaseOffset) * 15f * facingDir;
                rect.localRotation = Quaternion.Euler(0f, 0f, pitch);

                rect.anchoredPosition = new Vector2(curX, curY);

                yield return null;
            }

            Destroy(fish);
        }

        /// <summary>
        /// Falling Flopper: Fish raining down from the top edge, tumbling and thrashing.
        /// </summary>
        private IEnumerator FallingFishRoutine(Transform parent)
        {
            var fish = CreateFishVisual(parent, out var rect);
            float startX = UnityEngine.Random.Range(-850f, 850f);
            float startY = 600f; // Just above screen

            rect.anchoredPosition = new Vector2(startX, startY);

            float vx = UnityEngine.Random.Range(-120f, 120f);
            float fallSpeed = UnityEngine.Random.Range(500f, 850f);
            float tumbleSpeed = UnityEngine.Random.Range(-280f, 280f);
            float curRot = UnityEngine.Random.Range(0f, 360f);

            float baseScale = UnityEngine.Random.Range(0.8f, 1.3f);
            float curX = startX;
            float curY = startY;

            while (curY > -600f)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.1f) dt = 0.016f;

                curX += vx * dt;
                curY -= fallSpeed * dt;
                curRot += tumbleSpeed * dt;

                float thrash = 1f + Mathf.Sin(Time.unscaledTime * 20f) * 0.3f;
                rect.localScale = new Vector3(baseScale * thrash, baseScale / thrash, 1f);

                rect.anchoredPosition = new Vector2(curX, curY);
                rect.localRotation = Quaternion.Euler(0f, 0f, curRot);

                yield return null;
            }

            Destroy(fish);
        }

        /// <summary>
        /// Rising water bubbles with gentle horizontal swaying.
        /// </summary>
        private IEnumerator BubbleRoutine(Transform parent, float delay)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            var bubbleGo = new GameObject("Bubble");
            bubbleGo.transform.SetParent(parent, false);
            var rect = bubbleGo.AddComponent<RectTransform>();

            var tmp = bubbleGo.AddComponent<TextMeshProUGUI>();
            var font = GetFont();
            if (font != null) tmp.font = font;

            tmp.text = UnityEngine.Random.value < 0.5f ? "🫧" : "○";
            tmp.fontSize = UnityEngine.Random.Range(24, 48);
            tmp.color = new Color(0.4f, 0.85f, 1f, UnityEngine.Random.Range(0.4f, 0.8f));
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            float startX = UnityEngine.Random.Range(-900f, 900f);
            float curY = -580f;
            float riseSpeed = UnityEngine.Random.Range(200f, 450f);
            float swayFreq = UnityEngine.Random.Range(2f, 4f);
            float swayAmp = UnityEngine.Random.Range(15f, 35f);
            float phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            float elapsed = 0f;

            while (curY < 600f)
            {
                float dt = Time.unscaledDeltaTime;
                if (dt > 0.1f) dt = 0.016f;
                elapsed += dt;

                curY += riseSpeed * dt;
                float curX = startX + Mathf.Sin(elapsed * swayFreq + phase) * swayAmp;

                rect.anchoredPosition = new Vector2(curX, curY);
                yield return null;
            }

            Destroy(bubbleGo);
        }

        /// <summary>
        /// Oceanic banner text at top of screen announcing Neptune's Curse.
        /// </summary>
        private IEnumerator OceanicBannerRoutine(Transform parent)
        {
            var bannerGo = new GameObject("OceanicBanner");
            bannerGo.transform.SetParent(parent, false);
            var rect = bannerGo.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(0f, 380f);
            rect.sizeDelta = new Vector2(1000f, 200f);

            var cg = bannerGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var titleTmp = bannerGo.AddComponent<TextMeshProUGUI>();
            var font = GetFont();
            if (font != null) titleTmp.font = font;

            titleTmp.text = "<color=#38EF7D>~ 🌊</color> <color=#11998E>NEPTUNE'S CURSE</color> <color=#38EF7D>🐟 ~</color>\n<size=55%><color=#E0F2FE>THE DEPTHS OVERFLOW!</color></size>";
            titleTmp.fontSize = 62;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            // Fade in and punch scale
            float elapsed = 0f;
            while (elapsed < 0.4f)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / 0.4f;
                cg.alpha = Mathf.Lerp(0f, 1f, t);
                float scale = Mathf.Lerp(0.5f, 1.1f, t);
                rect.localScale = Vector3.one * scale;
                yield return null;
            }

            rect.localScale = Vector3.one;

            // Floating wave bob
            float stayDuration = 2.8f;
            elapsed = 0f;
            while (elapsed < stayDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float bob = Mathf.Sin(elapsed * 4f) * 12f;
                rect.anchoredPosition = new Vector2(0f, 380f + bob);
                yield return null;
            }

            // Fade out
            elapsed = 0f;
            while (elapsed < 0.6f)
            {
                elapsed += Time.unscaledDeltaTime;
                cg.alpha = Mathf.Lerp(1f, 0f, elapsed / 0.6f);
                yield return null;
            }

            Destroy(bannerGo);
        }
    }
}
