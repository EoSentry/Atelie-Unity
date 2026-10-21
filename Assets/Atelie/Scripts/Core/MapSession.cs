using System.Collections.Generic;
using UnityEngine;

namespace Atelie
{
    /// <summary>Qualquer ação que pode ser desfeita e refeita.</summary>
    public interface IUndoEntry
    {
        void Undo(MapSession s);
        void Redo(MapSession s);
    }

    /// <summary>Desfazer de pintura: guarda os blocos de pixels de antes/depois de um traço.</summary>
    public sealed class RasterUndo : IUndoEntry
    {
        public List<RasterPatch> patches = new List<RasterPatch>();
        public void Undo(MapSession s) { Apply(s, false); }
        public void Redo(MapSession s) { Apply(s, true); }

        void Apply(MapSession s, bool after)
        {
            RectInt box = new RectInt(0, 0, 0, 0);
            for (int i = 0; i < patches.Count; i++)
            {
                patches[i].Apply(after);                                        // restaura os pixels
                if (patches[i].layer == s.land) box = BrushEngine.Union(box, patches[i].Bounds());
            }
            if (box.width > 0) s.OnLandChanged(box);   // terra mudou: refaz contorno e água rasa
            s.RaiseChanged();
        }
    }

    /// <summary>Desfazer de vetores (objetos, rotas, textos): guarda o texto JSON de antes/depois.</summary>
    public sealed class VectorUndo : IUndoEntry
    {
        public string before, after;
        public void Undo(MapSession s) { s.SetVector(before); }
        public void Redo(MapSession s) { s.SetVector(after); }
    }

    /// <summary>
    /// Tudo que pertence a UM mapa aberto: o documento (dados), as camadas de pixels e o histórico
    /// de desfazer. Camadas: sea (mar, opaca) · shoal (água rasa, resolução reduzida) ·
    /// coast (contorno) · land (terra, com transparência).
    /// </summary>
    public sealed class MapSession
    {
        public MapDocument doc;                  // dados do mapa (nome, objetos, configurações…)
        public PixelLayer sea, land, coast;
        public PixelLayer shoal;                 // tem resolução menor (ver ShoalScale)
        public float ShoalScale = 1f;            // largura do shoal / largura do mapa
        public int W { get { return doc.width; } }
        public int H { get { return doc.height; } }

        public readonly List<IUndoEntry> undo = new List<IUndoEntry>();   // histórico
        public int undoIndex;                    // posição atual no histórico
        public int maxUndo = 60;                 // quantos passos guardar

        public event System.Action Changed;              // algo mudou (a UI pode marcar "não salvo")
        public event System.Action<RectInt> LandChanged; // a terra mudou nesta região
        public event System.Action Rebuilt;              // as camadas foram recriadas (novo mapa / abrir)

        public bool shoalDirty;                  // true = a água rasa precisa ser recalculada

        /// <summary>Maior lado de mapa permitido (limite da placa de vídeo, no máximo 8192).</summary>
        public static int MaxSide { get { return Mathf.Min(SystemInfo.maxTextureSize, 8192); } }

        public MapSession(MapDocument d, TexSampler seaFill)
        {
            Build(d, seaFill);
        }

        /// <summary>
        /// (Re)cria tudo para um documento: apaga as camadas antigas, cria as novas, enche o mar
        /// com a textura dada e zera o histórico.
        /// </summary>
        public void Build(MapDocument d, TexSampler seaFill)
        {
            DestroyLayers();
            d.width = Mathf.Clamp(d.width, 256, MaxSide);
            d.height = Mathf.Clamp(d.height, 256, MaxSide);
            doc = d;
            sea = new PixelLayer(d.width, d.height, "sea");
            land = new PixelLayer(d.width, d.height, "land");
            coast = new PixelLayer(d.width, d.height, "coast");
            // a água rasa é calculada em resolução menor (no máximo ~1536 px de lado) para ser rápida
            ShoalScale = Mathf.Clamp(1536f / Mathf.Max(d.width, d.height), .25f, 1f);
            int sw = Mathf.Max(16, Mathf.RoundToInt(d.width * ShoalScale));
            int sh = Mathf.Max(16, Mathf.RoundToInt(d.height * ShoalScale));
            shoal = new PixelLayer(sw, sh, "shoal");
            if (seaFill != null) FillSea(seaFill);
            else sea.Clear(new Color32(74, 130, 170, 255));    // azul padrão se não houver textura
            undo.Clear(); undoIndex = 0;
            shoalDirty = true;
            if (Rebuilt != null) Rebuilt();
        }

        void DestroyLayers()
        {
            if (sea != null) sea.Destroy();
            if (land != null) land.Destroy();
            if (coast != null) coast.Destroy();
            if (shoal != null) shoal.Destroy();
        }

        /// <summary>Preenche todo o mar com uma textura.</summary>
        public void FillSea(TexSampler tex)
        {
            Color32[] p = sea.px; int w = sea.W, h = sea.H;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color32 c = tex.At(x, y); c.a = 255; p[y * w + x] = c;
                }
            sea.MarkAllDirty();
        }

        // ---------------- eventos ----------------
        public void RaiseChanged() { if (Changed != null) Changed(); }

        /// <summary>Chame depois de mexer na terra: refaz o contorno nessa região e agenda a água rasa.</summary>
        public void OnLandChanged(RectInt box)
        {
            if (box.width > 0 && box.height > 0) CoastRenderer.Update(land, coast, box);
            shoalDirty = true;
            if (LandChanged != null) LandChanged(box);
        }

        /// <summary>Refaz o contorno do mapa inteiro.</summary>
        public void RebuildCoast() { CoastRenderer.RebuildAll(land, coast); shoalDirty = true; }

        // ---------------- desfazer / refazer ----------------

        /// <summary>Adiciona uma ação ao histórico (descartando o que estava "à frente" se houve desfazer).</summary>
        public void Push(IUndoEntry e)
        {
            if (undoIndex < undo.Count) undo.RemoveRange(undoIndex, undo.Count - undoIndex);
            undo.Add(e);
            if (undo.Count > maxUndo) undo.RemoveAt(0);    // esquece a mais antiga
            undoIndex = undo.Count;
            RaiseChanged();
        }
        public bool CanUndo { get { return undoIndex > 0; } }
        public bool CanRedo { get { return undoIndex < undo.Count; } }
        public void Undo() { if (!CanUndo) return; undo[--undoIndex].Undo(this); RaiseChanged(); }
        public void Redo() { if (!CanRedo) return; undo[undoIndex++].Redo(this); RaiseChanged(); }

        // ---------------- vetores (objetos, rotas, textos) ----------------

        /// <summary>Fotografia dos vetores em texto (usada pelo desfazer de vetores).</summary>
        public string VectorJson() { return JsonUtility.ToJson(doc.vec); }

        /// <summary>Restaura os vetores a partir de uma fotografia.</summary>
        public void SetVector(string json)
        {
            VectorData v = JsonUtility.FromJson<VectorData>(json);
            doc.vec = v;
            if (VectorReplaced != null) VectorReplaced();   // quem desenha objetos deve se redesenhar
            RaiseChanged();
        }
        public event System.Action VectorReplaced;
    }
}
