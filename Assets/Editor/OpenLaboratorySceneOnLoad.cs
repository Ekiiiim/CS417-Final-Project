using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenLaboratorySceneOnLoad
{
    private const string LaboratoryScenePath = "Assets/Laboratory/Scenes/Laboratory.unity";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";

    static OpenLaboratorySceneOnLoad()
    {
        EditorApplication.delayCall += OpenLaboratorySceneIfNeeded;
    }

    private static void OpenLaboratorySceneIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.path == LaboratoryScenePath)
        {
            return;
        }

        if (!string.IsNullOrEmpty(activeScene.path) && activeScene.path != SampleScenePath)
        {
            return;
        }

        EditorSceneManager.OpenScene(LaboratoryScenePath, OpenSceneMode.Single);
    }
}
