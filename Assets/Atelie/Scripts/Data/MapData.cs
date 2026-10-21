using System;
using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>
    /// Modelo de dados do mapa (tudo que NÃO são pixels do terreno/mar).
    /// Convenção de coordenadas: pixels do mapa, origem no canto INFERIOR esquerdo, Y para cima
    /// (igual às Texture2D da Unity). O importador do app web converte o Y.
    /// Todas as classes são [Serializable] para funcionar com JsonUtility.
    /// </summary>

    [Serializable] public class StatEntry { public string label; public string value; }

    /// <summary>Dados anexados a um objeto (aparecem ao clicar): nome, facção, números, notas, selo.</summary>
    [Serializable]
    public class ObjInfo
    {
        public string name;
        public string faction;
        public string factionColor;
        public List<StatEntry> stats = new List<StatEntry>();
        public string notes;
        public int badge;
    }

    [Serializable]
    public class ObjData
    {
        public int uid;
        public string id;            // id do ObjectDef
        public float x, y;           // base-centro do sprite, em pixels do mapa
        public float scale = 1f;
        public float rot;            // graus
        public bool flip;
        public int variant;          // índice da variação (quando o ObjectDef tem várias)
        public float zOffset;        // ajuste manual da ordenação em Y
        public bool hasInfo;
        public ObjInfo info = new ObjInfo();
        public string lightKind;     // "" = sem luz
        public float lightIntensity = 1f;
        public string subMapId;      // "Mapa do local": id do mapa filho (vazio = não tem)
        public string subMapName;
    }

    [Serializable]
    public class PathData
    {
        public int uid;
        public string style;         // id do PathStyleDef
        public float width = 8f;
        public bool taper;
        public bool flip;
        public int palette;
        public List<Vector2> points = new List<Vector2>();
    }

    [Serializable]
    public class LabelData
    {
        public int uid;
        public string text = "Nome do lugar";
        public float x, y;
        public string font;
        public float size = 44f;
        public Color color = new Color(.23f, .16f, .09f, 1f);
        public float spacing = 4f;
        public float curve;
        public float rot;
        public bool halo = true;
        public string bgId;          // moldura/pergaminho de fundo do texto
        public Color bgColor = Color.white;
        public float bgWidth;
    }

    [Serializable] public class CellData { public int q, r; public Color color; }

    [Serializable]
    public class VectorData
    {
        public List<ObjData> objects = new List<ObjData>();
        public List<PathData> paths = new List<PathData>();
        public List<LabelData> labels = new List<LabelData>();
        public List<CellData> cells = new List<CellData>();
        public int nextUid = 1;
    }

    [Serializable]
    public class ShoalSettings
    {
        public bool on = true;
        public float width = 44f;        // largura da faixa rasa, em pixels do mapa
        public float intensity = .5f;    // 0..1
        public bool lines = true;        // linhas de ondulação
        public int tone = 0;             // índice em ShoalBuilder.Tones
    }

    [Serializable]
    public class FxSettings
    {
        public string preset = "none";
        public float sepia, gray, hue, vignette, grain, stains, folds, burn, fibre;
        public float saturation = 100f, brightness = 100f, contrast = 100f;
        public float gradeTemp;                         // -100 frio … +100 quente
        public Color gradeColor = Color.white; public float gradeAmt;
        public Color shadowColor = Color.black; public float shadowAmt;
        public Color highColor = Color.white; public float highAmt;
        public float fog;
        public int seed = 7;
    }

    [Serializable]
    public class ShapeSettings
    {
        public string type = "rect";     // rect, scroll1, scroll2, globe, moll, eck4, winkel, robinson
        public float round;              // arredondar cantos (0..1)
        public string orient = "h";
        public bool graticule = true;
        public float gridStep = 30f, gridLon = -20f, gridLat = 18f;
    }

    [Serializable]
    public class FrameSettings
    {
        public string style = "line";    // id do FrameDef ou "none"
        public int palette;
        public float thickness = 26f;
        public bool vignette = true, inner = true;
        public bool title; public string titleText = ""; public float titleSize = 72f; public string titlePos = "tl";
        public bool scale; public string scalePos = "tl"; public string scaleUnit = "";
        public int seed = 5;
    }

    [Serializable]
    public class GridSettings
    {
        public string type = "none";     // none, hex, square
        public float size = 100f;
        public string orient = "pointy";
        public Color color = new Color(.08f, .06f, .04f, 1f);
        public float alpha = .4f, lineWidth = 1.2f, dash;
        public float offsetX, offsetY;
        public bool coords, snap;
    }

    [Serializable]
    public class AmbientSettings
    {
        public string season = "none";   // none, spring, summer, autumn, winter
        public float seasonAmt = 80f;
        public string night = "none";
        public float nightAmt = 100f;
        public bool auto = true, emit = true;
    }

    [Serializable]
    public class TableSettings { public string id = "default"; public float scale = 1f, dim = 100f; public bool vignette = true; }

    /// <summary>Documento completo (menos os pixels, que ficam em sea.png / land.png).</summary>
    [Serializable]
    public class MapDocument
    {
        public int version = 1;
        public string name = "Novo mapa";
        public string mapId;             // id usado para achar este mapa dentro de um objeto (sub-mapas)
        public string parentMapId;       // quando é "Mapa do local"
        public int parentObjUid;
        public int width = 3072, height = 2048;
        public string seaTexture = "sea_mid";
        public float metersPerPixel = .5f; public string unit = "km";
        public VectorData vec = new VectorData();
        public ShoalSettings shoal = new ShoalSettings();
        public FxSettings fx = new FxSettings();
        public ShapeSettings shape = new ShapeSettings();
        public FrameSettings frame = new FrameSettings();
        public GridSettings grid = new GridSettings();
        public AmbientSettings ambient = new AmbientSettings();
        public TableSettings table = new TableSettings();

        public int NextUid() { return vec.nextUid++; }
    }
}
