using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Terreno (grama, areia, neve…) ou textura de mar. Você cria pelo menu
    /// Assets > Create > Atelie > Terreno ou Mar e arrasta a SUA textura (tileável).
    /// A textura precisa estar com "Read/Write" ligado no Import Settings
    /// (se não estiver, o código tenta ler mesmo assim, só fica um pouco mais lento ao carregar).
    /// </summary>
    [CreateAssetMenu(menuName = "Atelie/Terreno ou Mar", fileName = "Terreno")]
    public class TerrainDef : ScriptableObject
    {
        [Tooltip("Identificador único (sem espaços). Vai para o arquivo salvo.")]
        public string id;
        public string displayName;
        public string group = "Terra";
        [Tooltip("Marque se for uma textura de MAR (vai para a camada do mar).")]
        public bool isSea;
        [Tooltip("Textura tileável (repete sem emenda). 512 ou 1024 px funciona bem.")]
        public Texture2D texture;
        [Tooltip("Quantos pixels do MAPA a textura cobre antes de repetir. 512 = tamanho natural se a textura tiver 512 px.")]
        public float tileSize = 512f;
        public Color tint = Color.white;
        [Tooltip("Ícone para o botão do menu (opcional).")]
        public Sprite icon;

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }
        public string Label { get { return string.IsNullOrEmpty(displayName) ? Id : displayName; } }
    }
}
