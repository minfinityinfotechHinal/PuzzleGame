using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Canvases")]
    public GameObject startScreen;
    public GameObject gameplayScreen;
    public GameObject completeScreen; // optional

    [Header("Level Settings")]
    public int currentLevel = 0;

    private const string LEVEL_KEY = "CURRENT_LEVEL";

     private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void LoadSavedLevel()
    {
        currentLevel = PlayerPrefs.GetInt(LEVEL_KEY, 0);
    }

    void Start()
    {
        ShowStartScreen();
    }

     // ---------------- START LEVEL ----------------
    public void PlayLevel(int rows, int cols)
    {
        // 👉 Switch UI
        startScreen.SetActive(false);
        gameplayScreen.SetActive(true);

        // 👉 Start puzzle
        PuzzleManager.Instance.StartLevel(rows, cols);
    }
    
      // ---------------- START LEVEL ----------------
   public void PlayDefaultLevel()
    {
        // 👉 Switch UI (THIS WAS MISSING)
        startScreen.SetActive(false);
        gameplayScreen.SetActive(true);

        // 👉 Start puzzle
        PuzzleManager.Instance.StartLevel(5, 10);
    }
    // ---------------- BACK BUTTON ----------------
    public void BackToMenu()
    {
        ShowStartScreen();
    }

    // ---------------- USING INDEX (optional) ----------------
    public void PlayLevelByIndex(int index)
    {
        startScreen.SetActive(false);
        gameplayScreen.SetActive(true);

        PuzzleManager.Instance.LoadLevel(index);
    }


    // ---------------- START SCREEN ----------------
    public void ShowStartScreen()
    {
        startScreen.SetActive(true);
        gameplayScreen.SetActive(false);

        if (completeScreen != null)
            completeScreen.SetActive(false);
    }

    void SaveLevel()
    {
        PlayerPrefs.SetInt(LEVEL_KEY, currentLevel);
        PlayerPrefs.Save();
    }

    // ---------------- LOAD LEVEL ----------------
    public void LoadLevel(int levelIndex)
    {
        currentLevel = levelIndex;
        SaveLevel();

        PuzzleManager.Instance.LoadLevel(levelIndex);
    }

    // ---------------- NEXT ----------------
    public void NextLevel()
    {
        currentLevel++;
        SaveLevel();

        PuzzleManager.Instance.LoadLevel(currentLevel);
    }

    // ---------------- REPLAY ----------------
    public void ReplayLevel()
{
    PuzzleManager.Instance.StartLevel(
        PuzzleManager.Instance.rows,
        PuzzleManager.Instance.cols
    );
}
}