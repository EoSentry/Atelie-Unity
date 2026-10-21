using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>Estilo de rota (estrada, trilha, muralha, encosta…). Usado na etapa 2.</summary>
    [CreateAssetMenu(menuName = "Atelie/Estilo de rota", fileName = "Rota")]
    public class PathStyleDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public string group = "road";
        [Tooltip("Textura que se repete AO LONGO da rota (a largura da imagem vira a largura da rota).")]
        public Texture2D bodyTexture;
        [Tooltip("Textura/sprite das bordas (opcional).")]
        public Texture2D edgeTexture;
        public float defaultWidth = 8f;
        public bool taperable;
        [Tooltip("Funde com outras rotas do mesmo grupo nos cruzamentos.")]
        public bool fuseJunctions = true;
        public bool carvesWater;

        public string Id { get { return string.IsNullOrEmpty(id) ? name : id; } }
    }
}
