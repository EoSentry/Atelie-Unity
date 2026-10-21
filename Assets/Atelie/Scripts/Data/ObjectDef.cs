using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Objeto colocável (casa, árvore, castelo, criatura…). Cada variação é um Sprite.
    /// No Import Settings do sprite: Pivot = Bottom Center (a "base" do objeto é onde ele pisa no mapa).
    /// </summary>
    [CreateAssetMenu(menuName = "Atelie/Objeto", fileName = "Objeto")]
    public class ObjectDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public string category = "Povoados";
        [Tooltip("Variações visuais (o editor alterna entre elas ao colocar). Pelo menos 1.")]
        public List<Sprite> variants = new List<Sprite>();
        [Tooltip("Largura em pixels do mapa quando a escala for 1.")]
        public float baseWidth = 100f;
        public bool canFlip = true;
        [Tooltip("Grupo para o modo 'misturar' (ex.: pinheiros). Objetos do mesmo grupo se alternam.")]
        public string group;
        [Tooltip("Pode abrir um 'Mapa do local' dentro dele (cidade, castelo, masmorra…).")]
        public bool canHaveSubMap;
        [Tooltip("Ícone para o menu (opcional; se vazio usa a primeira variação).")]
        public Sprite icon;

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }
        public string Label { get { return string.IsNullOrEmpty(displayName) ? Id : displayName; } }
        public Sprite Icon { get { return icon != null ? icon : (variants.Count > 0 ? variants[0] : null); } }
    }
}
