using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scene flow (map-setup-plan.txt Q3/Q4):
///   - the MENU scene is the start screen of the game (first thing the player sees)
///   - START GAME loads the game scene with the chosen setup
///   - a "MENU" button in-game goes back to the setup screen
///   - starting the game scene directly (without the menu) redirects to the menu first
/// The chosen setup survives scene loads for this play session (statics).
/// </summary>
public static class GameFlow
{
    public const string MenuScene = "Assets/Scenes/MenuScene.unity";
    public const string GameScene = "Assets/Scenes/SampleScene.unity";

    /// <summary>The setup the player last started a game with (pre-fills the menu).</summary>
    public static GameSetup LastSetup;
    /// <summary>The premade map of that setup (null = random map).</summary>
    public static MapDefinition LastMap;

    static bool menuOpened;

    public static bool CameFromMenu { get { return menuOpened; } }

    /// <summary>Called by MenuView on start, so the game scene knows to boot into a game.</summary>
    public static void StartMenu()
    {
        menuOpened = true;
    }

    public static void OpenMenu()
    {
        if (Application.isBatchMode) return;
        SceneManager.LoadScene(MenuScene);
    }

    public static void OpenGame()
    {
        menuOpened = true;
        if (Application.isBatchMode) return;
        SceneManager.LoadScene(GameScene);
    }
}
