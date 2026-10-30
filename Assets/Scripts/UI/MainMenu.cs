using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Головне меню (сцена 0). Enter/Space - почати, Esc - вийти.
/// Після проходження всіх рівнів показує екран перемоги зі статистикою.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameTitle = "ПІСЛЯ ЗАКРИТТЯ";
    [SerializeField] string subtitle = "Нічний універмаг. Ти - прозорий привид. Вибирайся, заманюючи охорону.";
    [SerializeField] string credits = "Зроблено на Halloween Game Jam";
    [SerializeField] Sprite[] ghostFrames = new Sprite[0];
    [SerializeField] Sprite[] guardFrames = new Sprite[0];
    [SerializeField] AudioClip music;
    [SerializeField] AudioClip startClip;

    Image ghostImage, guardImage;
    Text pressText;
    float time;
    bool starting;
    AudioSource audioSource;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.volume = 0.35f;
        audioSource.clip = music;
        if (music != null) audioSource.Play();
        Build();
    }

    void Build()
    {
        var root = UIFactory.CreateCanvas(transform, "Menu").transform;
        Vector2 c = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);

        UIFactory.MakeText(root, "Title", gameTitle, 110, top, new Vector2(0f, -120f), new Vector2(1700f, 140f),
            TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f));
        UIFactory.MakeText(root, "Subtitle", subtitle, 30, top, new Vector2(0f, -250f), new Vector2(1400f, 80f),
            TextAnchor.UpperCenter, new Color(0.75f, 0.8f, 0.95f));

        ghostImage = UIFactory.MakeImage(root, "Ghost", c, new Vector2(0f, 30f), new Vector2(240f, 240f), new Color(1f, 1f, 1f, 0.85f),
            ghostFrames.Length > 0 ? ghostFrames[0] : null);
        guardImage = UIFactory.MakeImage(root, "Guard", bottom, new Vector2(-700f, 150f), new Vector2(160f, 160f), Color.white,
            guardFrames.Length > 0 ? guardFrames[0] : null);
        guardImage.enabled = guardFrames.Length > 0;
        ghostImage.enabled = ghostFrames.Length > 0;

        string info;
        if (GameProgress.JustWon)
        {
            info = $"ТИ ВТІК З УНІВЕРМАГУ!\nЧас: {GameProgress.FormatTime(GameProgress.RunTime)}    Спіймали: {GameProgress.TimesCaught} раз(ів)";
            UIFactory.MakeText(root, "Win", info, 44, c, new Vector2(0f, -170f), new Vector2(1500f, 130f),
                TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.6f));
        }

        pressText = UIFactory.MakeText(root, "Press", GameProgress.JustWon ? "ENTER - грати ще раз" : "ENTER - почати",
            48, c, new Vector2(0f, -280f), new Vector2(1200f, 70f), TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f));

        UIFactory.MakeText(root, "Controls",
            "WASD - летіти     E - використати приманку     R - заново     Esc - меню / вихід\n" +
            "У темряві тебе не видно. Світло ліхтарів і ламп тебе виказує.",
            26, bottom, new Vector2(0f, 60f), new Vector2(1600f, 90f), TextAnchor.LowerCenter, new Color(1f, 1f, 1f, 0.7f));
        UIFactory.MakeText(root, "Credits", credits, 20, new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(800f, 30f),
            TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.4f));
    }

    void Update()
    {
        time += Time.deltaTime;

        if (ghostFrames.Length > 0)
        {
            ghostImage.sprite = ghostFrames[(int)(time * 6f) % ghostFrames.Length];
            ghostImage.rectTransform.anchoredPosition = new Vector2(0f, 30f + Mathf.Sin(time * 2f) * 12f);
            var col = ghostImage.color;
            col.a = 0.55f + 0.35f * Mathf.Sin(time * 1.3f);
            ghostImage.color = col;
        }

        if (guardFrames.Length > 0)
        {
            // сторож ходить туди-сюди внизу екрана
            float x = Mathf.PingPong(time * 180f, 1400f) - 700f;
            bool right = Mathf.Repeat(time * 180f, 2800f) < 1400f;
            guardImage.rectTransform.anchoredPosition = new Vector2(x, 150f);
            guardImage.sprite = guardFrames[(int)(time * 8f) % guardFrames.Length];
            guardImage.rectTransform.localScale = new Vector3(right ? 1f : -1f, 1f, 1f);
        }

        pressText.enabled = Mathf.Repeat(time, 1f) < 0.7f;

        if (starting) return;

        if (GameInput.StartPressed)
        {
            starting = true;
            GameProgress.ResetRun();
            if (startClip != null) audioSource.PlayOneShot(startClip);
            Invoke(nameof(LoadFirstLevel), 0.5f);
        }
        else if (GameInput.BackPressed)
        {
            Application.Quit();
        }
    }

    void LoadFirstLevel()
    {
        if (SceneManager.sceneCountInBuildSettings > 1) SceneManager.LoadScene(1);
    }
}
