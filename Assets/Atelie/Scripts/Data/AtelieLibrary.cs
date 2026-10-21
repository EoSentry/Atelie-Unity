using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>Biblioteca: reúne tudo que o editor pode usar. Crie uma em Assets > Create > Atelie > Biblioteca.</summary>
    [CreateAssetMenu(menuName = "Atelie/Biblioteca", fileName = "Biblioteca")]
    public class AtelieLibrary : ScriptableObject
    {
        public List<TerrainDef> terrains = new List<TerrainDef>();
        public List<ObjectDef> objects = new List<ObjectDef>();
        public List<PathStyleDef> pathStyles = new List<PathStyleDef>();

        public TerrainDef FindTerrain(string id)
        {
            for (int i = 0; i < terrains.Count; i++) if (terrains[i] != null && terrains[i].Id == id) return terrains[i];
            return null;
        }
        public ObjectDef FindObject(string id)
        {
            for (int i = 0; i < objects.Count; i++) if (objects[i] != null && objects[i].Id == id) return objects[i];
            return null;
        }
        public PathStyleDef FindPathStyle(string id)
        {
            for (int i = 0; i < pathStyles.Count; i++) if (pathStyles[i] != null && pathStyles[i].Id == id) return pathStyles[i];
            return null;
        }
        public List<TerrainDef> Land() { return terrains.FindAll(t => t != null && !t.isSea); }
        public List<TerrainDef> Sea() { return terrains.FindAll(t => t != null && t.isSea); }
    }
}
