#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
public static class AvatarEditorPreview
{
    public static void Run()
    {EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.EnterPlaymode();}
}
#endif
