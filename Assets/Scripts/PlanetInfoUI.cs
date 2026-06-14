using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class PlanetInfoUI : MonoBehaviour
{
    private enum CelestialSection
    {
        PlanetaryProfile,
        Stats,
        OrbitCharacteristics,
        Structure,
        Atmosphere
    }

    [Header("Selection")]
    public float selectionSphereRadius = 0.35f;
    public float selectionScreenThreshold = 120f;

    public GameObject panel;
    public TMP_Text planetNameText;
    public Image celestialBodyImage;
    public TMP_Text celestialBodyNameText;
    public Image celestialProfileButtonImage;
    public Image planetaryProfileMainImage;
    public TMP_Text planetaryProfileDescriptionText;
    public ScrollRect planetaryProfileScrollRect;
    public ScrollRect statsScrollRect;
    public ScrollRect orbitCharacteristicsScrollRect;
    public ScrollRect structureScrollRect;
    public ScrollRect atmosphereScrollRect;
    public TMP_Text orbitCharacteristicsDescriptionText;
    public TMP_Text structureDescriptionText;
    public Image atmosphereMainImage;
    public TMP_Text atmosphereDescriptionText;
    public Camera mainCamera;
    public MobileOrbitCamera orbitCamera;
    public GameObject solarSystemUiRoot;
    public Button solarSystemMenuButton;
    public Button visitButton;
    public GameObject simulationControls;
    public Button focusBackButton;
    public Button celestialBodyBackButton;
    public Button previousButton;
    public Button nextButton;
    public Button celestialPreviousButton;
    public Button celestialNextButton;
    public Button targetButton;
    public Button pauseButton;
    public Slider speedSlider;
    public Button hidePanelButton;
    public Button hideUnhideButton;
    public Image hideUnhideIconImage;
    public Sprite hideIconSprite;
    public Sprite viewIconSprite;
    public Button basicsButton;
    public Button extrasButton;
    public Button soundButton;
    public MusicIconAnimator soundButtonIconAnimator;
    public Image soundButtonFill;
    public TMP_Text soundTitleText;
    public GameObject basicsGroup;
    public GameObject extrasGroup;
    public Image basicsButtonFill;
    public Image extrasButtonFill;
    public Button viewInArButton;
    public Image arPlanetIcon;
    public Button imagesButton;
    public SolarAudioManager solarAudioManager;
    public GameObject imagesGalleryOverlay;
    public ScrollRect galleryScrollRect;
    public Button galleryBackButton;
    public GameObject imageViewerOverlay;
    public Button viewerCloseButton;
    public Button viewerPreviousButton;
    public Button viewerNextButton;
    public TMP_Text viewerImageTitleText;
    public Image viewerMainImage;
    public GameObject imageCaptionPanel;
    public TMP_Text imageCaptionText;
    public ScrollRect imageCaptionScrollRect;
    public Button imageCaptionHideButton;
    public Button imageCaptionShowButton;

    [Header("Celestial Body UI")]
    public Button planetaryProfileButton;
    public Button statsButton;
    public Button orbitCharacteristicsButton;
    public Button structureButton;
    public Button atmosphereButton;
    public GameObject planetaryProfilePanel;
    public GameObject statsPanel;
    public GameObject orbitCharacteristicsPanel;
    public GameObject structurePanel;
    public GameObject atmospherePanel;
    public bool showPlanetaryProfileOnStart = true;
    public float sectionSlideDuration = 0.28f;
    public float sectionSlideOffset = 100f;
    public float profileImageZoomMultiplier = 1.2f;
    public Color navButtonInactiveColor = new Color(1f, 1f, 1f, 1f);
    public Color navButtonActiveColor = new Color(0f, 0f, 0f, 1f);
    public float navButtonInactiveScale = 1f;
    public float navButtonActiveScale = 1.08f;
    public Color statsOddRowColor = new Color(0.02f, 0.02f, 0.02f, 0.85f);
    public Color statsEvenRowColor = new Color(0.03f, 0.14f, 0.4f, 0.9f);
    public bool showSolarSystemLabels = true;
    public Vector2 solarSystemLabelScreenOffset = new Vector2(18f, 10f);
    public int solarSystemLabelFontSize = 18;
    public Color solarSystemLabelColor = new Color(0.85f, 0.9f, 1f, 1f);
    public Color activeBottomTabFillColor = Color.black;
    private CelestialBody currentBody;
    private OrbitAroundSun[] orbitAroundSunScripts;
    private OrbitAroundPlanet[] orbitAroundPlanetScripts;
    private PlanetRotation[] rotationScripts;
    private OrbitSnakeTrail[] orbitSnakeTrails;
    private Coroutine sectionAnimationCoroutine;
    private CelestialSection? activeSection;
    private RectTransform contentAreaRectTransform;
    private Vector2 planetaryProfilePanelAnchoredPosition;
    private Vector2 statsPanelAnchoredPosition;
    private Vector2 orbitCharacteristicsPanelAnchoredPosition;
    private Vector2 structurePanelAnchoredPosition;
    private Vector2 atmospherePanelAnchoredPosition;
    private readonly List<Button> hidePanelButtons = new List<Button>();
    private readonly Dictionary<Graphic, Color> originalGraphicColors = new Dictionary<Graphic, Color>();
    private readonly Dictionary<Renderer, bool> originalCelestialRendererStates = new Dictionary<Renderer, bool>();
    private readonly Dictionary<GameObject, bool> hiddenUiStateMap = new Dictionary<GameObject, bool>();
    private readonly List<Image> hideUnhideIconImages = new List<Image>();
    private readonly Dictionary<string, Sprite> runtimeBodySpriteCache = new Dictionary<string, Sprite>();
    private Color basicsButtonFillOriginalColor = Color.white;
    private Color extrasButtonFillOriginalColor = Color.white;
    private Color soundButtonFillOriginalColor = Color.white;
    private bool showBasicsTab = true;
    private static readonly string[] FocusOrder =
    {
        "Sun",
        "Mercury",
        "Venus",
        "Earth",
        "Moon",
        "Mars",
        "Jupiter",
        "Saturn",
        "Uranus",
        "Neptune"
    };
    private GameObject celestialBodyUiRoot;
    private bool isCelestialBodyUiHidden;
    private Image planetaryProfileMainImageContent;
    private Image atmosphereMainImageContent;
    private Coroutine visitRefreshCoroutine;
    private RectTransform statsContent;
    private GameObject statRowTemplate;
    private TMP_Text statRowTemplateLabelText;
    private TMP_Text statRowTemplateValueText;
    private readonly List<GameObject> generatedStatRows = new List<GameObject>();
    private RectTransform galleryContent;
    private GameObject galleryItemTemplate;
    private Image galleryItemTemplateThumbnailImage;
    private RectTransform galleryItemTemplateThumbnailRect;
    private readonly List<GameObject> generatedGalleryItems = new List<GameObject>();
    private int currentGalleryIndex = -1;
    private RectTransform solarSystemLabelsRoot;
    private Canvas solarSystemCanvas;
    private readonly Dictionary<CelestialBody, TMP_Text> solarSystemLabelMap = new Dictionary<CelestialBody, TMP_Text>();
    private GameObject solarSystemTopLeftMenuObject;
    private GameObject solarSystemTopLeftBackObject;
    private GameObject celestialBodyTopLeftBackObject;

    private void Awake()
    {
        if (panel == null)
        {
            panel = FindObjectByName("PlanetInfoCard");
        }

        if (solarSystemUiRoot == null)
        {
            solarSystemUiRoot = FindObjectByName("SolarSystemUI");
        }

        if (celestialBodyUiRoot == null)
        {
            celestialBodyUiRoot = FindObjectByName("CelestialBodyUI");
        }

        if (celestialBodyImage == null)
        {
            celestialBodyImage = FindComponentInChildrenByName<Image>(panel, "CelestialBodyImage");
        }

        if (celestialBodyNameText == null)
        {
            celestialBodyNameText = FindComponentUnderNamedParent<TMP_Text>(celestialBodyUiRoot, "TopCenterNav", "PlanetNameBar");
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (orbitCamera == null && mainCamera != null)
        {
            orbitCamera = mainCamera.GetComponent<MobileOrbitCamera>();
        }

        if (solarSystemMenuButton == null)
        {
            solarSystemMenuButton = FindComponentUnderNamedParent<Button>(solarSystemUiRoot, "TopLeftGroup", "MenuButton");
        }

        if (solarSystemTopLeftMenuObject == null)
        {
            solarSystemTopLeftMenuObject = solarSystemMenuButton != null
                ? solarSystemMenuButton.gameObject
                : FindChildObjectByName(FindChildObjectByName(solarSystemUiRoot, "TopLeftGroup"), "MenuButton");
        }

        Button preferredFocusBackButton = FindComponentUnderNamedParent<Button>(solarSystemUiRoot, "TopLeftGroup", "BackButton");
        if (preferredFocusBackButton != null)
        {
            focusBackButton = preferredFocusBackButton;
        }
        else if (focusBackButton == null)
        {
            focusBackButton = FindButtonByName("BackButton");
        }

        if (solarSystemTopLeftBackObject == null)
        {
            solarSystemTopLeftBackObject = focusBackButton != null
                ? focusBackButton.gameObject
                : FindChildObjectByName(FindChildObjectByName(solarSystemUiRoot, "TopLeftGroup"), "BackButton");
        }

        Button preferredCelestialBodyBackButton = FindComponentUnderNamedParent<Button>(celestialBodyUiRoot, "TopLeftControls", "BackButton");
        if (preferredCelestialBodyBackButton != null)
        {
            celestialBodyBackButton = preferredCelestialBodyBackButton;
        }
        else if (celestialBodyBackButton == null)
        {
            celestialBodyBackButton = FindLastButtonByName("BackButton");
        }

        if (celestialBodyTopLeftBackObject == null)
        {
            celestialBodyTopLeftBackObject = celestialBodyBackButton != null
                ? celestialBodyBackButton.gameObject
                : FindChildObjectByName(FindChildObjectByName(celestialBodyUiRoot, "TopLeftControls"), "BackButton");
        }

        Button preferredVisitButton = FindComponentInChildrenByName<Button>(panel, "VisitButton");
        if (preferredVisitButton != null)
        {
            visitButton = preferredVisitButton;
        }
        else if (visitButton == null)
        {
            visitButton = FindButtonByName("VisitButton");
        }

        if (celestialPreviousButton == null)
        {
            celestialPreviousButton = FindComponentUnderNamedParent<Button>(celestialBodyUiRoot, "TopCenterNav", "PreviousButton");
        }

        if (celestialNextButton == null)
        {
            celestialNextButton = FindComponentUnderNamedParent<Button>(celestialBodyUiRoot, "TopCenterNav", "NextButton");
        }

        Button preferredPreviousButton = FindComponentInChildrenByName<Button>(panel, "PreviousButton");
        if (preferredPreviousButton != null)
        {
            previousButton = preferredPreviousButton;
        }
        else if (previousButton == null)
        {
            previousButton = FindButtonByName("PreviousButton");
        }

        Button preferredNextButton = FindComponentInChildrenByName<Button>(panel, "NextButton");
        if (preferredNextButton != null)
        {
            nextButton = preferredNextButton;
        }
        else if (nextButton == null)
        {
            nextButton = FindLastButtonByName("NextButton");
        }

        TMP_Text preferredPlanetNameText = FindComponentInChildrenByName<TMP_Text>(panel, "PlanetNameBar");
        if (preferredPlanetNameText != null)
        {
            planetNameText = preferredPlanetNameText;
        }
        else if (planetNameText == null)
        {
            planetNameText = FindTmpTextByName("PlanetName");
        }

        Button preferredTargetButton = FindOrCreateButtonFromGraphic(panel, "PlanetNameBar");
        if (preferredTargetButton != null)
        {
            targetButton = preferredTargetButton;
        }
        else if (targetButton == null)
        {
            targetButton = FindOrCreateButtonFromGraphic(panel, "TargetIcon");
        }

        if (pauseButton == null)
        {
            pauseButton = FindButtonByName("PauseButton");
        }

        if (hidePanelButton == null)
        {
            hidePanelButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "HidePanelButton");
        }

        if (hidePanelButton == null)
        {
            hidePanelButton = FindButtonByName("HidePanelButton");
        }

        if (hideUnhideButton == null)
        {
            hideUnhideButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "HideUnhideButton");
        }

        if (hideUnhideButton == null)
        {
            hideUnhideButton = FindButtonByName("HideUnhideButton");
        }

        if (basicsButton == null)
        {
            basicsButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "BasicsButton");
        }

        if (extrasButton == null)
        {
            extrasButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "ExtrasButton");
        }

        if (soundButton == null)
        {
            soundButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "SoundButton");
        }

        if (soundButtonIconAnimator == null && soundButton != null)
        {
            soundButtonIconAnimator = soundButton.GetComponentInChildren<MusicIconAnimator>(true);
        }

        if (soundButtonFill == null && soundButton != null)
        {
            soundButtonFill = FindComponentInChildrenByName<Image>(soundButton.gameObject, "Fill");
        }

        if (soundTitleText == null)
        {
            soundTitleText = FindComponentInChildrenByName<TMP_Text>(celestialBodyUiRoot, "SoundTitle");
        }

        if (basicsGroup == null)
        {
            basicsGroup = FindChildObjectByName(celestialBodyUiRoot, "Basics");
        }

        if (extrasGroup == null)
        {
            extrasGroup = FindChildObjectByName(celestialBodyUiRoot, "Extras");
        }

        if (basicsButtonFill == null && basicsButton != null)
        {
            basicsButtonFill = FindComponentInChildrenByName<Image>(basicsButton.gameObject, "Fill");

            if (basicsButtonFill == null)
            {
                basicsButtonFill = FindComponentInChildrenByName<Image>(basicsButton.gameObject, "Fill2");
            }
        }

        if (extrasButtonFill == null && extrasButton != null)
        {
            extrasButtonFill = FindComponentInChildrenByName<Image>(extrasButton.gameObject, "Fill");

            if (extrasButtonFill == null)
            {
                extrasButtonFill = FindComponentInChildrenByName<Image>(extrasButton.gameObject, "Fill2");
            }
        }

        if (viewInArButton == null)
        {
            viewInArButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "ViewInArButton");
        }

        if (arPlanetIcon == null && viewInArButton != null)
        {
            arPlanetIcon = FindComponentInChildrenByName<Image>(viewInArButton.gameObject, "ARPlanetIcon");
        }

        if (imagesButton == null)
        {
            imagesButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "ImagesButton");
        }

        hidePanelButtons.Clear();
        if (celestialBodyUiRoot != null)
        {
            hidePanelButtons.AddRange(FindComponentsInChildrenByName<Button>(celestialBodyUiRoot, "HidePanelButton"));
        }

        if (hidePanelButtons.Count == 0)
        {
            hidePanelButtons.AddRange(FindButtonsByName("HidePanelButton"));
        }

        if (planetaryProfileButton == null)
        {
            planetaryProfileButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "PlanetaryProfileButton");
        }

        if (planetaryProfileButton == null)
        {
            planetaryProfileButton = FindButtonByName("PlanetaryProfileButton");
        }

        if (celestialProfileButtonImage == null && planetaryProfileButton != null)
        {
            celestialProfileButtonImage = FindChildImage(planetaryProfileButton, "Image");
        }

        if (statsButton == null)
        {
            statsButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "StatsButton");
        }

        if (statsButton == null)
        {
            statsButton = FindButtonByName("StatsButton");
        }

        if (orbitCharacteristicsButton == null)
        {
            orbitCharacteristicsButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "OrbitCharacteristicsButton");
        }

        if (orbitCharacteristicsButton == null)
        {
            orbitCharacteristicsButton = FindButtonByName("OrbitCharacteristicsButton");
        }

        if (structureButton == null)
        {
            structureButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "StructureButton");
        }

        if (structureButton == null)
        {
            structureButton = FindButtonByName("StructureButton");
        }

        if (atmosphereButton == null)
        {
            atmosphereButton = FindComponentInChildrenByName<Button>(celestialBodyUiRoot, "AtmosphereButton");
        }

        if (atmosphereButton == null)
        {
            atmosphereButton = FindButtonByName("AtmosphereButton");
        }

        if (planetaryProfilePanel == null)
        {
            planetaryProfilePanel = FindChildObjectByName(celestialBodyUiRoot, "PlanetaryProfile");
        }

        if (planetaryProfilePanel == null)
        {
            planetaryProfilePanel = FindObjectByName("PlanetaryProfile");
        }

        if (planetaryProfileMainImage == null)
        {
            planetaryProfileMainImage = FindComponentInChildrenByName<Image>(planetaryProfilePanel, "MainImage");
        }

        if (planetaryProfileDescriptionText == null)
        {
            planetaryProfileDescriptionText = FindComponentInChildrenByName<TMP_Text>(planetaryProfilePanel, "DescriptionText");
        }

        EnsurePlanetaryProfileCoverImage();

        if (planetaryProfileScrollRect == null)
        {
            planetaryProfileScrollRect = FindComponentInChildrenByName<ScrollRect>(planetaryProfilePanel, "DescriptionScrollView");
        }

        if (statsPanel == null)
        {
            statsPanel = FindChildObjectByName(celestialBodyUiRoot, "Stats");
        }

        if (statsPanel == null)
        {
            statsPanel = FindObjectByName("Stats");
        }

        if (statsScrollRect == null)
        {
            statsScrollRect = FindComponentInChildrenByName<ScrollRect>(statsPanel, "StatsScrollView");
        }

        if (statsContent == null && statsScrollRect != null)
        {
            statsContent = statsScrollRect.content;
        }

        if (statRowTemplate == null)
        {
            statRowTemplate = FindChildObjectByName(statsPanel, "StatRowTemplate");
        }

        if (statRowTemplate != null)
        {
            statRowTemplateLabelText = FindComponentInChildrenByName<TMP_Text>(statRowTemplate, "LabelText");
            statRowTemplateValueText = FindComponentInChildrenByName<TMP_Text>(statRowTemplate, "ValueText");
            statRowTemplate.SetActive(false);
        }

        if (orbitCharacteristicsPanel == null)
        {
            orbitCharacteristicsPanel = FindChildObjectByName(celestialBodyUiRoot, "OrbitCharacteristics");
        }

        if (orbitCharacteristicsPanel == null)
        {
            orbitCharacteristicsPanel = FindObjectByName("OrbitCharacteristics");
        }

        if (orbitCharacteristicsScrollRect == null)
        {
            orbitCharacteristicsScrollRect = FindComponentInChildrenByName<ScrollRect>(orbitCharacteristicsPanel, "OrbitDescriptionScrollView");
        }

        if (orbitCharacteristicsDescriptionText == null)
        {
            orbitCharacteristicsDescriptionText = FindComponentInChildrenByName<TMP_Text>(orbitCharacteristicsPanel, "DescriptionText");
        }

        if (structurePanel == null)
        {
            structurePanel = FindChildObjectByName(celestialBodyUiRoot, "SolidCore");
        }

        if (structurePanel == null)
        {
            structurePanel = FindObjectByName("SolidCore");
        }

        if (structureScrollRect == null)
        {
            structureScrollRect = FindComponentInChildrenByName<ScrollRect>(structurePanel, "CoreDescriptionScrollView");
        }

        if (structureDescriptionText == null)
        {
            structureDescriptionText = FindComponentInChildrenByName<TMP_Text>(structurePanel, "DescriptionText");
        }

        if (atmospherePanel == null)
        {
            atmospherePanel = FindChildObjectByName(celestialBodyUiRoot, "Atmosphere");
        }

        if (atmospherePanel == null)
        {
            atmospherePanel = FindObjectByName("Atmosphere");
        }

        if (atmosphereScrollRect == null)
        {
            atmosphereScrollRect = FindComponentInChildrenByName<ScrollRect>(atmospherePanel, "AtmosphereDescriptionScrollView");
        }

        if (atmosphereMainImage == null)
        {
            atmosphereMainImage = FindComponentInChildrenByName<Image>(atmospherePanel, "MainImage");
        }

        if (atmosphereDescriptionText == null)
        {
            atmosphereDescriptionText = FindComponentInChildrenByName<TMP_Text>(atmospherePanel, "DescriptionText");
        }

        EnsureAtmosphereCoverImage();

        if (imagesGalleryOverlay == null)
        {
            imagesGalleryOverlay = FindChildObjectByName(celestialBodyUiRoot, "ImagesGalleryOverlay");
        }

        if (galleryScrollRect == null && imagesGalleryOverlay != null)
        {
            galleryScrollRect = FindComponentInChildrenByName<ScrollRect>(imagesGalleryOverlay, "GalleryScrollView");
        }

        if (galleryContent == null && galleryScrollRect != null)
        {
            galleryContent = galleryScrollRect.content;
        }

        if (galleryItemTemplate == null && imagesGalleryOverlay != null)
        {
            galleryItemTemplate = FindChildObjectByName(imagesGalleryOverlay, "GalleryItemTemplate");
        }

        if (galleryItemTemplate != null)
        {
            galleryItemTemplateThumbnailImage = FindComponentInChildrenByName<Image>(galleryItemTemplate, "ThumbnailImage");
            galleryItemTemplateThumbnailRect = galleryItemTemplateThumbnailImage != null ? galleryItemTemplateThumbnailImage.rectTransform : null;
            EnsureGalleryItemTemplateCoverImage();
            galleryItemTemplate.SetActive(false);
        }

        if (galleryBackButton == null && imagesGalleryOverlay != null)
        {
            galleryBackButton = FindComponentInChildrenByName<Button>(imagesGalleryOverlay, "GalleryBackButton");

            if (galleryBackButton == null)
            {
                galleryBackButton = FindComponentInChildrenByName<Button>(imagesGalleryOverlay, "BackButton");
            }
        }

        if (imageViewerOverlay == null)
        {
            imageViewerOverlay = FindChildObjectByName(celestialBodyUiRoot, "ImageViewerOverlay");
        }

        if (viewerCloseButton == null && imageViewerOverlay != null)
        {
            viewerCloseButton = FindComponentInChildrenByName<Button>(imageViewerOverlay, "ViewerCloseButton");
        }

        if (viewerPreviousButton == null && imageViewerOverlay != null)
        {
            viewerPreviousButton = FindComponentInChildrenByName<Button>(imageViewerOverlay, "ViewerPreviousButton");
        }

        if (viewerNextButton == null && imageViewerOverlay != null)
        {
            viewerNextButton = FindComponentInChildrenByName<Button>(imageViewerOverlay, "ViewerNextButton");
        }

        if (viewerImageTitleText == null && imageViewerOverlay != null)
        {
            viewerImageTitleText = FindComponentInChildrenByName<TMP_Text>(imageViewerOverlay, "ViewerImageTitle");
        }

        if (viewerMainImage == null && imageViewerOverlay != null)
        {
            viewerMainImage = FindComponentInChildrenByName<Image>(imageViewerOverlay, "ViewerMainImage");
        }

        if (imageCaptionText == null && imageViewerOverlay != null)
        {
            imageCaptionText = FindComponentInChildrenByName<TMP_Text>(imageViewerOverlay, "ImageCaptionText");
        }

        if (imageCaptionPanel == null && imageViewerOverlay != null)
        {
            imageCaptionPanel = FindChildObjectByName(imageViewerOverlay, "ImageCaptionPanel");
        }

        if (imageCaptionScrollRect == null && imageViewerOverlay != null)
        {
            imageCaptionScrollRect = FindComponentInChildrenByName<ScrollRect>(imageViewerOverlay, "ImageCaptionScrollView");
        }

        if (imageCaptionHideButton == null && imageViewerOverlay != null)
        {
            imageCaptionHideButton = FindComponentInChildrenByName<Button>(imageViewerOverlay, "ImageCaptionHideButton");
        }

        if (imageCaptionShowButton == null && imageViewerOverlay != null)
        {
            imageCaptionShowButton = FindComponentInChildrenByName<Button>(imageViewerOverlay, "ImageCaptionShowButton");
        }

        if (contentAreaRectTransform == null)
        {
            GameObject contentAreaObject = FindChildObjectByName(celestialBodyUiRoot, "ContentArea");

            if (contentAreaObject == null)
            {
                contentAreaObject = FindObjectByName("ContentArea");
            }

            if (contentAreaObject != null)
            {
                contentAreaRectTransform = contentAreaObject.GetComponent<RectTransform>();
            }
        }

        if (hideUnhideIconImage == null && hideUnhideButton != null)
        {
            hideUnhideIconImage = FindChildImage(hideUnhideButton, "Icon");
        }

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        if (soundButtonIconAnimator != null)
        {
            soundButtonIconAnimator.Stop();
        }

        UpdateSoundTitle(false);

        if (solarAudioManager == null)
        {
            solarAudioManager = FindObjectOfType<SolarAudioManager>();
        }

        SetImageCaptionPanelVisible(true);

        if (imageViewerOverlay != null)
        {
            hidePanelButtons.RemoveAll(button => button != null && button.transform.IsChildOf(imageViewerOverlay.transform));
        }

        if (hideUnhideIconImage == null && hideUnhideButton != null)
        {
            hideUnhideIconImage = FindFirstChildImage(hideUnhideButton);
        }

        if (hideIconSprite == null && hideUnhideIconImage != null)
        {
            hideIconSprite = hideUnhideIconImage.sprite;
        }

        if (viewIconSprite == null)
        {
            viewIconSprite = FindSpriteByName("view");
        }

        EnsureHideUnhideSpritesLoaded();
        CacheHideUnhideIconImages();
        CacheBottomTabVisualDefaults();

        CacheSectionAnchoredPositions();
        InitializeSolarSystemLabels();

        if (speedSlider == null)
        {
            speedSlider = FindFirstObjectByType<Slider>();
        }

        if (simulationControls == null && pauseButton != null)
        {
            simulationControls = pauseButton.transform.parent != null ? pauseButton.transform.parent.gameObject : null;
        }

        CacheNavigationVisualDefaults();
        UpdateBottomTabVisuals();
    }

    private void Start()
    {
        orbitAroundSunScripts = FindObjectsByType<OrbitAroundSun>(FindObjectsSortMode.None);
        orbitAroundPlanetScripts = FindObjectsByType<OrbitAroundPlanet>(FindObjectsSortMode.None);
        rotationScripts = FindObjectsByType<PlanetRotation>(FindObjectsSortMode.None);
        orbitSnakeTrails = FindObjectsByType<OrbitSnakeTrail>(FindObjectsSortMode.None);

        if (panel != null)
        {
            panel.SetActive(false);
        }

        if (focusBackButton != null)
        {
            focusBackButton.onClick.AddListener(HandleFocusBackButton);
        }

        if (celestialBodyBackButton != null)
        {
            celestialBodyBackButton.onClick.AddListener(HandleCelestialBodyBackButton);
        }

        if (visitButton != null)
        {
            visitButton.onClick.AddListener(HandleVisitButton);
        }

        if (targetButton != null)
        {
            targetButton.onClick.AddListener(HandleTargetButton);
        }

        if (previousButton != null)
        {
            previousButton.onClick.AddListener(HandlePreviousButton);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(HandleNextButton);
        }

        if (celestialPreviousButton != null)
        {
            celestialPreviousButton.onClick.AddListener(HandlePreviousButton);
        }

        if (celestialNextButton != null)
        {
            celestialNextButton.onClick.AddListener(HandleNextButton);
        }

        if (hideUnhideButton != null)
        {
            hideUnhideButton.onClick.AddListener(HandleHideUnhideButton);
        }

        if (hidePanelButton != null)
        {
            hidePanelButton.onClick.AddListener(HandleHidePanelButton);
        }

        if (basicsButton != null)
        {
            basicsButton.onClick.AddListener(HandleBasicsButton);
        }

        if (extrasButton != null)
        {
            extrasButton.onClick.AddListener(HandleExtrasButton);
        }

        if (soundButton != null)
        {
            soundButton.onClick.AddListener(HandleSoundButton);
        }

        if (imagesButton != null)
        {
            imagesButton.onClick.AddListener(HandleImagesButton);
        }

        if (galleryBackButton != null)
        {
            galleryBackButton.onClick.AddListener(HandleGalleryBackButton);
        }

        if (viewerCloseButton != null)
        {
            viewerCloseButton.onClick.AddListener(HandleViewerCloseButton);
        }

        if (viewerPreviousButton != null)
        {
            viewerPreviousButton.onClick.AddListener(HandleViewerPreviousButton);
        }

        if (viewerNextButton != null)
        {
            viewerNextButton.onClick.AddListener(HandleViewerNextButton);
        }

        if (imageCaptionHideButton != null)
        {
            imageCaptionHideButton.onClick.AddListener(HandleImageCaptionHideButton);
        }

        if (imageCaptionShowButton != null)
        {
            imageCaptionShowButton.onClick.AddListener(HandleImageCaptionShowButton);
        }

        foreach (Button button in hidePanelButtons)
        {
            if (button != null && button != hidePanelButton)
            {
                button.onClick.AddListener(HandleHidePanelButton);
            }
        }

        if (planetaryProfileButton != null)
        {
            planetaryProfileButton.onClick.AddListener(HandlePlanetaryProfileButton);
        }

        if (statsButton != null)
        {
            statsButton.onClick.AddListener(HandleStatsButton);
        }

        if (orbitCharacteristicsButton != null)
        {
            orbitCharacteristicsButton.onClick.AddListener(HandleOrbitCharacteristicsButton);
        }

        if (structureButton != null)
        {
            structureButton.onClick.AddListener(HandleStructureButton);
        }

        if (atmosphereButton != null)
        {
            atmosphereButton.onClick.AddListener(HandleAtmosphereButton);
        }

        if (showPlanetaryProfileOnStart)
        {
            SetActiveSection(CelestialSection.PlanetaryProfile);
        }
        else
        {
            UpdateNavigationButtonVisuals(null);
        }

        ShowBasicsTab(false);

        if (currentBody != null)
        {
            UpdateFocusedUi();
        }
        UpdateHideUnhideIcon();
        UpdateTopLeftNavigationButtons();
    }

    private void OnDestroy()
    {
        if (focusBackButton != null)
        {
            focusBackButton.onClick.RemoveListener(HandleFocusBackButton);
        }

        if (celestialBodyBackButton != null)
        {
            celestialBodyBackButton.onClick.RemoveListener(HandleCelestialBodyBackButton);
        }

        if (visitButton != null)
        {
            visitButton.onClick.RemoveListener(HandleVisitButton);
        }

        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(HandleTargetButton);
        }

        if (previousButton != null)
        {
            previousButton.onClick.RemoveListener(HandlePreviousButton);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(HandleNextButton);
        }

        if (celestialPreviousButton != null)
        {
            celestialPreviousButton.onClick.RemoveListener(HandlePreviousButton);
        }

        if (celestialNextButton != null)
        {
            celestialNextButton.onClick.RemoveListener(HandleNextButton);
        }

        if (hideUnhideButton != null)
        {
            hideUnhideButton.onClick.RemoveListener(HandleHideUnhideButton);
        }

        if (hidePanelButton != null)
        {
            hidePanelButton.onClick.RemoveListener(HandleHidePanelButton);
        }

        if (basicsButton != null)
        {
            basicsButton.onClick.RemoveListener(HandleBasicsButton);
        }

        if (extrasButton != null)
        {
            extrasButton.onClick.RemoveListener(HandleExtrasButton);
        }

        if (soundButton != null)
        {
            soundButton.onClick.RemoveListener(HandleSoundButton);
        }

        if (imagesButton != null)
        {
            imagesButton.onClick.RemoveListener(HandleImagesButton);
        }

        foreach (Button button in hidePanelButtons)
        {
            if (button != null && button != hidePanelButton)
            {
                button.onClick.RemoveListener(HandleHidePanelButton);
            }
        }

        if (planetaryProfileButton != null)
        {
            planetaryProfileButton.onClick.RemoveListener(HandlePlanetaryProfileButton);
        }

        if (statsButton != null)
        {
            statsButton.onClick.RemoveListener(HandleStatsButton);
        }

        if (orbitCharacteristicsButton != null)
        {
            orbitCharacteristicsButton.onClick.RemoveListener(HandleOrbitCharacteristicsButton);
        }

        if (structureButton != null)
        {
            structureButton.onClick.RemoveListener(HandleStructureButton);
        }

        if (atmosphereButton != null)
        {
            atmosphereButton.onClick.RemoveListener(HandleAtmosphereButton);
        }

        if (galleryBackButton != null)
        {
            galleryBackButton.onClick.RemoveListener(HandleGalleryBackButton);
        }

        if (viewerCloseButton != null)
        {
            viewerCloseButton.onClick.RemoveListener(HandleViewerCloseButton);
        }

        if (viewerPreviousButton != null)
        {
            viewerPreviousButton.onClick.RemoveListener(HandleViewerPreviousButton);
        }

        if (viewerNextButton != null)
        {
            viewerNextButton.onClick.RemoveListener(HandleViewerNextButton);
        }

        if (imageCaptionHideButton != null)
        {
            imageCaptionHideButton.onClick.RemoveListener(HandleImageCaptionHideButton);
        }

        if (imageCaptionShowButton != null)
        {
            imageCaptionShowButton.onClick.RemoveListener(HandleImageCaptionShowButton);
        }

    }

    private void Update()
    {
        UpdateTopLeftNavigationButtons();
        UpdateSolarSystemLabels();

        Vector2? pointerPosition = GetPointerDownPosition();

        if (!pointerPosition.HasValue || mainCamera == null)
        {
            return;
        }

        if (IsPointerOverUi())
        {
            return;
        }

        CelestialBody body = GetSelectedBody(pointerPosition.Value);

        if (body == null)
        {
            return;
        }

        if (currentBody != null && body == currentBody)
        {
            return;
        }

        FocusBody(body);
    }

    private void InitializeSolarSystemLabels()
    {
        if (!showSolarSystemLabels || solarSystemUiRoot == null)
        {
            return;
        }

        solarSystemCanvas = solarSystemUiRoot.GetComponent<Canvas>();
        if (solarSystemCanvas == null)
        {
            return;
        }

        GameObject labelsRootObject = FindChildObjectByName(solarSystemUiRoot, "SolarSystemLabels");
        if (labelsRootObject == null)
        {
            labelsRootObject = new GameObject("SolarSystemLabels", typeof(RectTransform));
            labelsRootObject.transform.SetParent(solarSystemUiRoot.transform, false);
        }

        solarSystemLabelsRoot = labelsRootObject.GetComponent<RectTransform>();
        solarSystemLabelsRoot.anchorMin = Vector2.zero;
        solarSystemLabelsRoot.anchorMax = Vector2.one;
        solarSystemLabelsRoot.offsetMin = Vector2.zero;
        solarSystemLabelsRoot.offsetMax = Vector2.zero;
        solarSystemLabelsRoot.SetAsLastSibling();

        solarSystemLabelMap.Clear();
        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        foreach (CelestialBody body in bodies)
        {
            if (body == null || string.IsNullOrWhiteSpace(body.bodyName))
            {
                continue;
            }

            TMP_Text labelText = CreateSolarSystemLabel(body);
            if (labelText != null)
            {
                solarSystemLabelMap[body] = labelText;
            }
        }
    }

    private TMP_Text CreateSolarSystemLabel(CelestialBody body)
    {
        if (solarSystemLabelsRoot == null)
        {
            return null;
        }

        GameObject labelObject = new GameObject($"{body.bodyName}Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(solarSystemLabelsRoot, false);

        TextMeshProUGUI labelText = labelObject.GetComponent<TextMeshProUGUI>();
        labelText.text = body.bodyName;
        labelText.fontSize = solarSystemLabelFontSize;
        labelText.color = body.labelColor;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.raycastTarget = false;

        RectTransform rectTransform = labelText.rectTransform;
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(160f, 32f);

        return labelText;
    }

    private void UpdateSolarSystemLabels()
    {
        if (!showSolarSystemLabels || mainCamera == null || solarSystemLabelsRoot == null)
        {
            return;
        }

        bool shouldShow = solarSystemUiRoot != null && solarSystemUiRoot.activeInHierarchy && currentBody == null;
        solarSystemLabelsRoot.gameObject.SetActive(shouldShow);

        if (!shouldShow)
        {
            return;
        }

        RectTransform canvasRect = solarSystemCanvas != null ? solarSystemCanvas.GetComponent<RectTransform>() : solarSystemLabelsRoot;

        foreach (KeyValuePair<CelestialBody, TMP_Text> pair in solarSystemLabelMap)
        {
            CelestialBody body = pair.Key;
            TMP_Text labelText = pair.Value;

            if (body == null || labelText == null)
            {
                continue;
            }

            labelText.color = body.labelColor;

            Vector3 worldCenter = body.transform.position;
            Renderer bodyRenderer = body.GetComponent<Renderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = body.GetComponentInChildren<Renderer>();
            }

            if (bodyRenderer != null)
            {
                worldCenter = bodyRenderer.bounds.center;
            }

            Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldCenter);
            bool isVisible = screenPoint.z > 0f &&
                             screenPoint.x >= -40f && screenPoint.x <= Screen.width + 40f &&
                             screenPoint.y >= -40f && screenPoint.y <= Screen.height + 40f;

            labelText.gameObject.SetActive(isVisible);
            if (!isVisible)
            {
                continue;
            }

            float bodyScreenRadius = 0f;
            if (bodyRenderer != null)
            {
                Vector3 edgeWorldPoint = worldCenter + mainCamera.transform.right * bodyRenderer.bounds.extents.magnitude;
                Vector3 edgeScreenPoint = mainCamera.WorldToScreenPoint(edgeWorldPoint);
                if (edgeScreenPoint.z > 0f)
                {
                    bodyScreenRadius = Mathf.Abs(edgeScreenPoint.x - screenPoint.x);
                }
            }

            Vector3 labelScreenPoint = screenPoint + new Vector3(bodyScreenRadius, bodyScreenRadius * 0.35f, 0f) + (Vector3)solarSystemLabelScreenOffset;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                labelScreenPoint,
                solarSystemCanvas != null && solarSystemCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? solarSystemCanvas.worldCamera : null,
                out Vector2 localPoint);

            labelText.rectTransform.anchoredPosition = localPoint;
        }
    }

    private static Vector2? GetPointerDownPosition()
    {
        if (Touchscreen.current != null)
        {
            foreach (TouchControl touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    return touch.position.ReadValue();
                }
            }
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return Mouse.current.position.ReadValue();
        }

        return null;
    }

    private CelestialBody GetSelectedBody(Vector2 screenPosition)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.SphereCastAll(ray, selectionSphereRadius, 1000f);

        CelestialBody bestBody = null;
        float bestScreenDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            CelestialBody body = hit.collider.GetComponent<CelestialBody>();

            if (body == null)
            {
                body = hit.collider.GetComponentInParent<CelestialBody>();
            }

            if (body == null)
            {
                continue;
            }

            if (!IsCelestialBodySelectable(body))
            {
                continue;
            }

            Vector3 worldCenter = body.transform.position;
            Renderer bodyRenderer = body.GetComponent<Renderer>();

            if (bodyRenderer == null)
            {
                bodyRenderer = body.GetComponentInChildren<Renderer>();
            }

            if (bodyRenderer != null)
            {
                worldCenter = bodyRenderer.bounds.center;
            }

            Vector3 screenPoint = mainCamera.WorldToScreenPoint(worldCenter);
            float screenDistance = Vector2.Distance(screenPosition, screenPoint);

            if (screenDistance < bestScreenDistance)
            {
                bestScreenDistance = screenDistance;
                bestBody = body;
            }
        }

        if (bestBody != null && bestScreenDistance <= selectionScreenThreshold)
        {
            return bestBody;
        }

        if (!Physics.Raycast(ray, out RaycastHit directHit))
        {
            return null;
        }

        CelestialBody directBody = directHit.collider.GetComponent<CelestialBody>();

        if (directBody == null)
        {
            directBody = directHit.collider.GetComponentInParent<CelestialBody>();
        }

        if (directBody != null && !IsCelestialBodySelectable(directBody))
        {
            return null;
        }

        return directBody;
    }

    private void HandleFocusBackButton()
    {
        CloseGalleryOverlays();

        if (currentBody == null)
        {
            RestoreAllCelestialBodyRenderers();

            if (panel != null)
            {
                panel.SetActive(false);
            }

            return;
        }

        currentBody = null;

        if (panel != null)
        {
            panel.SetActive(false);
        }

        UpdateCelestialBodyImage(null);
        SetCelestialMotionEnabled(true);
        SetOrbitLinesVisible(true);
        SetSimulationControlsVisible(true);
        RestoreAllCelestialBodyRenderers();

        if (orbitCamera != null)
        {
            orbitCamera.RestoreDefaultView();
        }

        if (solarAudioManager != null)
        {
            solarAudioManager.StopPlanetMusic();
        }

        UpdateSoundButtonIcon(false);
        UpdateTopLeftNavigationButtons();
    }

    private void HandleCelestialBodyBackButton()
    {
        CloseGalleryOverlays();
        UpdateInspectCameraPanelOffset(false);
        RestoreAllCelestialBodyRenderers();

        if (celestialBodyUiRoot != null)
        {
            celestialBodyUiRoot.SetActive(false);
        }

        if (solarSystemUiRoot != null)
        {
            solarSystemUiRoot.SetActive(true);
        }

        if (solarAudioManager != null)
        {
            solarAudioManager.StopPlanetMusic();
        }

        UpdateSoundButtonIcon(false);
        UpdateTopLeftNavigationButtons();
    }

    private void HandleVisitButton()
    {
        if (currentBody == null)
        {
            currentBody = FindBodyByName("Sun");
        }

        CloseGalleryOverlays();

        if (celestialBodyUiRoot != null)
        {
            celestialBodyUiRoot.SetActive(true);
        }

        if (solarSystemUiRoot != null)
        {
            solarSystemUiRoot.SetActive(false);
        }

        IsolateVisitedCelestialBody(currentBody);

        ShowBasicsTab(true);
        Canvas.ForceUpdateCanvases();
        UpdateFocusedUi();
        SetActiveSection(CelestialSection.PlanetaryProfile);
        UpdateInspectCameraPanelOffset(true);

        if (visitRefreshCoroutine != null)
        {
            StopCoroutine(visitRefreshCoroutine);
        }

        visitRefreshCoroutine = StartCoroutine(RefreshFocusedUiAfterVisit());
        UpdateTopLeftNavigationButtons();
    }

    private IEnumerator RefreshFocusedUiAfterVisit()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        UpdateFocusedUi();
        UpdateTopLeftNavigationButtons();
        visitRefreshCoroutine = null;
    }

    private void HandleTargetButton()
    {
        if (currentBody == null || orbitCamera == null)
        {
            return;
        }

        currentBody.ResetToDefaultOrientation();
        orbitCamera.ResetFocusedView();
    }

    private void HandlePreviousButton()
    {
        FocusAdjacentBody(-1);
    }

    private void HandleNextButton()
    {
        FocusAdjacentBody(1);
    }

    private void UpdateTopLeftNavigationButtons()
    {
        bool isFocusedPlanetView =
            currentBody != null &&
            celestialBodyUiRoot != null &&
            !celestialBodyUiRoot.activeInHierarchy;

        bool isCelestialBodyViewActive =
            celestialBodyUiRoot != null &&
            celestialBodyUiRoot.activeInHierarchy;

        bool isSolarSystemViewActive =
            !isFocusedPlanetView &&
            !isCelestialBodyViewActive &&
            solarSystemUiRoot != null &&
            solarSystemUiRoot.activeInHierarchy;

        if (solarSystemTopLeftMenuObject != null)
        {
            solarSystemTopLeftMenuObject.SetActive(isSolarSystemViewActive);
        }

        SetMenuIconVisibility(solarSystemTopLeftMenuObject, true);

        if (solarSystemTopLeftBackObject != null)
        {
            solarSystemTopLeftBackObject.SetActive(isFocusedPlanetView);
        }

        SetMenuIconVisibility(solarSystemTopLeftBackObject, false);

        if (celestialBodyTopLeftBackObject != null)
        {
            celestialBodyTopLeftBackObject.SetActive(isCelestialBodyViewActive);
        }

        SetMenuIconVisibility(celestialBodyTopLeftBackObject, false);
    }

    private static void SetMenuIconVisibility(GameObject buttonObject, bool isVisible)
    {
        if (buttonObject == null)
        {
            return;
        }

        GameObject menuIconObject = FindChildObjectByName(buttonObject, "MenuIcon");
        if (menuIconObject != null)
        {
            menuIconObject.SetActive(isVisible);
        }
    }

    private void UpdateCelestialBodyImage(CelestialBody body)
    {
        if (celestialBodyImage == null)
        {
            return;
        }

        if (body == null || string.IsNullOrWhiteSpace(body.bodyName))
        {
            celestialBodyImage.enabled = false;
            return;
        }

        Sprite bodySprite = FindBodySprite(body.bodyName);
        if (bodySprite == null)
        {
            celestialBodyImage.enabled = false;
            return;
        }

        celestialBodyImage.sprite = bodySprite;
        celestialBodyImage.overrideSprite = bodySprite;
        celestialBodyImage.enabled = true;
        celestialBodyImage.preserveAspect = true;
        celestialBodyImage.SetAllDirty();
    }

    private void UpdateCelestialProfileButtonImage(CelestialBody body)
    {
        if (celestialProfileButtonImage == null)
        {
            return;
        }

        if (body == null || string.IsNullOrWhiteSpace(body.bodyName))
        {
            celestialProfileButtonImage.enabled = false;
            return;
        }

        Sprite bodySprite = FindBodySprite(body.bodyName);
        if (bodySprite == null)
        {
            celestialProfileButtonImage.enabled = false;
            return;
        }

        celestialProfileButtonImage.sprite = bodySprite;
        celestialProfileButtonImage.overrideSprite = bodySprite;
        celestialProfileButtonImage.enabled = true;
        celestialProfileButtonImage.preserveAspect = true;
        celestialProfileButtonImage.SetAllDirty();
    }

    private void UpdateArPlanetIcon(CelestialBody body)
    {
        if (arPlanetIcon == null)
        {
            return;
        }

        if (body == null || string.IsNullOrWhiteSpace(body.bodyName))
        {
            arPlanetIcon.enabled = false;
            return;
        }

        Sprite bodySprite = FindBodySprite(body.bodyName);
        if (bodySprite == null)
        {
            arPlanetIcon.enabled = false;
            return;
        }

        arPlanetIcon.sprite = bodySprite;
        arPlanetIcon.overrideSprite = bodySprite;
        arPlanetIcon.enabled = true;
        arPlanetIcon.preserveAspect = true;
        arPlanetIcon.SetAllDirty();
    }

    private void UpdateFocusedUi()
    {
        string bodyName = currentBody != null ? currentBody.bodyName : string.Empty;

        if (planetNameText != null)
        {
            planetNameText.text = bodyName;
        }

        if (celestialBodyNameText != null)
        {
            celestialBodyNameText.text = bodyName;
        }

        UpdateCelestialBodyImage(currentBody);
        UpdateCelestialProfileButtonImage(currentBody);
        UpdateArPlanetIcon(currentBody);
        UpdatePlanetaryProfileContent(currentBody);
        UpdateStatsContent(currentBody);
        UpdateOrbitCharacteristicsContent(currentBody);
        UpdateStructureContent(currentBody);
        UpdateAtmosphereContent(currentBody);
        UpdateGalleryContent(currentBody);
        UpdateSectionAvailability(currentBody);
        ResetAllScrollPositions();
    }

    private void UpdatePlanetaryProfileContent(CelestialBody body)
    {
        if (planetaryProfileMainImageContent != null)
        {
            if (body != null && body.profileImage != null)
            {
                planetaryProfileMainImageContent.sprite = body.profileImage;
                planetaryProfileMainImageContent.overrideSprite = body.profileImage;
                planetaryProfileMainImageContent.enabled = true;
                planetaryProfileMainImageContent.preserveAspect = false;
                ApplyCoverImageSizing(body.profileImage);
                planetaryProfileMainImageContent.SetAllDirty();
            }
            else
            {
                planetaryProfileMainImageContent.enabled = false;
            }
        }

        if (planetaryProfileDescriptionText != null)
        {
            planetaryProfileDescriptionText.text = body != null ? body.profileDescription : string.Empty;
        }
    }

    private void UpdateStatsContent(CelestialBody body)
    {
        if (statsContent == null || statRowTemplate == null)
        {
            return;
        }

        ClearGeneratedStatRows();

        if (body == null || body.stats == null || body.stats.Count == 0)
        {
            RebuildStatsLayout();
            return;
        }

        for (int i = 0; i < body.stats.Count; i++)
        {
            CelestialStatEntry statEntry = body.stats[i];
            GameObject rowObject = Instantiate(statRowTemplate, statsContent, false);
            rowObject.name = $"{statRowTemplate.name}_{i}";
            rowObject.SetActive(true);

            Image rowImage = rowObject.GetComponent<Image>();
            if (rowImage != null)
            {
                Color oddColor = new Color(0.02f, 0.02f, 0.02f, 0.92f);
                Color evenColor = new Color(0.03f, 0.14f, 0.4f, 0.95f);
                rowImage.enabled = true;
                rowImage.color = i % 2 == 0 ? oddColor : evenColor;
            }

            TMP_Text labelText = FindComponentInChildrenByName<TMP_Text>(rowObject, "LabelText") ?? statRowTemplateLabelText;
            TMP_Text valueText = FindComponentInChildrenByName<TMP_Text>(rowObject, "ValueText") ?? statRowTemplateValueText;

            if (labelText != null)
            {
                labelText.text = statEntry != null ? statEntry.label : string.Empty;
            }

            if (valueText != null)
            {
                valueText.text = statEntry != null ? statEntry.value : string.Empty;
            }

            generatedStatRows.Add(rowObject);
        }

        RebuildStatsLayout();
    }

    private void UpdateOrbitCharacteristicsContent(CelestialBody body)
    {
        if (orbitCharacteristicsDescriptionText != null)
        {
            orbitCharacteristicsDescriptionText.text = body != null ? body.orbitCharacteristicsDescription : string.Empty;
            RefreshScrollableTextLayout(orbitCharacteristicsDescriptionText, orbitCharacteristicsScrollRect);
        }
    }

    private void UpdateStructureContent(CelestialBody body)
    {
        if (structureDescriptionText != null)
        {
            structureDescriptionText.text = body != null ? body.structureDescription : string.Empty;
            RefreshScrollableTextLayout(structureDescriptionText, structureScrollRect);
        }
    }

    private void UpdateAtmosphereContent(CelestialBody body)
    {
        if (atmosphereMainImageContent != null)
        {
            if (body != null && body.atmosphereImage != null)
            {
                atmosphereMainImageContent.sprite = body.atmosphereImage;
                atmosphereMainImageContent.overrideSprite = body.atmosphereImage;
                atmosphereMainImageContent.enabled = true;
                atmosphereMainImageContent.preserveAspect = false;
                ApplyCoverImageSizing(body.atmosphereImage, atmosphereMainImage.rectTransform, atmosphereMainImageContent.rectTransform);
                atmosphereMainImageContent.SetAllDirty();
            }
            else
            {
                atmosphereMainImageContent.enabled = false;
            }
        }

        if (atmosphereDescriptionText != null)
        {
            atmosphereDescriptionText.text = body != null ? body.atmosphereDescription : string.Empty;
            RefreshScrollableTextLayout(atmosphereDescriptionText, atmosphereScrollRect);
        }
    }

    private void UpdateGalleryContent(CelestialBody body)
    {
        if (imagesGalleryOverlay != null && imagesGalleryOverlay.activeInHierarchy)
        {
            PopulateGalleryForCurrentBody();
        }

        if (imageViewerOverlay != null && imageViewerOverlay.activeInHierarchy)
        {
            if (body == null || body.galleryEntries == null || body.galleryEntries.Count == 0)
            {
                HandleGalleryBackButton();
                return;
            }

            int safeIndex = Mathf.Clamp(currentGalleryIndex, 0, body.galleryEntries.Count - 1);
            CelestialGalleryEntry activeEntry = body.galleryEntries[safeIndex];

            bool sameTitle = viewerImageTitleText == null || viewerImageTitleText.text == (activeEntry != null ? activeEntry.title : string.Empty);
            bool sameImage = viewerMainImage == null || viewerMainImage.sprite == (activeEntry != null ? activeEntry.image : null);
            bool sameCaption = imageCaptionText == null || imageCaptionText.text == (activeEntry != null ? activeEntry.description : string.Empty);

            currentGalleryIndex = safeIndex;

            if (!(sameTitle && sameImage && sameCaption))
            {
                ShowGalleryEntry(currentGalleryIndex);
            }
        }
    }

    private void UpdateSectionAvailability(CelestialBody body)
    {
        bool hasOrbitCharacteristics = body != null && !string.IsNullOrWhiteSpace(body.orbitCharacteristicsDescription);

        if (orbitCharacteristicsButton != null)
        {
            orbitCharacteristicsButton.gameObject.SetActive(hasOrbitCharacteristics);
        }

        if (activeSection == CelestialSection.OrbitCharacteristics && !hasOrbitCharacteristics)
        {
            SetActiveSection(CelestialSection.PlanetaryProfile);
        }
    }

    private static void RefreshScrollableTextLayout(TMP_Text textComponent, ScrollRect scrollRect)
    {
        if (textComponent == null)
        {
            return;
        }

        textComponent.ForceMeshUpdate();

        RectTransform textRect = textComponent.rectTransform;
        float targetWidth = textRect.rect.width;
        Vector2 preferredValues = textComponent.GetPreferredValues(textComponent.text, targetWidth, 0f);
        float targetHeight = Mathf.Max(preferredValues.y, 1f);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(textRect);

        if (scrollRect != null)
        {
            if (scrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
            }

            RectTransform scrollRectTransform = scrollRect.transform as RectTransform;
            if (scrollRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectTransform);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ClearGeneratedStatRows()
    {
        for (int i = 0; i < generatedStatRows.Count; i++)
        {
            if (generatedStatRows[i] != null)
            {
                Destroy(generatedStatRows[i]);
            }
        }

        generatedStatRows.Clear();
    }

    private void ClearGeneratedGalleryItems()
    {
        for (int i = 0; i < generatedGalleryItems.Count; i++)
        {
            if (generatedGalleryItems[i] != null)
            {
                Destroy(generatedGalleryItems[i]);
            }
        }

        generatedGalleryItems.Clear();
    }

    private void PopulateGalleryForCurrentBody()
    {
        if (galleryContent == null || galleryItemTemplate == null)
        {
            return;
        }

        ClearGeneratedGalleryItems();

        if (currentBody == null || currentBody.galleryEntries == null || currentBody.galleryEntries.Count == 0)
        {
            RebuildGalleryLayout();
            return;
        }

        for (int i = 0; i < currentBody.galleryEntries.Count; i++)
        {
            CelestialGalleryEntry galleryEntry = currentBody.galleryEntries[i];
            GameObject itemObject = Instantiate(galleryItemTemplate, galleryContent, false);
            itemObject.name = $"{galleryItemTemplate.name}_{i}";
            itemObject.SetActive(true);

            Image thumbnailImage = FindComponentInChildrenByName<Image>(itemObject, "ThumbnailImage") ?? galleryItemTemplateThumbnailImage;
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = galleryEntry != null ? galleryEntry.image : null;
                thumbnailImage.overrideSprite = galleryEntry != null ? galleryEntry.image : null;
                thumbnailImage.enabled = galleryEntry != null && galleryEntry.image != null;
                thumbnailImage.preserveAspect = false;
                if (galleryEntry != null && galleryEntry.image != null)
                {
                    ApplyGalleryThumbnailCoverSizing(thumbnailImage, galleryEntry.image);
                }
                thumbnailImage.SetAllDirty();
            }

            Button itemButton = itemObject.GetComponent<Button>();
            if (itemButton != null)
            {
                int entryIndex = i;
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(() => OpenGalleryEntry(entryIndex));
            }

            generatedGalleryItems.Add(itemObject);
        }

        RebuildGalleryLayout();
    }

    private void RebuildGalleryLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (galleryScrollRect != null)
        {
            galleryScrollRect.horizontal = false;
            galleryScrollRect.vertical = true;
            galleryScrollRect.movementType = ScrollRect.MovementType.Clamped;
        }

        RectTransform viewportRect = galleryScrollRect != null ? galleryScrollRect.viewport : null;
        GridLayoutGroup gridLayout = galleryContent != null ? galleryContent.GetComponent<GridLayoutGroup>() : null;

        if (galleryContent != null && viewportRect != null)
        {
            galleryContent.anchorMin = new Vector2(0f, 1f);
            galleryContent.anchorMax = new Vector2(1f, 1f);
            galleryContent.pivot = new Vector2(0.5f, 1f);
            galleryContent.anchoredPosition = new Vector2(0f, galleryContent.anchoredPosition.y);

            float viewportWidth = viewportRect.rect.width;
            if (viewportWidth > 0f)
            {
                galleryContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);
            }
        }

        if (gridLayout != null && viewportRect != null)
        {
            float viewportWidth = viewportRect.rect.width;
            float cellWidth = gridLayout.cellSize.x;
            float spacingX = gridLayout.spacing.x;
            float paddingWidth = gridLayout.padding.left + gridLayout.padding.right;
            float usableWidth = Mathf.Max(0f, viewportWidth - paddingWidth);
            int columnCount = Mathf.Max(1, Mathf.FloorToInt((usableWidth + spacingX) / Mathf.Max(1f, cellWidth + spacingX)));

            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = columnCount;
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
        }

        if (galleryContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(galleryContent);
        }

        if (galleryScrollRect != null)
        {
            RectTransform scrollRectTransform = galleryScrollRect.transform as RectTransform;
            if (scrollRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectTransform);
            }

            galleryScrollRect.verticalNormalizedPosition = 1f;
            galleryScrollRect.horizontalNormalizedPosition = 0f;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void OpenGalleryEntry(int index)
    {
        if (currentBody == null || currentBody.galleryEntries == null || currentBody.galleryEntries.Count == 0)
        {
            return;
        }

        currentGalleryIndex = Mathf.Clamp(index, 0, currentBody.galleryEntries.Count - 1);

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(true);
        }

        ShowGalleryEntry(currentGalleryIndex);
    }

    private void ShowAdjacentGalleryEntry(int direction)
    {
        if (currentBody == null || currentBody.galleryEntries == null || currentBody.galleryEntries.Count == 0)
        {
            return;
        }

        if (currentGalleryIndex < 0)
        {
            currentGalleryIndex = 0;
        }
        else
        {
            currentGalleryIndex = (currentGalleryIndex + direction + currentBody.galleryEntries.Count) % currentBody.galleryEntries.Count;
        }

        ShowGalleryEntry(currentGalleryIndex);
    }

    private void ShowGalleryEntry(int index)
    {
        if (currentBody == null || currentBody.galleryEntries == null || currentBody.galleryEntries.Count == 0)
        {
            return;
        }

        int safeIndex = Mathf.Clamp(index, 0, currentBody.galleryEntries.Count - 1);
        CelestialGalleryEntry galleryEntry = currentBody.galleryEntries[safeIndex];
        currentGalleryIndex = safeIndex;
        SetImageCaptionPanelVisible(true);

        if (viewerImageTitleText != null)
        {
            viewerImageTitleText.text = galleryEntry != null ? galleryEntry.title : string.Empty;
        }

        if (imageCaptionText != null)
        {
            imageCaptionText.text = galleryEntry != null ? galleryEntry.description : string.Empty;
            RefreshImageCaptionLayout();
        }

        if (viewerMainImage != null)
        {
            if (galleryEntry != null && galleryEntry.image != null)
            {
                viewerMainImage.sprite = galleryEntry.image;
                viewerMainImage.overrideSprite = galleryEntry.image;
                viewerMainImage.enabled = true;
                viewerMainImage.preserveAspect = true;
                viewerMainImage.SetAllDirty();
            }
            else
            {
                viewerMainImage.enabled = false;
            }
        }

    }

    private void RefreshImageCaptionLayout()
    {
        if (imageCaptionText == null)
        {
            return;
        }

        imageCaptionText.raycastTarget = false;
        imageCaptionText.ForceMeshUpdate();
        LayoutRebuilder.ForceRebuildLayoutImmediate(imageCaptionText.rectTransform);

        if (imageCaptionScrollRect != null)
        {
            if (imageCaptionScrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(imageCaptionScrollRect.content);
            }

            if (imageCaptionScrollRect.viewport != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(imageCaptionScrollRect.viewport);
            }

            RectTransform scrollRectTransform = imageCaptionScrollRect.transform as RectTransform;
            if (scrollRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectTransform);
            }

            imageCaptionScrollRect.verticalNormalizedPosition = 1f;
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SetImageCaptionPanelVisible(bool isVisible)
    {
        if (imageCaptionPanel != null)
        {
            imageCaptionPanel.SetActive(isVisible);
        }

        if (imageCaptionHideButton != null)
        {
            imageCaptionHideButton.gameObject.SetActive(isVisible);
        }

        if (imageCaptionShowButton != null)
        {
            imageCaptionShowButton.gameObject.SetActive(!isVisible);
        }
    }

    private void RebuildStatsLayout()
    {
        Canvas.ForceUpdateCanvases();

        if (statsContent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(statsContent);
        }

        if (statsScrollRect != null)
        {
            RectTransform scrollRectTransform = statsScrollRect.transform as RectTransform;
            if (scrollRectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectTransform);
            }
        }

        Canvas.ForceUpdateCanvases();
    }

    private void ResetAllScrollPositions()
    {
        ResetScrollRect(planetaryProfileScrollRect);
        ResetScrollRect(statsScrollRect);
        ResetScrollRect(orbitCharacteristicsScrollRect);
        ResetScrollRect(structureScrollRect);
        ResetScrollRect(atmosphereScrollRect);
    }

    private void ResetScrollPositionForSection(CelestialSection section)
    {
        switch (section)
        {
            case CelestialSection.PlanetaryProfile:
                ResetScrollRect(planetaryProfileScrollRect);
                break;
            case CelestialSection.Stats:
                ResetScrollRect(statsScrollRect);
                break;
            case CelestialSection.OrbitCharacteristics:
                ResetScrollRect(orbitCharacteristicsScrollRect);
                break;
            case CelestialSection.Structure:
                ResetScrollRect(structureScrollRect);
                break;
            case CelestialSection.Atmosphere:
                ResetScrollRect(atmosphereScrollRect);
                break;
        }
    }

    private static void ResetScrollRect(ScrollRect scrollRect)
    {
        if (scrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        if (scrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
        }

        RectTransform scrollRectTransform = scrollRect.transform as RectTransform;
        if (scrollRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectTransform);
        }

        Canvas.ForceUpdateCanvases();
        scrollRect.StopMovement();
        scrollRect.normalizedPosition = new Vector2(0f, 1f);
        scrollRect.verticalNormalizedPosition = 1f;

        if (scrollRect.content != null)
        {
            scrollRect.content.anchoredPosition = new Vector2(scrollRect.content.anchoredPosition.x, 0f);
        }
    }

    private void EnsurePlanetaryProfileCoverImage()
    {
        if (planetaryProfileMainImage == null)
        {
            return;
        }

        RectMask2D rectMask = planetaryProfileMainImage.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = planetaryProfileMainImage.gameObject.AddComponent<RectMask2D>();
        }

        planetaryProfileMainImage.raycastTarget = false;
        planetaryProfileMainImage.color = new Color(1f, 1f, 1f, 0f);
        planetaryProfileMainImage.sprite = null;
        planetaryProfileMainImage.overrideSprite = null;

        Transform existingChild = planetaryProfileMainImage.transform.Find("CoverImage");
        if (existingChild != null)
        {
            planetaryProfileMainImageContent = existingChild.GetComponent<Image>();
        }

        if (planetaryProfileMainImageContent == null)
        {
            GameObject coverImageObject = new GameObject("CoverImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coverImageObject.layer = planetaryProfileMainImage.gameObject.layer;
            coverImageObject.transform.SetParent(planetaryProfileMainImage.transform, false);
            planetaryProfileMainImageContent = coverImageObject.GetComponent<Image>();
        }

        RectTransform coverRect = planetaryProfileMainImageContent.rectTransform;
        coverRect.anchorMin = new Vector2(0.5f, 1f);
        coverRect.anchorMax = new Vector2(0.5f, 1f);
        coverRect.pivot = new Vector2(0.5f, 1f);
        coverRect.anchoredPosition = Vector2.zero;
        coverRect.localScale = Vector3.one;

        planetaryProfileMainImageContent.raycastTarget = false;
        planetaryProfileMainImageContent.type = Image.Type.Simple;
        planetaryProfileMainImageContent.preserveAspect = false;
    }

    private void EnsureAtmosphereCoverImage()
    {
        if (atmosphereMainImage == null)
        {
            return;
        }

        RectMask2D rectMask = atmosphereMainImage.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = atmosphereMainImage.gameObject.AddComponent<RectMask2D>();
        }

        atmosphereMainImage.raycastTarget = false;
        atmosphereMainImage.color = new Color(1f, 1f, 1f, 0f);
        atmosphereMainImage.sprite = null;
        atmosphereMainImage.overrideSprite = null;

        Transform existingChild = atmosphereMainImage.transform.Find("CoverImage");
        if (existingChild != null)
        {
            atmosphereMainImageContent = existingChild.GetComponent<Image>();
        }

        if (atmosphereMainImageContent == null)
        {
            GameObject coverImageObject = new GameObject("CoverImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            coverImageObject.layer = atmosphereMainImage.gameObject.layer;
            coverImageObject.transform.SetParent(atmosphereMainImage.transform, false);
            atmosphereMainImageContent = coverImageObject.GetComponent<Image>();
        }

        RectTransform coverRect = atmosphereMainImageContent.rectTransform;
        coverRect.anchorMin = new Vector2(0.5f, 1f);
        coverRect.anchorMax = new Vector2(0.5f, 1f);
        coverRect.pivot = new Vector2(0.5f, 1f);
        coverRect.anchoredPosition = Vector2.zero;
        coverRect.localScale = Vector3.one;

        atmosphereMainImageContent.raycastTarget = false;
        atmosphereMainImageContent.type = Image.Type.Simple;
        atmosphereMainImageContent.preserveAspect = false;
    }

    private void EnsureGalleryItemTemplateCoverImage()
    {
        if (galleryItemTemplate == null || galleryItemTemplateThumbnailImage == null)
        {
            return;
        }

        RectMask2D rectMask = galleryItemTemplate.GetComponent<RectMask2D>();
        if (rectMask == null)
        {
            rectMask = galleryItemTemplate.AddComponent<RectMask2D>();
        }

        Image templateBackground = galleryItemTemplate.GetComponent<Image>();
        if (templateBackground != null)
        {
            templateBackground.color = Color.black;
        }

        galleryItemTemplateThumbnailRect = galleryItemTemplateThumbnailImage.rectTransform;
        galleryItemTemplateThumbnailRect.anchorMin = new Vector2(0.5f, 0.5f);
        galleryItemTemplateThumbnailRect.anchorMax = new Vector2(0.5f, 0.5f);
        galleryItemTemplateThumbnailRect.pivot = new Vector2(0.5f, 0.5f);
        galleryItemTemplateThumbnailRect.anchoredPosition = Vector2.zero;
        galleryItemTemplateThumbnailRect.localScale = Vector3.one;

        galleryItemTemplateThumbnailImage.raycastTarget = false;
        galleryItemTemplateThumbnailImage.type = Image.Type.Simple;
        galleryItemTemplateThumbnailImage.preserveAspect = false;
    }
    private void ApplyCoverImageSizing(Sprite sprite)
    {
        if (planetaryProfileMainImage == null || planetaryProfileMainImageContent == null || sprite == null)
        {
            return;
        }

        ApplyCoverImageSizing(sprite, planetaryProfileMainImage.rectTransform, planetaryProfileMainImageContent.rectTransform);
    }

    private void ApplyCoverImageSizing(Sprite sprite, RectTransform containerRect, RectTransform coverRect)
    {
        if (containerRect == null || coverRect == null || sprite == null)
        {
            return;
        }

        Rect container = containerRect.rect;

        if (container.width <= 0f || container.height <= 0f)
        {
            return;
        }

        float spriteWidth = sprite.rect.width;
        float spriteHeight = sprite.rect.height;
        if (spriteWidth <= 0f || spriteHeight <= 0f)
        {
            return;
        }

        float containerAspect = container.width / container.height;
        float spriteAspect = spriteWidth / spriteHeight;

        float targetWidth;
        float targetHeight;

        if (spriteAspect > containerAspect)
        {
            targetHeight = container.height;
            targetWidth = targetHeight * spriteAspect;
        }
        else
        {
            targetWidth = container.width;
            targetHeight = targetWidth / spriteAspect;
        }

        float zoomMultiplier = Mathf.Max(1f, profileImageZoomMultiplier);
        targetWidth *= zoomMultiplier;
        targetHeight *= zoomMultiplier;

        coverRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);
        coverRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetHeight);
        coverRect.anchoredPosition = Vector2.zero;
    }

    private void ApplyGalleryThumbnailCoverSizing(Image thumbnailImage, Sprite sprite)
    {
        if (thumbnailImage == null || sprite == null)
        {
            return;
        }

        RectTransform containerRect = thumbnailImage.transform.parent as RectTransform;
        RectTransform coverRect = thumbnailImage.rectTransform;
        if (containerRect == null || coverRect == null)
        {
            return;
        }

        coverRect.anchorMin = new Vector2(0.5f, 0.5f);
        coverRect.anchorMax = new Vector2(0.5f, 0.5f);
        coverRect.pivot = new Vector2(0.5f, 0.5f);
        coverRect.anchoredPosition = Vector2.zero;
        coverRect.localScale = Vector3.one;

        ApplyCoverImageSizing(sprite, containerRect, coverRect);
    }

    private Sprite FindBodySprite(string bodyName)
    {
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return null;
        }

#if UNITY_EDITOR
        if (bodyName == "Saturn")
        {
            Sprite saturnSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/CelestialBodyImages/Saturn.png");
            if (saturnSprite != null)
            {
                return saturnSprite;
            }
        }

        if (bodyName == "Neptune")
        {
            Sprite neptuneSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/CelestialBodyImages/Neptune.png");
            if (neptuneSprite != null)
            {
                return neptuneSprite;
            }
        }

        string preferredIconPath = Path.Combine("Assets", "Icons", "CelestialBodyImages", bodyName + ".png").Replace("\\", "/");
        Sprite preferredIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(preferredIconPath);
        if (preferredIconSprite != null)
        {
            return preferredIconSprite;
        }
#endif

        Sprite existingSprite = FindSpriteByName(bodyName);
        if (existingSprite != null)
        {
            return existingSprite;
        }

        if (runtimeBodySpriteCache.TryGetValue(bodyName, out Sprite cachedSprite))
        {
            return cachedSprite;
        }

#if UNITY_EDITOR
        string lowerName = bodyName.ToLowerInvariant();
        string[] texturePaths =
        {
            Path.Combine("Assets", "Textures", lowerName + ".jpg").Replace("\\", "/"),
            Path.Combine("Assets", "Textures", lowerName + ".png").Replace("\\", "/"),
            Path.Combine("Assets", "Textures", lowerName + "_surface.jpg").Replace("\\", "/"),
            Path.Combine("Assets", "Textures", lowerName + "_surface.png").Replace("\\", "/")
        };

        foreach (string texturePath in texturePaths)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                continue;
            }

            Sprite createdSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));

            runtimeBodySpriteCache[bodyName] = createdSprite;
            return createdSprite;
        }
#endif

        return null;
    }

    private void FocusAdjacentBody(int direction)
    {
        int currentIndex = GetCurrentFocusIndex();
        int startIndex = currentIndex >= 0 ? currentIndex : 0;
        int nextIndex = (startIndex + direction + FocusOrder.Length) % FocusOrder.Length;

        CelestialBody nextBody = FindBodyByName(FocusOrder[nextIndex]);
        if (nextBody != null)
        {
            FocusBody(nextBody);
        }
    }

    private int GetCurrentFocusIndex()
    {
        if (currentBody == null || string.IsNullOrWhiteSpace(currentBody.bodyName))
        {
            return -1;
        }

        for (int i = 0; i < FocusOrder.Length; i++)
        {
            if (FocusOrder[i] == currentBody.bodyName)
            {
                return i;
            }
        }

        return -1;
    }

    private static CelestialBody FindBodyByName(string bodyName)
    {
        if (string.IsNullOrWhiteSpace(bodyName))
        {
            return null;
        }

        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        foreach (CelestialBody body in bodies)
        {
            if (body != null && body.bodyName == bodyName)
            {
                return body;
            }
        }

        return null;
    }

    private void FocusBody(CelestialBody body)
    {
        if (body == null)
        {
            return;
        }

        bool bodyChanged = currentBody != null && currentBody != body;

        if (bodyChanged && solarAudioManager != null)
        {
            solarAudioManager.StopPlanetMusic();
            UpdateSoundButtonIcon(false);
        }

        currentBody = body;

        if (panel != null)
        {
            panel.SetActive(true);
        }

        UpdateFocusedUi();

        if (celestialBodyUiRoot != null && celestialBodyUiRoot.activeInHierarchy)
        {
            IsolateVisitedCelestialBody(body);
            ShowBasicsTab(true);
            SetActiveSection(CelestialSection.PlanetaryProfile);
        }

        SetCelestialMotionEnabled(false);
        SetOrbitLinesVisible(false);
        SetSimulationControlsVisible(false);

        body.ResetToDefaultOrientation();

        if (orbitCamera != null)
        {
            GetAutoFocusSettings(body.transform, out float autoFocusDistance, out float autoMinFocusDistance, out float autoMaxFocusDistance);
            body.GetFocusDistances(autoFocusDistance, autoMinFocusDistance, autoMaxFocusDistance,
                out float focusDistance, out float minFocusDistance, out float maxFocusDistance);
            Quaternion defaultViewRotation = GetDefaultFocusViewRotation(body);

            orbitCamera.FocusOnTarget(
                body.transform,
                focusDistance,
                minFocusDistance,
                maxFocusDistance,
                defaultViewRotation);
        }

        UpdateInspectCameraPanelOffset(activeSection.HasValue);
    }

    private void HandlePlanetaryProfileButton()
    {
        ToggleSection(CelestialSection.PlanetaryProfile);
    }

    private void HandleStatsButton()
    {
        ToggleSection(CelestialSection.Stats);
    }

    private void HandleOrbitCharacteristicsButton()
    {
        ToggleSection(CelestialSection.OrbitCharacteristics);
    }

    private void HandleStructureButton()
    {
        ToggleSection(CelestialSection.Structure);
    }

    private void HandleAtmosphereButton()
    {
        ToggleSection(CelestialSection.Atmosphere);
    }

    private void HandleBasicsButton()
    {
        ShowBasicsTab(true);
        SetActiveSection(CelestialSection.PlanetaryProfile);
    }

    private void HandleExtrasButton()
    {
        ShowExtrasTab(true);
        SetActiveSection(CelestialSection.Structure);
    }

    private void HandleImagesButton()
    {
        if (currentBody == null)
        {
            return;
        }

        PopulateGalleryForCurrentBody();

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(true);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        UpdateInspectCameraPanelOffset(false);
    }

    private void HandleSoundButton()
    {
        if (solarAudioManager == null || currentBody == null || string.IsNullOrWhiteSpace(currentBody.bodyName))
        {
            return;
        }

        solarAudioManager.TogglePlanetMusic(currentBody.bodyName);
        UpdateSoundButtonIcon(solarAudioManager.IsPlayingPlanetMusic(currentBody.bodyName));
    }

    private void UpdateSoundButtonIcon(bool isPlaying)
    {
        if (soundButtonIconAnimator != null)
        {
            soundButtonIconAnimator.SetPlaying(isPlaying);
        }

        if (soundButtonFill != null)
        {
            soundButtonFill.color = isPlaying ? activeBottomTabFillColor : soundButtonFillOriginalColor;
            soundButtonFill.SetAllDirty();
        }

        UpdateSoundTitle(isPlaying);
    }

    private void UpdateSoundTitle(bool isPlaying)
    {
        if (soundTitleText == null)
        {
            return;
        }

        bool shouldShow =
            isPlaying &&
            currentBody != null &&
            !string.IsNullOrWhiteSpace(currentBody.soundTitle);

        soundTitleText.gameObject.SetActive(shouldShow);

        if (shouldShow)
        {
            soundTitleText.text = currentBody.soundTitle;
        }
        else
        {
            soundTitleText.text = string.Empty;
        }
    }

    private void HandleGalleryBackButton()
    {
        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        SetImageCaptionPanelVisible(true);
        currentGalleryIndex = -1;
        UpdateInspectCameraPanelOffset(activeSection.HasValue);
    }

    private void HandleViewerCloseButton()
    {
        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(true);
        }

        SetImageCaptionPanelVisible(true);
    }

    private void HandleViewerPreviousButton()
    {
        ShowAdjacentGalleryEntry(-1);
    }

    private void HandleViewerNextButton()
    {
        ShowAdjacentGalleryEntry(1);
    }

    private void HandleImageCaptionHideButton()
    {
        SetImageCaptionPanelVisible(false);
    }

    private void HandleImageCaptionShowButton()
    {
        SetImageCaptionPanelVisible(true);
    }

    private void CloseGalleryOverlays()
    {
        if (imagesGalleryOverlay != null)
        {
            imagesGalleryOverlay.SetActive(false);
        }

        if (imageViewerOverlay != null)
        {
            imageViewerOverlay.SetActive(false);
        }

        SetImageCaptionPanelVisible(true);
        currentGalleryIndex = -1;
    }

    private void HandleHidePanelButton()
    {
        HideActiveSection();
    }

    private void HandleHideUnhideButton()
    {
        ToggleCelestialBodyUiVisibility();
    }

    private void SetCelestialMotionEnabled(bool isEnabled)
    {
        foreach (OrbitAroundSun orbitScript in orbitAroundSunScripts)
        {
            orbitScript.enabled = isEnabled;
        }

        foreach (OrbitAroundPlanet orbitScript in orbitAroundPlanetScripts)
        {
            orbitScript.enabled = isEnabled;
        }

        foreach (PlanetRotation rotationScript in rotationScripts)
        {
            rotationScript.enabled = isEnabled;
        }
    }

    private void SetOrbitLinesVisible(bool isVisible)
    {
        if (orbitSnakeTrails == null)
        {
            return;
        }

        foreach (OrbitSnakeTrail orbitSnakeTrail in orbitSnakeTrails)
        {
            if (orbitSnakeTrail == null)
            {
                continue;
            }

            LineRenderer lineRenderer = orbitSnakeTrail.GetComponent<LineRenderer>();
            if (lineRenderer != null)
            {
                lineRenderer.enabled = isVisible;
            }
        }
    }

    private void CacheCelestialRendererStates()
    {
        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        foreach (CelestialBody body in bodies)
        {
            if (body == null)
            {
                continue;
            }

            foreach (Renderer renderer in GetOwnedRenderers(body))
            {
                if (renderer != null && !originalCelestialRendererStates.ContainsKey(renderer))
                {
                    originalCelestialRendererStates[renderer] = renderer.enabled;
                }
            }
        }
    }

    private void IsolateVisitedCelestialBody(CelestialBody visibleBody)
    {
        CacheCelestialRendererStates();

        CelestialBody[] bodies = FindObjectsByType<CelestialBody>(FindObjectsSortMode.None);
        foreach (CelestialBody body in bodies)
        {
            if (body == null)
            {
                continue;
            }

            bool shouldShowBody = body == visibleBody;
            foreach (Renderer renderer in GetOwnedRenderers(body))
            {
                if (renderer == null)
                {
                    continue;
                }

                bool originalState = originalCelestialRendererStates.TryGetValue(renderer, out bool cachedState) ? cachedState : renderer.enabled;
                renderer.enabled = shouldShowBody && originalState;
            }
        }
    }

    private void RestoreAllCelestialBodyRenderers()
    {
        CacheCelestialRendererStates();

        foreach (KeyValuePair<Renderer, bool> rendererState in originalCelestialRendererStates)
        {
            if (rendererState.Key != null)
            {
                rendererState.Key.enabled = rendererState.Value;
            }
        }
    }

    private static IEnumerable<Renderer> GetOwnedRenderers(CelestialBody body)
    {
        if (body == null)
        {
            yield break;
        }

        Renderer[] renderers = body.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            if (renderer is LineRenderer)
            {
                continue;
            }

            CelestialBody owner = renderer.GetComponentInParent<CelestialBody>();
            if (owner == body)
            {
                yield return renderer;
            }
        }
    }

    private static bool IsCelestialBodySelectable(CelestialBody body)
    {
        if (body == null || !body.gameObject.activeInHierarchy)
        {
            return false;
        }

        foreach (Renderer renderer in GetOwnedRenderers(body))
        {
            if (renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPointerOverUi()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    private void SetSimulationControlsVisible(bool isVisible)
    {
        if (simulationControls != null)
        {
            simulationControls.SetActive(isVisible);
        }

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(isVisible);
        }

        if (speedSlider != null)
        {
            speedSlider.gameObject.SetActive(isVisible);
        }
    }

    private static Button FindButtonByName(string buttonName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button.name == buttonName)
            {
                return button;
            }
        }

        return null;
    }

    private static List<Button> FindButtonsByName(string buttonName)
    {
        List<Button> matchingButtons = new List<Button>();
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();

        foreach (Button button in buttons)
        {
            if (!button.gameObject.scene.IsValid())
            {
                continue;
            }

            if (button.name == buttonName)
            {
                matchingButtons.Add(button);
            }
        }

        return matchingButtons;
    }

    private static Image FindImageByName(string imageName)
    {
        Image[] images = FindObjectsByType<Image>(FindObjectsSortMode.None);

        foreach (Image image in images)
        {
            if (image.name == imageName)
            {
                return image;
            }
        }

        return null;
    }

    private static Button FindLastButtonByName(string buttonName)
    {
        Button[] buttons = Resources.FindObjectsOfTypeAll<Button>();
        Button lastMatch = null;

        foreach (Button button in buttons)
        {
            if (!button.gameObject.scene.IsValid())
            {
                continue;
            }

            if (button.name == buttonName)
            {
                lastMatch = button;
            }
        }

        return lastMatch;
    }

    private static TMP_Text FindTmpTextByName(string textName)
    {
        TMP_Text[] textComponents = Resources.FindObjectsOfTypeAll<TMP_Text>();

        foreach (TMP_Text textComponent in textComponents)
        {
            if (!textComponent.gameObject.scene.IsValid())
            {
                continue;
            }

            if (textComponent.name == textName)
            {
                return textComponent;
            }
        }

        return null;
    }

    private static T FindComponentInChildrenByName<T>(GameObject root, string objectName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component.name == objectName)
            {
                return component;
            }
        }

        return null;
    }

    private static List<T> FindComponentsInChildrenByName<T>(GameObject root, string objectName) where T : Component
    {
        List<T> matches = new List<T>();

        if (root == null)
        {
            return matches;
        }

        T[] components = root.GetComponentsInChildren<T>(true);
        foreach (T component in components)
        {
            if (component.name == objectName)
            {
                matches.Add(component);
            }
        }

        return matches;
    }

    private static T FindComponentUnderNamedParent<T>(GameObject root, string parentName, string childName) where T : Component
    {
        if (root == null)
        {
            return null;
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform transform in transforms)
        {
            if (transform.name != parentName)
            {
                continue;
            }

            T[] components = transform.GetComponentsInChildren<T>(true);
            foreach (T component in components)
            {
                if (component.name == childName)
                {
                    return component;
                }
            }
        }

        return null;
    }

    private static GameObject FindChildObjectByName(GameObject root, string objectName)
    {
        Transform transform = FindComponentInChildrenByName<Transform>(root, objectName);
        return transform != null ? transform.gameObject : null;
    }

    private static Button FindOrCreateButtonFromGraphic(GameObject root, string objectName)
    {
        Graphic targetGraphic = FindComponentInChildrenByName<Graphic>(root, objectName);

        if (targetGraphic == null)
        {
            return null;
        }

        Button button = targetGraphic.GetComponent<Button>();
        if (button == null)
        {
            button = targetGraphic.gameObject.AddComponent<Button>();
        }

        button.targetGraphic = targetGraphic;
        return button;
    }

    private static Button FindOrCreateButtonFromGraphic(string objectName)
    {
        GameObject targetObject = FindObjectByName(objectName);

        if (targetObject == null)
        {
            return null;
        }

        Button button = targetObject.GetComponent<Button>();
        if (button == null)
        {
            button = targetObject.AddComponent<Button>();
        }

        Graphic targetGraphic = targetObject.GetComponent<Graphic>();
        if (targetGraphic != null)
        {
            button.targetGraphic = targetGraphic;
        }

        return button;
    }

    private static List<Image> FindAllImagesByName(string imageName)
    {
        List<Image> matches = new List<Image>();
        Image[] images = Resources.FindObjectsOfTypeAll<Image>();

        foreach (Image image in images)
        {
            if (!image.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.Equals(image.name, imageName, System.StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(image);
            }
        }

        return matches;
    }

    private static Image FindChildImage(Button button, string nameFragment)
    {
        if (button == null)
        {
            return null;
        }

        Image[] images = button.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.gameObject == button.gameObject)
            {
                continue;
            }

            if (image.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return image;
            }
        }

        return null;
    }

    private static Image FindFirstChildImage(Button button)
    {
        if (button == null)
        {
            return null;
        }

        Image[] images = button.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image.gameObject != button.gameObject)
            {
                return image;
            }
        }

        return null;
    }

    private static GameObject FindObjectByName(string objectName)
    {
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();

        foreach (Transform transform in transforms)
        {
            if (!transform.gameObject.scene.IsValid())
            {
                continue;
            }

            if (transform.name == objectName)
            {
                return transform.gameObject;
            }
        }

        return null;
    }

    private static Sprite FindSpriteByName(string spriteName)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();

        foreach (Sprite sprite in sprites)
        {
            if (string.Equals(sprite.name, spriteName, System.StringComparison.OrdinalIgnoreCase))
            {
                return sprite;
            }
        }

        return null;
    }

    private void EnsureHideUnhideSpritesLoaded()
    {
#if UNITY_EDITOR
        if (hideIconSprite == null)
        {
            hideIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/hide.png");
        }

        if (viewIconSprite == null)
        {
            viewIconSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Icons/view.png");
        }
#endif
    }

    private void CacheHideUnhideIconImages()
    {
        hideUnhideIconImages.Clear();

        if (hideUnhideIconImage != null)
        {
            hideUnhideIconImages.Add(hideUnhideIconImage);
        }

        if (hideUnhideButton == null)
        {
            return;
        }

        Image[] images = hideUnhideButton.GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (image == null || image == hideUnhideButton.targetGraphic)
            {
                continue;
            }

            if (image.name.IndexOf("HideUnhide", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                image.name.IndexOf("Icon", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                if (!hideUnhideIconImages.Contains(image))
                {
                    hideUnhideIconImages.Add(image);
                }
            }
        }
    }

    private void ToggleSection(CelestialSection section)
    {
        if (activeSection.HasValue && activeSection.Value == section && IsSectionVisible(GetPanelForSection(section)))
        {
            HideActiveSection();
            return;
        }

        SetActiveSection(section);
    }

    private void HideActiveSection()
    {
        if (!activeSection.HasValue)
        {
            return;
        }

        GameObject activePanel = GetPanelForSection(activeSection.Value);

        if (activePanel == null || !activePanel.activeSelf)
        {
            activeSection = null;
            UpdateNavigationButtonVisuals(null);
            UpdateInspectCameraPanelOffset(false);
            return;
        }

        if (sectionAnimationCoroutine != null)
        {
            StopCoroutine(sectionAnimationCoroutine);
        }

        sectionAnimationCoroutine = StartCoroutine(AnimateSectionOut(activePanel));
        activeSection = null;
        UpdateNavigationButtonVisuals(null);
        UpdateInspectCameraPanelOffset(false);
    }

    private void ToggleCelestialBodyUiVisibility()
    {
        isCelestialBodyUiHidden = !isCelestialBodyUiHidden;

        if (isCelestialBodyUiHidden)
        {
            HideCelestialBodyUi();
        }
        else
        {
            RestoreCelestialBodyUi();
        }

        UpdateHideUnhideIcon();
    }

    private void HideCelestialBodyUi()
    {
        CloseGalleryOverlays();
        UpdateInspectCameraPanelOffset(false);

        if (celestialBodyUiRoot == null || hideUnhideButton == null)
        {
            return;
        }

        hiddenUiStateMap.Clear();

        Transform rootTransform = celestialBodyUiRoot.transform;
        Transform buttonTransform = hideUnhideButton.transform;

        foreach (Transform child in rootTransform)
        {
            if (child == null)
            {
                continue;
            }

            if (buttonTransform.IsChildOf(child))
            {
                foreach (Transform nestedChild in child)
                {
                    if (nestedChild == null || nestedChild == buttonTransform)
                    {
                        continue;
                    }

                    hiddenUiStateMap[nestedChild.gameObject] = nestedChild.gameObject.activeSelf;
                    nestedChild.gameObject.SetActive(false);
                }

                continue;
            }

            hiddenUiStateMap[child.gameObject] = child.gameObject.activeSelf;
            child.gameObject.SetActive(false);
        }
    }

    private void RestoreCelestialBodyUi()
    {
        foreach (KeyValuePair<GameObject, bool> state in hiddenUiStateMap)
        {
            if (state.Key != null)
            {
                state.Key.SetActive(state.Value);
            }
        }

        hiddenUiStateMap.Clear();
        UpdateInspectCameraPanelOffset(activeSection.HasValue);
    }

    private void UpdateHideUnhideIcon()
    {
        EnsureHideUnhideSpritesLoaded();

        Sprite targetSprite = isCelestialBodyUiHidden ? viewIconSprite : hideIconSprite;

        if (targetSprite == null)
        {
            return;
        }

        if (hideUnhideIconImage != null)
        {
            hideUnhideIconImage.overrideSprite = null;
            hideUnhideIconImage.sprite = targetSprite;
            hideUnhideIconImage.overrideSprite = targetSprite;
            hideUnhideIconImage.preserveAspect = true;
            hideUnhideIconImage.enabled = false;
            hideUnhideIconImage.enabled = true;
            hideUnhideIconImage.SetAllDirty();
        }

        if (hideUnhideIconImages.Count == 0)
        {
            hideUnhideIconImages.AddRange(FindAllImagesByName("HIdeUnhideIcon"));

            if (hideUnhideIconImages.Count == 0)
            {
                hideUnhideIconImages.AddRange(FindAllImagesByName("HideUnhideIcon"));
            }
        }

        foreach (Image iconImage in hideUnhideIconImages)
        {
            if (iconImage == null)
            {
                continue;
            }

            iconImage.overrideSprite = null;
            iconImage.sprite = targetSprite;
            iconImage.overrideSprite = targetSprite;
            iconImage.preserveAspect = true;
            iconImage.enabled = false;
            iconImage.enabled = true;
            iconImage.SetAllDirty();
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SetActiveSection(CelestialSection section)
    {
        GameObject selectedPanel = null;

        HideSectionImmediatelyExcept(section);

        switch (section)
        {
            case CelestialSection.PlanetaryProfile:
                selectedPanel = planetaryProfilePanel;
                break;
            case CelestialSection.Stats:
                selectedPanel = statsPanel;
                break;
            case CelestialSection.OrbitCharacteristics:
                selectedPanel = orbitCharacteristicsPanel;
                break;
            case CelestialSection.Structure:
                selectedPanel = structurePanel;
                break;
            case CelestialSection.Atmosphere:
                selectedPanel = atmospherePanel;
                break;
        }

        if (sectionAnimationCoroutine != null)
        {
            StopCoroutine(sectionAnimationCoroutine);
        }

        if (selectedPanel != null)
        {
            ResetScrollPositionForSection(section);
            sectionAnimationCoroutine = StartCoroutine(AnimateSectionIn(selectedPanel));
        }

        activeSection = section;
        UpdateNavigationButtonVisuals(section);
        UpdateInspectCameraPanelOffset(true);
    }

    private void UpdateInspectCameraPanelOffset(bool isPanelVisible)
    {
        if (orbitCamera == null)
        {
            return;
        }

        bool shouldOffset =
            isPanelVisible &&
            currentBody != null &&
            celestialBodyUiRoot != null &&
            celestialBodyUiRoot.activeInHierarchy &&
            !isCelestialBodyUiHidden &&
            (imagesGalleryOverlay == null || !imagesGalleryOverlay.activeInHierarchy) &&
            (imageViewerOverlay == null || !imageViewerOverlay.activeInHierarchy) &&
            showBasicsTab;

        orbitCamera.SetInspectViewOffsetEnabled(shouldOffset);
    }

    private static void SetSectionVisible(GameObject sectionObject, bool isVisible)
    {
        if (sectionObject != null)
        {
            sectionObject.SetActive(isVisible);
        }
    }

    private void HideSectionImmediatelyExcept(CelestialSection visibleSection)
    {
        HideSectionImmediately(planetaryProfilePanel, visibleSection == CelestialSection.PlanetaryProfile ? planetaryProfilePanelAnchoredPosition : GetStoredAnchoredPosition(CelestialSection.PlanetaryProfile));
        HideSectionImmediately(statsPanel, visibleSection == CelestialSection.Stats ? statsPanelAnchoredPosition : GetStoredAnchoredPosition(CelestialSection.Stats));
        HideSectionImmediately(orbitCharacteristicsPanel, visibleSection == CelestialSection.OrbitCharacteristics ? orbitCharacteristicsPanelAnchoredPosition : GetStoredAnchoredPosition(CelestialSection.OrbitCharacteristics));
        HideSectionImmediately(structurePanel, visibleSection == CelestialSection.Structure ? structurePanelAnchoredPosition : GetStoredAnchoredPosition(CelestialSection.Structure));
        HideSectionImmediately(atmospherePanel, visibleSection == CelestialSection.Atmosphere ? atmospherePanelAnchoredPosition : GetStoredAnchoredPosition(CelestialSection.Atmosphere));

        SetSectionVisible(planetaryProfilePanel, visibleSection == CelestialSection.PlanetaryProfile);
        SetSectionVisible(statsPanel, visibleSection == CelestialSection.Stats);
        SetSectionVisible(orbitCharacteristicsPanel, visibleSection == CelestialSection.OrbitCharacteristics);
        SetSectionVisible(structurePanel, visibleSection == CelestialSection.Structure);
        SetSectionVisible(atmospherePanel, visibleSection == CelestialSection.Atmosphere);
    }

    private GameObject GetPanelForSection(CelestialSection section)
    {
        switch (section)
        {
            case CelestialSection.PlanetaryProfile:
                return planetaryProfilePanel;
            case CelestialSection.Stats:
                return statsPanel;
            case CelestialSection.OrbitCharacteristics:
                return orbitCharacteristicsPanel;
            case CelestialSection.Structure:
                return structurePanel;
            case CelestialSection.Atmosphere:
                return atmospherePanel;
            default:
                return null;
        }
    }

    private static bool IsSectionVisible(GameObject sectionObject)
    {
        return sectionObject != null && sectionObject.activeSelf;
    }

    private IEnumerator AnimateSectionIn(GameObject sectionObject)
    {
        RectTransform rectTransform = sectionObject.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(sectionObject);

        if (rectTransform == null)
        {
            yield break;
        }

        Vector2 endPosition = GetStoredAnchoredPosition(GetSectionForPanel(sectionObject));
        Vector2 startPosition = endPosition + new Vector2(-sectionSlideOffset, 0f);

        sectionObject.SetActive(true);
        rectTransform.anchoredPosition = startPosition;
        canvasGroup.alpha = 0f;

        if (sectionSlideDuration <= 0.0001f)
        {
            rectTransform.anchoredPosition = endPosition;
            canvasGroup.alpha = 1f;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < sectionSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / sectionSlideDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, easedT);
            canvasGroup.alpha = easedT;
            yield return null;
        }

        rectTransform.anchoredPosition = endPosition;
        canvasGroup.alpha = 1f;
        sectionAnimationCoroutine = null;
    }

    private IEnumerator AnimateSectionOut(GameObject sectionObject)
    {
        RectTransform rectTransform = sectionObject.GetComponent<RectTransform>();
        CanvasGroup canvasGroup = GetOrAddCanvasGroup(sectionObject);

        if (rectTransform == null)
        {
            sectionObject.SetActive(false);
            yield break;
        }

        Vector2 startPosition = rectTransform.anchoredPosition;
        Vector2 endPosition = GetStoredAnchoredPosition(GetSectionForPanel(sectionObject)) + new Vector2(-sectionSlideOffset, 0f);

        if (sectionSlideDuration <= 0.0001f)
        {
            rectTransform.anchoredPosition = GetStoredAnchoredPosition(GetSectionForPanel(sectionObject));
            canvasGroup.alpha = 1f;
            sectionObject.SetActive(false);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < sectionSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / sectionSlideDuration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);
            rectTransform.anchoredPosition = Vector2.LerpUnclamped(startPosition, endPosition, easedT);
            canvasGroup.alpha = 1f - easedT;
            yield return null;
        }

        rectTransform.anchoredPosition = GetStoredAnchoredPosition(GetSectionForPanel(sectionObject));
        canvasGroup.alpha = 1f;
        sectionObject.SetActive(false);
        sectionAnimationCoroutine = null;
    }

    private void CacheSectionAnchoredPositions()
    {
        planetaryProfilePanelAnchoredPosition = GetPanelAnchoredPosition(planetaryProfilePanel);
        statsPanelAnchoredPosition = GetPanelAnchoredPosition(statsPanel);
        orbitCharacteristicsPanelAnchoredPosition = GetPanelAnchoredPosition(orbitCharacteristicsPanel);
        structurePanelAnchoredPosition = GetPanelAnchoredPosition(structurePanel);
        atmospherePanelAnchoredPosition = GetPanelAnchoredPosition(atmospherePanel);
    }

    private static Vector2 GetPanelAnchoredPosition(GameObject panelObject)
    {
        if (panelObject == null)
        {
            return Vector2.zero;
        }

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        return rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
    }

    private Vector2 GetStoredAnchoredPosition(CelestialSection section)
    {
        switch (section)
        {
            case CelestialSection.PlanetaryProfile:
                return planetaryProfilePanelAnchoredPosition;
            case CelestialSection.Stats:
                return statsPanelAnchoredPosition;
            case CelestialSection.OrbitCharacteristics:
                return orbitCharacteristicsPanelAnchoredPosition;
            case CelestialSection.Structure:
                return structurePanelAnchoredPosition;
            case CelestialSection.Atmosphere:
                return atmospherePanelAnchoredPosition;
            default:
                return Vector2.zero;
        }
    }

    private static void HideSectionImmediately(GameObject panelObject, Vector2 anchoredPosition)
    {
        if (panelObject == null)
        {
            return;
        }

        RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = anchoredPosition;
        }

        CanvasGroup canvasGroup = GetOrAddCanvasGroup(panelObject);
        canvasGroup.alpha = 1f;
    }

    private CelestialSection GetSectionForPanel(GameObject panelObject)
    {
        if (panelObject == planetaryProfilePanel)
        {
            return CelestialSection.PlanetaryProfile;
        }

        if (panelObject == statsPanel)
        {
            return CelestialSection.Stats;
        }

        if (panelObject == orbitCharacteristicsPanel)
        {
            return CelestialSection.OrbitCharacteristics;
        }

        if (panelObject == structurePanel)
        {
            return CelestialSection.Structure;
        }

        return CelestialSection.Atmosphere;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject panelObject)
    {
        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup = panelObject.AddComponent<CanvasGroup>();
        }

        return canvasGroup;
    }

    private void CacheNavigationVisualDefaults()
    {
        CacheButtonGraphics(planetaryProfileButton);
        CacheButtonGraphics(statsButton);
        CacheButtonGraphics(orbitCharacteristicsButton);
        CacheButtonGraphics(structureButton);
        CacheButtonGraphics(atmosphereButton);

        DisableNavigationButtonTransitions();
    }

    private void CacheBottomTabVisualDefaults()
    {
        CacheButtonGraphics(basicsButton);
        CacheButtonGraphics(extrasButton);
        CacheButtonGraphics(soundButton);

        if (basicsButtonFill != null)
        {
            basicsButtonFillOriginalColor = basicsButtonFill.color;
        }

        if (extrasButtonFill != null)
        {
            extrasButtonFillOriginalColor = extrasButtonFill.color;
        }

        if (soundButtonFill != null)
        {
            soundButtonFillOriginalColor = soundButtonFill.color;
        }

        DisableButtonTransition(basicsButton);
        DisableButtonTransition(extrasButton);
        DisableButtonTransition(soundButton);
    }

    private void CacheButtonGraphics(Button button)
    {
        if (button == null)
        {
            return;
        }

        Graphic targetGraphic = button.targetGraphic;
        if (targetGraphic != null && !originalGraphicColors.ContainsKey(targetGraphic))
        {
            originalGraphicColors[targetGraphic] = targetGraphic.color;
        }

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            if (!originalGraphicColors.ContainsKey(graphic))
            {
                originalGraphicColors[graphic] = graphic.color;
            }
        }
    }

    private void DisableNavigationButtonTransitions()
    {
        DisableButtonTransition(planetaryProfileButton);
        DisableButtonTransition(statsButton);
        DisableButtonTransition(orbitCharacteristicsButton);
        DisableButtonTransition(structureButton);
        DisableButtonTransition(atmosphereButton);
    }

    private static void DisableButtonTransition(Button button)
    {
        if (button == null)
        {
            return;
        }

        button.transition = Selectable.Transition.None;
    }

    private void UpdateNavigationButtonVisuals(CelestialSection? selectedSection)
    {
        SetNavigationButtonVisual(planetaryProfileButton, selectedSection == CelestialSection.PlanetaryProfile);
        SetNavigationButtonVisual(statsButton, selectedSection == CelestialSection.Stats);
        SetNavigationButtonVisual(orbitCharacteristicsButton, selectedSection == CelestialSection.OrbitCharacteristics);
        SetNavigationButtonVisual(structureButton, selectedSection == CelestialSection.Structure);
        SetNavigationButtonVisual(atmosphereButton, selectedSection == CelestialSection.Atmosphere);
    }

    private void ShowBasicsTab(bool refreshSectionAvailability)
    {
        showBasicsTab = true;
        UpdateBottomTabVisuals();

        if (refreshSectionAvailability)
        {
            UpdateSectionAvailability(currentBody);
        }
    }

    private void ShowExtrasTab(bool refreshSectionAvailability)
    {
        showBasicsTab = false;
        UpdateBottomTabVisuals();

        if (refreshSectionAvailability)
        {
            UpdateSectionAvailability(currentBody);
        }
    }

    private void UpdateBottomTabVisuals()
    {
        if (basicsGroup != null)
        {
            basicsGroup.SetActive(showBasicsTab);
        }

        if (extrasGroup != null)
        {
            extrasGroup.SetActive(!showBasicsTab);
        }

        if (basicsButtonFill != null)
        {
            basicsButtonFill.color = showBasicsTab ? activeBottomTabFillColor : basicsButtonFillOriginalColor;
            basicsButtonFill.SetAllDirty();
        }

        if (extrasButtonFill != null)
        {
            extrasButtonFill.color = showBasicsTab ? extrasButtonFillOriginalColor : activeBottomTabFillColor;
            extrasButtonFill.SetAllDirty();
        }
    }

    private void SetNavigationButtonVisual(Button button, bool isActive)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rectTransform = button.transform as RectTransform;
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * (isActive ? navButtonActiveScale : navButtonInactiveScale);
        }

        Graphic targetGraphic = button.targetGraphic;
        if (targetGraphic != null)
        {
            Color targetColor = isActive ? navButtonActiveColor : GetOriginalColor(targetGraphic);
            targetGraphic.color = targetColor;
        }

        Graphic[] graphics = button.GetComponentsInChildren<Graphic>(true);
        foreach (Graphic graphic in graphics)
        {
            Color originalColor = GetOriginalColor(graphic);
            Color appliedColor = originalColor;

            if (graphic == targetGraphic)
            {
                appliedColor = isActive ? navButtonActiveColor : originalColor;
            }
            else
            {
                appliedColor.a = originalColor.a * (isActive ? 1f : 0.92f);
            }

            graphic.color = appliedColor;
        }
    }

    private Color GetOriginalColor(Graphic graphic)
    {
        if (graphic != null && originalGraphicColors.TryGetValue(graphic, out Color originalColor))
        {
            return originalColor;
        }

        return graphic != null ? graphic.color : Color.white;
    }

    private void GetAutoFocusSettings(Transform bodyTransform, out float focusDistance, out float minFocusDistance, out float maxFocusDistance)
    {
        Renderer bodyRenderer = bodyTransform.GetComponent<Renderer>();

        if (bodyRenderer == null)
        {
            bodyRenderer = bodyTransform.GetComponentInChildren<Renderer>();
        }

        if (bodyRenderer == null)
        {
            focusDistance = 1.2f;
            minFocusDistance = 0.5f;
            maxFocusDistance = 4f;
            return;
        }

        Bounds bounds = bodyRenderer.bounds;
        float radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        float fieldOfView = mainCamera != null ? mainCamera.fieldOfView : 60f;
        float halfFovRadians = Mathf.Deg2Rad * fieldOfView * 0.5f;

        // Back off enough that the body starts comfortably inside frame.
        float fillScreenDistance = radius / Mathf.Tan(halfFovRadians) * 1.6f;

        // Keep the near clip plane in front of the surface so we don't cut into the planet.
        float safeMinimumDistance = radius + 0.01f;

        focusDistance = Mathf.Max(fillScreenDistance, safeMinimumDistance + 0.15f);
        minFocusDistance = safeMinimumDistance;
        maxFocusDistance = 30f;
    }

    private Quaternion GetDefaultFocusViewRotation(CelestialBody body)
    {
        OrbitAroundPlanet orbitAroundPlanet = body.GetComponent<OrbitAroundPlanet>();

        if (orbitAroundPlanet != null && orbitAroundPlanet.target != null)
        {
            Vector3 outwardDirection = (body.transform.position - orbitAroundPlanet.target.position).normalized;

            if (outwardDirection.sqrMagnitude > 0.0001f)
            {
                return Quaternion.LookRotation(-outwardDirection, body.GetDefaultWorldRotation() * Vector3.up);
            }
        }

        Quaternion defaultBodyRotation = body.GetDefaultWorldRotation();
        return Quaternion.LookRotation(
            defaultBodyRotation * Vector3.forward,
            defaultBodyRotation * Vector3.up);
    }
}
