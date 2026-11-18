using UnityEditor;
using UnityEngine;

/// <summary>
/// Liga os ícones de Assets/Art/Icons (Tools/Blender/build_item_icons.py) nos itens de
/// Assets/ScriptableObjects/Items: o ícone com o mesmo nome do asset vai para ItemData.icon.
/// Menu: Jogo/Art/Ligar ícones nos itens.
/// </summary>
public static class AssignItemIcons
{
    [MenuItem("Jogo/Art/Ligar ícones nos itens")]
    public static void Run()
    {
        int ok = 0; string missing = "";
        // varre os arquivos (a busca por tipo do AssetDatabase pula assets escritos à mão em YAML)
        foreach (var path in System.IO.Directory.GetFiles("Assets/ScriptableObjects/Items", "*.asset"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(path.Replace(System.IO.Path.DirectorySeparatorChar, '/'));
            if (item == null) continue; // ItemDatabase etc.
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Icons/{item.name}.png");
            if (sprite == null) { missing += item.name + " "; continue; }
            var so = new SerializedObject(item);
            so.FindProperty("icon").objectReferenceValue = sprite;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            ok++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[AssignItemIcons] {ok} itens com ícone; sem ícone: [{missing}]");
    }
}
