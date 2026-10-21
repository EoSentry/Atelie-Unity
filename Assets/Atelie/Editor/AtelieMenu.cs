#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Atelie.EditorTools
{
    /// <summary>Menu Atelie no editor da Unity: cria a cena de teste com um clique.</summary>
    // Adiciona o menu 'Atelie' à barra do Unity (só existe dentro do editor, não vai para o jogo final).
    public static class AtelieMenu
    {
        [MenuItem("Atelie/Criar cena de teste")]
        // Cria uma cena nova e vazia com um GameObject 'Atelie' que tem o AtelieBootstrap.
        public static void CreateTestScene()
        {
            Scene sc = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject go = new GameObject("Atelie");
            go.AddComponent<AtelieBootstrap>();
            EditorSceneManager.MarkSceneDirty(sc);
            Selection.activeGameObject = go;
            Debug.Log("Cena criada. Salve (Ctrl+S) em Assets/Scenes e aperte Play.");
        }

        [MenuItem("Atelie/Adicionar bootstrap à cena atual")]
        // Adiciona o mesmo GameObject à cena que você já tem aberta.
        public static void AddBootstrap()
        {
            GameObject go = new GameObject("Atelie");
            go.AddComponent<AtelieBootstrap>();
            Selection.activeGameObject = go;
        }
    }
}
#endif
